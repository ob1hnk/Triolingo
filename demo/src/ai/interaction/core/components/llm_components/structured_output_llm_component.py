import logging
import json
import re
from typing import Any, Dict, List, Optional, Type, Union
from litellm import (
    Router,
    get_supported_openai_params,
    supports_response_schema,
    ModelResponse,
)
from pydantic import BaseModel

from ai.core.utils.provider_utils import detect_provider

from .llm_component import LLMComponent

logger = logging.getLogger(__name__)


class StructuredOutputLLMComponent(LLMComponent):
    """
    Structured Output을 지원하는 LLM Component
    JSON Schema와 Pydantic 모델을 사용하여 구조화된 응답을 받을 수 있습니다.
    """

    def __init__(
        self, prompt_path: str, router: Router, enable_validation: bool = True
    ):
        """
        StructuredOutputLLMComponent 초기화

        Args:
            prompt_path: 프롬프트 파일 경로
            router: LiteLLM Router 인스턴스
            enable_validation: JSON 스키마 검증 활성화 여부
        """
        super().__init__(prompt_path, router)
        self.enable_validation = enable_validation
        logger.info(
            f"StructuredOutputLLMComponent initialized with validation: {enable_validation}"
        )

    def check_model_support(
        self, model: str, response_format_type: str = "json_schema"
    ) -> bool:
        """
        모델이 특정 response_format을 지원하는지 확인

        Args:
            model: 모델명
            response_format_type: 지원 확인할 response_format 타입 ("json_object" 또는 "json_schema")

        Returns:
            지원 여부
        """
        try:
            if response_format_type == "json_object":
                params = get_supported_openai_params(model=model)
                return "response_format" in params
            elif response_format_type == "json_schema":
                return supports_response_schema(model=model)
            else:
                logger.warning(f"Unknown response_format_type: {response_format_type}")
                return False
        except Exception as e:
            logger.error(f"Error checking model support for {model}: {e}")
            return False

    def _clean_json_content(self, content: str) -> str:
        """
        마크다운 코드 블록을 제거하여 JSON 문자열 추출
        openrouter/anthropic 모델 계열이 structured output을 지원하지 않아 추가

        Args:
            content: 원본 콘텐츠 문자열

        Returns:
            코드 블록이 제거된 JSON 문자열
        """
        if not isinstance(content, str):
            return content

        cleaned = content.strip()

        pattern1 = r"^```(?:json)?\s*\n(.*?)\n?```\s*$"
        match = re.match(pattern1, cleaned, re.DOTALL)
        if match:
            cleaned = match.group(1).strip()
            logger.debug("Removed markdown code blocks (pattern 1)")
            return cleaned
        cleaned = re.sub(r"^```(?:json)?\s*\n?", "", cleaned, flags=re.MULTILINE)
        cleaned = re.sub(r"\s*```\s*$", "", cleaned, flags=re.MULTILINE)

        return cleaned.strip()

    def parse_content(
        self,
        response: ModelResponse,
    ) -> Union[Dict[str, Any], Any]:
        """
        litellm ModelResponse에서 구조화된 내용을 파싱

        - 이미 dict로 파싱된 경우
        - 순수 JSON 문자열
        - 마크다운 코드 블록으로 감싸진 JSON (```json ... ```)

        Args:
            response: litellm ModelResponse 객체

        Returns:
            파싱된 내용 (딕셔너리 또는 Pydantic 모델 인스턴스)
        """
        try:
            if not hasattr(response, "choices") or not response.choices:
                raise ValueError("Invalid response: no choices found")

            content = response.choices[0].message.content

            if not content:
                raise ValueError("Empty content in response")

            # 이미 dict인 경우 그대로 반환
            if isinstance(content, dict):
                logger.debug("Content is already a dict, returning as-is")
                return content

            # str이 아닌 경우 에러
            if not isinstance(content, str):
                raise ValueError(f"Content must be str or dict, got {type(content)}")

            # string인 경우 JSON 파싱 시도
            try:
                parsed_content = json.loads(content)
                logger.debug("Successfully parsed JSON without cleaning")
                return parsed_content
            except json.JSONDecodeError as first_error:
                logger.debug(
                    f"Initial JSON parse failed: {first_error}. "
                    "Attempting to clean markdown code blocks..."
                )

                # 마크다운 코드 블록 제거 후 JSON 파싱 시도
                # TODO: 가장 취약한 부분, 개선 고민 필요
                try:
                    cleaned_content = self._clean_json_content(content)
                    parsed_content = json.loads(cleaned_content)
                    logger.info(
                        "Successfully parsed JSON after removing markdown code blocks"
                    )
                    return parsed_content
                except json.JSONDecodeError as second_error:
                    logger.error(f"Failed to parse JSON after cleaning: {second_error}")
                    raise ValueError(
                        f"Invalid JSON in response. "
                        f"Initial error: {first_error}, "
                        f"After cleaning error: {second_error}"
                    )

        except Exception as e:
            logger.error(f"Failed to parse content: {e}")
            raise

    def _format_response_format(
        self,
        response_format: Union[Dict[str, Any], Type[BaseModel], str],
        strict: bool = True,
        provider: str = "other",
    ) -> Dict[str, Any]:
        if response_format == "json_object":
            return {"type": "json_object"}

        elif isinstance(response_format, type) and issubclass(
            response_format, BaseModel
        ):
            # Gemini provider는 response_mime_type과 response_schema 사용
            if provider == "google":
                json_schema = response_format.model_json_schema()
                return {
                    "response_mime_type": "application/json",
                    "response_schema": json_schema,
                }

            # OpenAI 등 기타 provider는 기존 형식
            json_schema = response_format.model_json_schema()
            payload = {
                "type": "json_schema",
                "json_schema": {
                    "name": response_format.__name__,
                    "schema": json_schema,
                },
            }
            if strict and provider == "openai":
                payload["strict"] = True
            return payload

        elif isinstance(response_format, dict):
            rf = dict(response_format)
            if "strict" in rf and provider != "openai":
                rf.pop("strict", None)
            return rf

        else:
            raise ValueError(
                f"Unsupported response_format type: {type(response_format)}"
            )

    async def call_llm_structured(
        self,
        model: str,
        messages: List[Dict[str, str]],
        response_format: Union[Dict[str, Any], Type[BaseModel], str],
        temperature: float = 0,
        max_tokens: Optional[int] = None,
        strict: bool = True,
        **kwargs,
    ) -> ModelResponse:
        """
        구조화된 출력을 위한 LLM 호출

        Args:
            model: 사용할 모델명
            messages: 대화 메시지 리스트
            response_format: 응답 형식 (Pydantic 모델, JSON 스키마 딕셔너리, 또는 "json_object")
            temperature: 온도
            max_tokens: 최대 토큰 수
            strict: JSON 스키마 strict 모드 (json_schema 타입일 때만 적용)
            **kwargs: 추가 파라미터

        Returns:
            litellm ModelResponse 객체
        """
        try:
            logger.info(f"Calling structured LLM with model: {model}")

            provider = detect_provider(model)
            strict_mode = strict if provider == "openai" else False

            formatted_response_format = self._format_response_format(
                response_format, strict_mode, provider
            )

            # 모델 지원 확인
            if isinstance(response_format, type) and issubclass(
                response_format, BaseModel
            ):
                if not self.check_model_support(model, "json_schema"):
                    logger.warning(
                        f"Model {model} may not support json_schema, but continuing..."
                    )
            elif response_format == "json_object":
                if not self.check_model_support(model, "json_object"):
                    logger.warning(
                        f"Model {model} may not support json_object, but continuing..."
                    )

            # 기본 파라미터 설정
            completion_kwargs = {
                "model": model,
                "messages": messages,
                "temperature": temperature,
                "max_tokens": max_tokens,
                "response_format": formatted_response_format,
                **kwargs,
            }

            # Claude 모델은 structured output 강제를 위해 thinking 비활성화
            if provider == "anthropic":
                completion_kwargs["thinking"] = {"type": "disabled"}

            elif provider == "bedrock":
                amrf = completion_kwargs.setdefault(
                    "additional_model_request_fields", {}
                )
                amrf["thinking"] = {"type": "disabled"}

            # Router를 통한 completion 호출
            try:
                response = await self.router.acompletion(**completion_kwargs)
                logger.info(f"Structured LLM call successful for model: {model}")
                return response

            except Exception as err:
                err_msg = str(err)
                logger.warning(
                    f"Structured LLM call failed (1st) for {model}: {err_msg}"
                )

                # Strict 모드 에러인 경우 strict 제거하고 재시도
                if "response_format.strict" in err_msg.lower():
                    rf = completion_kwargs.get("response_format", {})
                    if isinstance(rf, dict) and "strict" in rf:
                        rf = dict(rf)
                        rf.pop("strict", None)
                        completion_kwargs["response_format"] = rf
                        logger.info("Retrying without response_format.strict")
                        try:
                            response = await self.router.acompletion(
                                **completion_kwargs
                            )
                            logger.info(
                                f"Structured LLM call successful (retry without strict) for model: {model}"
                            )
                            return response
                        except Exception as retry_err:
                            logger.warning(
                                f"Retry without strict also failed: {retry_err}"
                            )

                # structured output 실패 시 json_object로 재시도
                if isinstance(response_format, type) and issubclass(
                    response_format, BaseModel
                ):
                    logger.info(
                        f"structured output failed, retrying with json_object for {model}"
                    )
                    completion_kwargs["response_format"] = {"type": "json_object"}
                    try:
                        response = await self.router.acompletion(**completion_kwargs)
                        logger.info(
                            f"Structured LLM call successful (retry with json_object) for model: {model}"
                        )
                        return response
                    except Exception as retry_err:
                        logger.warning(
                            f"Retry with json_object also failed: {retry_err}"
                        )

                raise err

        except Exception as e:
            logger.error(f"Structured LLM call failed for model {model}: {e}")
            raise

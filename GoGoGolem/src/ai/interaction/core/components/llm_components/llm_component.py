import logging
from typing import Any, Dict, List, Optional

from litellm import Router, ModelResponse
import litellm

logger = logging.getLogger(__name__)


class LLMComponent:
    """
    LLM call을 담당하는 컴포넌트
    ModelRouter를 통해 다양한 LLM 모델에 접근할 수 있습니다.
    """

    def __init__(self, prompt_path: str, router: Router):
        """
        LLMComponent 초기화

        Args:
            prompt_path: 프롬프트 파일 경로
            router: LiteLLM Router 인스턴스
        """
        self.prompt_path = prompt_path
        self.router = router
        logger.info(f"LLMComponent initialized with prompt_path: {prompt_path}")

    def parse_content(
        self,
        response: ModelResponse,
    ) -> str:
        """
        litellm ModelResponse에서 텍스트 내용을 파싱합니다.
        스트리밍이 아닌 단일 응답에서 콘텐츠를 추출하는 데 사용됩니다.

        Args:
            response: litellm ModelResponse 객체

        Returns:
            파싱된 텍스트 내용 (str)

        Raises:
            ValueError: 응답이 유효하지 않거나 내용이 비어있는 경우
        """
        try:
            if not hasattr(response, "choices") or not response.choices:
                raise ValueError("Invalid response: no choices found")

            content = response.choices[0].message.content

            if content is None:
                raise ValueError("Empty content in response")

            return content

        except (AttributeError, IndexError) as e:
            logger.error(
                f"Failed to parse content due to unexpected response structure: {e}"
            )
            raise ValueError(
                f"Failed to parse content from response: {response}"
            ) from e
        except Exception as e:
            logger.error(f"Failed to parse content: {e}")
            raise

    async def call_llm(
        self,
        model: str,
        messages: List[Dict[str, str]],
        temperature: float = 0,
        max_tokens: Optional[int] = None,
        stream: bool = False,
        build_stream: bool = False,
        **kwargs,
    ) -> Any:
        """
        LLM을 호출하는 메서드

        Args:
            model: 사용할 모델명
            messages: 대화 메시지 리스트
            temperature: 온도
            max_tokens: 최대 토큰 수
            stream: 스트리밍 여부
            build_stream: 스트리밍 응답 재구성 여부
            **kwargs: 추가 파라미터

        Returns:
            LLM 응답 객체
        """
        try:
            logger.info(f"Calling LLM with model: {model}")

            # 기본 파라미터 설정
            completion_kwargs = {
                "model": model,
                "messages": messages,
                "temperature": temperature,
                "stream": stream,
                **kwargs,
            }

            if not stream:
                response: ModelResponse = await self.router.acompletion(
                    **completion_kwargs
                )
                logger.info("LLM call successful (non-stream) for model: %s", model)
                return response

            # TODO: streaming response 구현 재확인 필요
            stream_iter = await self.router.acompletion(**completion_kwargs)
            logger.info("LLM call started (stream) for model: %s", model)

            if not build_stream:
                return stream_iter

            chunks: List[ModelResponse] = []
            async for chunk in stream_iter:
                chunks.append(chunk)

            built: ModelResponse = litellm.stream_chunk_builder(
                chunks, messages=messages
            )
            text = self.parse_content(built)
            logger.info("LLM stream built successfully for model: %s", model)
            return text

        except Exception as e:
            logger.error(f"LLM call failed for model {model}: {e}")
            raise

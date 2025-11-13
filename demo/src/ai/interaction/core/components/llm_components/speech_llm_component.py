import logging
import base64
import io
from typing import Any, Dict, List, Optional, Union, BinaryIO
from pathlib import Path
from litellm import Router

from interaction.core.infra.model_configs import ModelConfigs

logger = logging.getLogger(__name__)


class SpeechLLMComponent:
    """
    Speech-to-Text를 담당하는 컴포넌트
    Router를 통해 음성을 텍스트로 변환합니다.
    """

    def __init__(self, router: Router, prompt_path: str = ""):
        """
        SpeechLLMComponent 초기화

        Args:
            router: LiteLLM Router 인스턴스
            prompt_path: 프롬프트 파일 경로
        """
        self.prompt_path = prompt_path
        self.router = router
        logger.info(f"SpeechLLMComponent initialized with prompt_path: {prompt_path}")

    def _validate_model(self, model: str) -> bool:
        """
        모델이 지원되는지 확인

        Args:
            model: 모델명

        Returns:
            지원 여부
        """
        return model in ModelConfigs.SPEECH_MODELS

    def _validate_response_format(self, model: str, response_format: str) -> bool:
        """
        모델이 특정 response_format을 지원하는지 확인

        Args:
            model: 모델명
            response_format: 응답 형식

        Returns:
            지원 여부
        """
        if model == "whisper":
            return response_format in [
                "json",
                "text",
                "srt",
                "verbose_json",
                "vtt",
            ]
        elif model in ["gpt-4o-transcribe", "gpt-4o-mini-transcribe"]:
            return response_format in ["json", "text"]
        elif model == "gpt-4o-transcribe-diarize":
            return response_format in ["json", "text", "diarized_json"]
        return False

    def _prepare_file(self, file: Union[str, Path, BinaryIO, bytes]) -> bytes:
        """
        파일을 bytes로 변환
        asyncio.to_thread 사용을 위해 파일을 메모리로 읽음

        Args:
            file: 파일 경로(str, Path), 파일 객체(BinaryIO), 또는 bytes

        Returns:
            파일의 bytes 데이터
        """
        if isinstance(file, bytes):
            # 이미 bytes인 경우
            return file
        elif isinstance(file, (str, Path)):
            file_path = Path(file)
            if not file_path.exists():
                raise FileNotFoundError(f"Audio file not found: {file_path}")
            # 파일 크기 확인 (25MB 제한)
            file_size = file_path.stat().st_size
            if file_size > 25 * 1024 * 1024:
                raise ValueError(
                    f"File size ({file_size / 1024 / 1024:.2f}MB) exceeds 25MB limit"
                )
            with open(file_path, "rb") as f:
                return f.read()
        elif hasattr(file, "read"):
            # 이미 파일 객체인 경우 - bytes로 읽기
            if hasattr(file, "tell") and hasattr(file, "seek"):
                # 파일 위치 저장
                current_pos = file.tell()
                file.seek(0)
                file_bytes = file.read()
                file.seek(current_pos)
                return file_bytes
            else:
                # 위치 조절 불가능한 경우 처음부터 읽기
                return file.read()
        else:
            raise ValueError(
                f"Invalid file type: {type(file)}. "
                f"Expected str, Path, BinaryIO, or bytes"
            )

    def _prepare_known_speaker_references(
        self, references: List[Union[str, Path, bytes]]
    ) -> List[str]:
        """
        알려진 화자 참조 파일을 data URL 형식으로 변환

        Args:
            references: 참조 파일 경로 또는 bytes 리스트

        Returns:
            data URL 형식의 참조 리스트
        """
        data_urls = []
        for ref in references:
            if isinstance(ref, bytes):
                # 이미 bytes인 경우 base64 인코딩
                encoded = base64.b64encode(ref).decode("utf-8")
                data_urls.append(f"data:audio/wav;base64,{encoded}")
            elif isinstance(ref, (str, Path)):
                # 파일 경로인 경우 읽어서 인코딩
                file_path = Path(ref)
                if not file_path.exists():
                    logger.warning(f"Speaker reference file not found: {file_path}")
                    continue
                with open(file_path, "rb") as f:
                    file_bytes = f.read()
                    encoded = base64.b64encode(file_bytes).decode("utf-8")
                    # 파일 확장자로 MIME 타입 추정 (간단한 버전)
                    ext = file_path.suffix.lower()
                    mime_type = (
                        "audio/wav"
                        if ext == ".wav"
                        else f"audio/{ext[1:]}"
                        if ext
                        else "audio/wav"
                    )
                    data_urls.append(f"data:{mime_type};base64,{encoded}")
            else:
                logger.warning(f"Invalid reference type: {type(ref)}")
        return data_urls

    async def transcribe(
        self,
        file: Union[str, Path, BinaryIO],
        model: str = "whisper",
        response_format: str = "json",
        language: Optional[str] = None,
        prompt: Optional[str] = None,
        temperature: Optional[float] = None,
        timestamp_granularities: Optional[List[str]] = None,
        stream: bool = False,
        chunking_strategy: Optional[Union[str, Dict[str, Any]]] = None,
        known_speaker_names: Optional[List[str]] = None,
        known_speaker_references: Optional[List[Union[str, Path, bytes]]] = None,
        **kwargs,
    ) -> Any:
        """
        음성을 텍스트로 변환 (Transcription)

        Args:
            file: 오디오 파일 경로(str, Path) 또는 파일 객체(BinaryIO)
            model: 사용할 모델 (기본값: "whisper")
            response_format: 응답 형식 (기본값: "json")
                - whisper: "json", "text", "srt", "verbose_json", "vtt"
                - gpt-4o-transcribe/mini: "json", "text"
                - gpt-4o-transcribe-diarize: "json", "text", "diarized_json"
            language: 오디오 언어 코드 (ISO 639-1)
            prompt: 프롬프트 (문맥 제공, 단어 보정 등)
            temperature: 온도 (0.0 ~ 1.0)
            timestamp_granularities: 타임스탬프 세분화 ("word", "segment")
                whisper만 지원
            stream: 스트리밍 여부
            chunking_strategy: 청킹 전략 (gpt-4o-transcribe-diarize 필수, 30초 이상)
                "auto" 또는 VAD 설정 딕셔너리
            known_speaker_names: 알려진 화자 이름 리스트
            known_speaker_references: 알려진 화자 참조 오디오 파일 리스트
            **kwargs: 추가 파라미터

        Returns:
            변환된 텍스트 또는 응답 객체 (response_format에 따라 다름)

        Raises:
            ValueError: 모델이나 response_format이 지원되지 않는 경우
            FileNotFoundError: 오디오 파일을 찾을 수 없는 경우
        """
        try:
            # 모델 검증
            if not self._validate_model(model):
                raise ValueError(
                    f"Unsupported model: {model}. "
                    f"Supported models: {ModelConfigs.SPEECH_MODELS}"
                )

            # 응답 형식 검증
            if not self._validate_response_format(model, response_format):
                raise ValueError(
                    f"Model {model} does not support response_format: {response_format}"
                )

            # litellm/OpenAI API는 파일 형식을 인식하기 위해 파일 이름이 필요
            file_to_use = None

            if isinstance(file, bytes):
                # bytes인 경우 BytesIO에 name 속성 추가하여 메모리에서 처리
                file_obj = io.BytesIO(file)
                file_obj.name = "audio.wav"
                file_to_use = file_obj
                logger.info("메모리 내 파일 객체 사용 (bytes)")
            elif isinstance(file, (str, Path)):
                # 파일 경로인 경우 그대로 사용
                file_to_use = str(file)
                logger.info(f"파일 경로 사용: {file_to_use}")
            elif hasattr(file, "read"):
                # 파일 객체인 경우 bytes로 읽어서 BytesIO로 변환
                file_data = self._prepare_file(file)
                file_obj = io.BytesIO(file_data)
                file_obj.name = "audio.wav"
                file_to_use = file_obj
                logger.info("메모리 내 파일 객체 사용 (file object)")
            else:
                file_to_use = file

            try:
                logger.info(f"Transcribing audio with model: {model}")

                # 기본 파라미터 설정
                transcription_kwargs = {
                    "model": model,
                    "file": file_to_use,
                    "response_format": response_format,
                    "stream": stream,
                    **kwargs,
                }

                # 선택적 파라미터 추가
                if language:
                    transcription_kwargs["language"] = language
                if prompt:
                    transcription_kwargs["prompt"] = prompt
                if temperature is not None:
                    transcription_kwargs["temperature"] = temperature
                if timestamp_granularities:
                    if model != "whisper":
                        logger.warning(
                            "timestamp_granularities is only supported for whisper"
                        )
                    else:
                        transcription_kwargs["timestamp_granularities"] = (
                            timestamp_granularities
                        )

                # Diarization 관련 파라미터 (gpt-4o-transcribe-diarize)
                if model == "gpt-4o-transcribe-diarize":
                    if chunking_strategy:
                        transcription_kwargs["chunking_strategy"] = chunking_strategy
                    elif not stream:
                        # 30초 이상 오디오의 경우 chunking_strategy 필수
                        logger.warning(
                            "chunking_strategy is recommended for "
                            "gpt-4o-transcribe-diarize with audio > 30s"
                        )

                    if known_speaker_names and known_speaker_references:
                        if len(known_speaker_names) != len(known_speaker_references):
                            raise ValueError(
                                "known_speaker_names and "
                                "known_speaker_references must have the same length"
                            )

                        data_urls = self._prepare_known_speaker_references(
                            known_speaker_references
                        )
                        if data_urls:
                            # extra_body를 통해 전달
                            transcription_kwargs.setdefault("extra_body", {})
                            transcription_kwargs["extra_body"][
                                "known_speaker_names"
                            ] = known_speaker_names
                            transcription_kwargs["extra_body"][
                                "known_speaker_references"
                            ] = data_urls

                # Router의 atranscription을 사용 (이미 비동기이므로 asyncio.to_thread 불필요)
                if stream:
                    # 스트리밍 응답
                    response = await self.router.atranscription(**transcription_kwargs)
                    logger.info(f"Transcription stream started for model: {model}")
                    return response
                else:
                    # 일반 응답
                    response = await self.router.atranscription(**transcription_kwargs)
                    logger.info(f"Transcription successful for model: {model}")

                    # response_format에 따라 반환 형식 조정
                    if response_format == "text":
                        return (
                            response
                            if isinstance(response, str)
                            else getattr(response, "text", str(response))
                        )
                    elif response_format in ["json", "verbose_json", "diarized_json"]:
                        # dict로 변환 가능한 경우
                        if isinstance(response, dict):
                            return response
                        elif hasattr(response, "model_dump"):
                            return response.model_dump()
                        elif hasattr(response, "dict"):
                            return response.dict()
                        else:
                            return response
                    else:
                        # srt, vtt 등 다른 형식
                        return response

            except Exception as e:
                logger.error(f"Error in transcription call: {e}")
                raise

        except Exception as e:
            logger.error(f"Transcription failed for model {model}: {e}")
            raise

    def parse_content(self, response: Any, response_format: str = "json") -> str:
        """
        Transcription/Translation 응답에서 텍스트 내용을 파싱

        Args:
            response: 응답 객체
            response_format: 응답 형식

        Returns:
            파싱된 텍스트 내용 (str)

        Raises:
            ValueError: 응답이 유효하지 않거나 내용이 비어있는 경우
        """
        try:
            if response_format == "text":
                if isinstance(response, str):
                    return response
                elif hasattr(response, "text"):
                    return response.text
                else:
                    return str(response)

            elif response_format in ["json", "verbose_json", "diarized_json"]:
                if isinstance(response, dict):
                    return response.get("text", "")
                elif hasattr(response, "text"):
                    return response.text
                elif hasattr(response, "model_dump"):
                    data = response.model_dump()
                    return data.get("text", "")
                else:
                    raise ValueError(
                        f"Cannot parse text from response: {type(response)}"
                    )

            else:
                # srt, vtt 등은 그대로 반환
                return str(response)

        except Exception as e:
            logger.error(f"Failed to parse content: {e}")
            raise ValueError(f"Failed to parse content from response: {e}") from e

"""
LLM 기반 Speech-to-Speech 컴포넌트 구현 (v2) - Realtime API 사용

OpenAI Realtime API를 사용하여 실시간 음성 처리를 수행합니다.
현재는 방식 A (세션 기반 일괄 처리)를 지원하며,
기존 v1 인터페이스(SpeechToSpeechPort)를 그대로 구현합니다.
"""

import logging
from typing import BinaryIO, Optional, Union
from pathlib import Path

from interaction.speech.domain.ports.speech_to_speech import SpeechToSpeechPort
from interaction.core.components.realtime_components.realtime_llm_component import (
    RealtimeLLMComponent,
)
from interaction.speech.prompts import SYSTEM_PROMPT, MODEL_CONFIG

logger = logging.getLogger(__name__)


class LLMSpeechToSpeechV2(SpeechToSpeechPort):
    """
    Speech-to-Speech 구현 (v2) - Realtime API 사용

    OpenAI Realtime API를 사용하여 실시간으로 음성을 처리합니다.
    기존 SpeechToSpeechPort 인터페이스를 구현하여 v1과 호환됩니다.
    """

    def __init__(
        self,
        api_key: str,
        model: str = "gpt-realtime-mini",
        base_url: str = "wss://api.openai.com/v1/realtime",
        default_system_prompt: Optional[str] = None,
        transcription_model: str = "gpt-4o-mini-transcribe",
        transcription_language: str = "ko",
        timeout: float = 60.0,
    ):
        """
        초기화

        Args:
            api_key: OpenAI API 키
            model: Realtime 모델명 (기본값: gpt-realtime-mini)
            base_url: Realtime API WebSocket URL
            default_system_prompt: 기본 시스템 프롬프트 (None이면 text_to_text_v1 프롬프트 사용)
            transcription_model: 입력 오디오 transcription 모델
            transcription_language: transcription 언어 (기본: ko)
            timeout: 연결 타임아웃 (초)
        """
        self.api_key = api_key
        self.model = model
        self.base_url = base_url
        self.default_system_prompt = default_system_prompt or SYSTEM_PROMPT
        self.transcription_model = transcription_model
        self.transcription_language = transcription_language
        self.timeout = timeout
        self.temperature = MODEL_CONFIG.get("temperature", 0.8)

        logger.info(
            f"LLMSpeechToSpeechV2 initialized with model: {model}, "
            f"transcription: {transcription_model} ({transcription_language})"
        )

    def _prepare_audio_bytes(
        self, audio_file: Union[str, Path, BinaryIO, bytes]
    ) -> bytes:
        """
        오디오 파일을 bytes로 변환

        Args:
            audio_file: 오디오 파일 경로, 파일 객체, 또는 bytes

        Returns:
            오디오 데이터 bytes
        """
        if isinstance(audio_file, bytes):
            return audio_file
        elif isinstance(audio_file, (str, Path)):
            file_path = Path(audio_file)
            if not file_path.exists():
                raise FileNotFoundError(f"Audio file not found: {file_path}")
            with open(file_path, "rb") as f:
                return f.read()
        elif hasattr(audio_file, "read"):
            if hasattr(audio_file, "tell") and hasattr(audio_file, "seek"):
                current_pos = audio_file.tell()
                audio_file.seek(0)
                audio_bytes = audio_file.read()
                audio_file.seek(current_pos)
                return audio_bytes
            else:
                return audio_file.read()
        else:
            raise ValueError(
                f"Invalid audio_file type: {type(audio_file)}. "
                f"Expected str, Path, BinaryIO, or bytes"
            )

    async def generate_response_from_audio(
        self,
        audio_file: BinaryIO,
        language: str = "ko",
        system_prompt: Optional[str] = None,
    ) -> str:
        """
        사용자 오디오로부터 AI 응답을 직접 생성 (Realtime API 사용)

        Args:
            audio_file: 오디오 파일 (BinaryIO 또는 bytes)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)
            system_prompt: 시스템 프롬프트 (선택사항, None이면 기본 프롬프트 사용)

        Returns:
            AI가 생성한 응답 텍스트 (str)
        """
        try:
            logger.info(f"Generating response from audio using Realtime API (language: {language})")

            # 1. 오디오 데이터 준비
            audio_bytes = self._prepare_audio_bytes(audio_file)
            logger.debug(f"Audio prepared: {len(audio_bytes)} bytes")

            # 2. Realtime 컴포넌트 생성 및 연결
            realtime = RealtimeLLMComponent(
                api_key=self.api_key,
                base_url=self.base_url,
                model=self.model,
                timeout=self.timeout,
            )

            async with realtime:
                # 3. 세션 설정
                instructions = system_prompt or self.default_system_prompt
                await realtime.configure_session(
                    instructions=instructions,
                    modalities=["text"],  # 텍스트 출력만 사용
                    input_audio_transcription={
                        "model": self.transcription_model,
                        "language": language,
                    },
                    temperature=self.temperature,
                )

                # 4. 오디오 처리 및 응답 생성
                result = await realtime.generate_response_from_audio(
                    audio_bytes=audio_bytes,
                    modalities=["text"],
                )

                logger.info("#############")
                logger.info(result)
                logger.info("#############")

            response_text = result.get("response_text", "")

            if not response_text:
                raise ValueError("Empty response from Realtime API")

            logger.info(f"Response generation successful: {response_text[:50]}...")
            return response_text

        except Exception as e:
            logger.error(f"Error in generate_response_from_audio (v2): {e}", exc_info=True)
            raise

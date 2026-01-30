"""
LLM 기반 Speech-to-Speech 컴포넌트 구현 (v1)

OpenAI의 gpt-4o-audio-preview 모델을 사용하여 음성 입력을 직접 이해하고
AI 응답을 생성합니다. 기존 2단계 파이프라인(음성→텍스트→응답)을
단일 단계로 통합하여 레이턴시를 줄입니다.
"""

import base64
import logging
from typing import BinaryIO, Union, List, Dict, Any
from pathlib import Path
from litellm import Router

from interaction.speech.domain.ports.speech_to_speech import SpeechToSpeechPort
from interaction.core.components.llm_components.llm_component import LLMComponent
from interaction.speech.prompts.text_to_text_v2 import SYSTEM_PROMPT, MODEL_CONFIG

logger = logging.getLogger(__name__)


class LLMSpeechToSpeechV1(LLMComponent, SpeechToSpeechPort):
    """
    Speech-to-Speech 구현 (v1)

    OpenAI의 gpt-4o-audio-preview 모델을 사용하여
    음성 입력을 직접 이해하고 AI 응답 텍스트를 생성합니다.
    """

    # 지원하는 오디오 형식
    SUPPORTED_AUDIO_FORMATS = ["wav", "mp3", "flac", "m4a", "ogg", "webm"]

    def __init__(
        self,
        router: Router,
        model: str = "gpt-4o-audio-preview",
    ):
        """
        초기화

        Args:
            router: LiteLLM Router 인스턴스
            model: 사용할 모델명 (기본값: gpt-4o-audio-preview)
        """
        super().__init__(prompt_path="", router=router)
        self.model = model
        self.temperature = MODEL_CONFIG.get("temperature", 0.7)
        self.max_tokens = MODEL_CONFIG.get("max_tokens", 10000)
        logger.info(f"LLMSpeechToSpeechV1 initialized with model: {model}")

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
            # 파일 크기 확인 (25MB 제한)
            file_size = file_path.stat().st_size
            if file_size > 25 * 1024 * 1024:
                raise ValueError(
                    f"File size ({file_size / 1024 / 1024:.2f}MB) exceeds 25MB limit"
                )
            with open(file_path, "rb") as f:
                return f.read()
        elif hasattr(audio_file, "read"):
            # BinaryIO 객체
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

    def _detect_audio_format(self, audio_bytes: bytes) -> str:
        """
        오디오 데이터의 형식을 감지

        Args:
            audio_bytes: 오디오 데이터

        Returns:
            오디오 형식 문자열 (예: "wav", "mp3")
        """
        # 매직 바이트로 형식 감지
        if audio_bytes[:4] == b"RIFF" and audio_bytes[8:12] == b"WAVE":
            return "wav"
        elif audio_bytes[:3] == b"ID3" or audio_bytes[:2] == b"\xff\xfb":
            return "mp3"
        elif audio_bytes[:4] == b"fLaC":
            return "flac"
        elif audio_bytes[4:8] == b"ftyp":
            return "m4a"
        elif audio_bytes[:4] == b"OggS":
            return "ogg"
        elif audio_bytes[:4] == b"\x1a\x45\xdf\xa3":
            return "webm"
        else:
            # 기본값으로 wav 사용
            logger.warning("Could not detect audio format, assuming wav")
            return "wav"

    def _build_audio_message(
        self, audio_bytes: bytes, audio_format: str
    ) -> Dict[str, Any]:
        """
        OpenAI Audio API 형식의 메시지 구성

        Args:
            audio_bytes: 오디오 데이터
            audio_format: 오디오 형식

        Returns:
            API 메시지 딕셔너리
        """
        # Base64 인코딩
        audio_base64 = base64.b64encode(audio_bytes).decode("utf-8")

        # OpenAI audio input 형식
        return {
            "role": "user",
            "content": [
                {
                    "type": "input_audio",
                    "input_audio": {
                        "data": audio_base64,
                        "format": audio_format,
                    },
                }
            ],
        }

    async def generate_response_from_audio(
        self,
        audio_file: BinaryIO,
        language: str = "ko",
    ) -> str:
        """
        사용자 오디오로부터 AI 응답을 직접 생성

        Args:
            audio_file: 오디오 파일 (BinaryIO 또는 bytes)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            AI가 생성한 응답 텍스트 (str)
        """
        try:
            logger.info(f"Generating response from audio (language: {language})")

            # 1. 오디오 데이터 준비
            audio_bytes = self._prepare_audio_bytes(audio_file)
            audio_format = self._detect_audio_format(audio_bytes)
            logger.debug(f"Audio format detected: {audio_format}")

            # 2. 메시지 구성
            messages: List[Dict[str, Any]] = [
                {"role": "system", "content": SYSTEM_PROMPT},
                self._build_audio_message(audio_bytes, audio_format),
            ]

            # 3. API 호출 (text 출력만 사용)
            logger.info(f"Calling LLM with model: {self.model}")
            response = await self.router.acompletion(
                model=self.model,
                messages=messages,
                modalities=["text"],  # 텍스트 출력만 사용 (음성 출력 비활성화)
                temperature=self.temperature,
                max_tokens=self.max_tokens,
            )

            # 4. 응답에서 텍스트 추출
            response_text = self.parse_content(response)

            if not response_text:
                raise ValueError("Empty response from LLM")

            logger.info(f"Response generation successful: {response_text[:50]}...")
            return response_text

        except Exception as e:
            logger.error(f"Error in generate_response_from_audio: {e}", exc_info=True)
            raise

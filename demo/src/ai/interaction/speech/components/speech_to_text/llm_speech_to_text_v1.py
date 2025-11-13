"""
LLM 기반 Speech-to-Text 컴포넌트 구현 (v1)

SpeechLLMComponent를 사용하여 음성을 텍스트로 변환합니다.
"""

import logging
from typing import BinaryIO
from litellm import Router

from interaction.speech.domain.ports.speech_to_text import SpeechToTextPort
from interaction.core.components.llm_components.speech_llm_component import (
    SpeechLLMComponent,
)

logger = logging.getLogger(__name__)


class LLMSpeechToTextV1(SpeechToTextPort, SpeechLLMComponent):
    """LLM을 사용한 Speech-to-Text 구현"""

    def __init__(self, router: Router):
        """
        초기화

        Args:
            router: LiteLLM Router 인스턴스
        """
        super().__init__(router=router, prompt_path="")
        logger.info("LLMSpeechToTextV1 initialized")

    async def transcribe_user_audio_to_text(
        self, audio_file: BinaryIO, language: str = "ko"
    ) -> str:
        """
        사용자 오디오를 텍스트로 변환

        Args:
            audio_file: 오디오 파일 (BinaryIO)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            변환된 텍스트 (str)
        """
        try:
            logger.info(f"Transcribing audio to text (language: {language})")

            # SpeechLLMComponent를 사용하여 음성을 텍스트로 변환
            transcription_result = await self.transcribe(
                file=audio_file,
                model="whisper",
                response_format="json",
                language=language,
            )

            # transcription 결과에서 텍스트 추출
            if isinstance(transcription_result, dict):
                transcription_text = transcription_result.get("text", "")
            else:
                transcription_text = self.parse_content(
                    transcription_result, response_format="json"
                )

            if not transcription_text:
                raise ValueError("Failed to transcribe audio: empty result")

            logger.info(f"Transcription successful: {transcription_text}")
            return transcription_text

        except Exception as e:
            logger.error(f"Error in transcribe_user_audio_to_text: {e}", exc_info=True)
            raise

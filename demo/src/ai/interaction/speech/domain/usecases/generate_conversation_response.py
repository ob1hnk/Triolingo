"""
음성 입력을 받아서 대화 응답을 생성하는 UseCase

1. SpeechToTextPort를 통해 음성을 텍스트로 변환
2. TextToTextPort를 통해 텍스트 응답 생성
"""

import logging
from typing import Dict, Any, BinaryIO
from interaction.speech.domain.ports.speech_to_text import SpeechToTextPort
from interaction.speech.domain.ports.text_to_text import TextToTextPort

logger = logging.getLogger(__name__)


class GenerateConversationResponseUseCase:
    """
    음성 파일을 받아서 대화 응답을 생성하는 UseCase
    """

    def __init__(
        self,
        speech_to_text: SpeechToTextPort,
        text_to_text: TextToTextPort | None = None,
    ):
        """
        UseCase 초기화

        Args:
            speech_to_text: Speech-to-Text 포트 구현체
            text_to_text: Text-to-Text 포트 구현체 (None이면 transcription만 반환)
        """
        self.speech_to_text = speech_to_text
        self.text_to_text = text_to_text
        logger.info("GenerateConversationResponseUseCase initialized")

    async def execute(
        self, audio_file: BinaryIO | bytes, language: str = "ko"
    ) -> Dict[str, Any]:
        """
        음성 파일을 처리하여 대화 응답을 생성

        Args:
            audio_file: 오디오 파일 (BinaryIO 또는 bytes)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            {
                "transcription": str,  # 음성 인식 결과
                "response": str,        # 생성된 응답
            }
        """
        try:
            logger.info("Starting conversation response generation")

            # 1. 음성을 텍스트로 변환 (Speech-to-Text)
            logger.info("Step 1: Converting speech to text")
            transcription_text = (
                await self.speech_to_text.transcribe_user_audio_to_text(
                    audio_file, language=language
                )
            )

            if not transcription_text:
                raise ValueError("Failed to transcribe audio: empty result")

            logger.info(f"Transcription successful: {transcription_text}")

            # 2. 텍스트로부터 응답 생성 (Text-to-Text)
            logger.info("Step 2: Generating response from text")
            response_text = (
                await self.text_to_text.create_response_from_user_audio_text(
                    transcription_text
                )
            )

            logger.info("Conversation response generation completed successfully")

            return {
                "transcription": transcription_text,
                "response": response_text,
            }

        except Exception as e:
            logger.error(f"Error in generate_conversation_response: {e}", exc_info=True)
            raise

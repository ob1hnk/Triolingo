"""
음성 입력을 받아서 대화 응답을 직접 생성하는 UseCase (v3) - Realtime API

OpenAI Realtime API를 사용하여 저지연 음성 처리를 수행합니다.
기존 v2와 동일한 인터페이스를 유지하면서 내부적으로 Realtime API를 사용합니다.
"""

import logging
from typing import Any, BinaryIO, Dict, Optional

from interaction.speech.domain.ports.speech_to_speech import SpeechToSpeechPort

logger = logging.getLogger(__name__)


class GenerateConversationResponseUseCaseV3:
    """
    음성 파일을 받아서 대화 응답을 직접 생성하는 UseCase (v3)

    Realtime API 기반의 SpeechToSpeechPort를 사용합니다.
    v2와 동일한 인터페이스를 유지하여 호환성을 보장합니다.
    """

    def __init__(
        self,
        speech_to_speech: SpeechToSpeechPort,
    ):
        """
        UseCase 초기화

        Args:
            speech_to_speech: Speech-to-Speech 포트 구현체 (LLMSpeechToSpeechV2)
        """
        self.speech_to_speech = speech_to_speech
        logger.info("GenerateConversationResponseUseCaseV3 initialized (Realtime API)")

    async def execute(
        self,
        audio_file: BinaryIO | bytes,
        language: str = "ko",
        system_prompt: Optional[str] = None,
    ) -> Dict[str, Any]:
        """
        음성 파일을 처리하여 대화 응답을 직접 생성

        Args:
            audio_file: 오디오 파일 (BinaryIO 또는 bytes)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)
            system_prompt: 시스템 프롬프트 (선택사항)

        Returns:
            {
                "response": str,  # AI가 생성한 응답
            }

        Note:
            v2와 동일한 반환 형식을 유지합니다.
            내부적으로 Realtime API를 사용하여 저지연 처리를 수행합니다.
        """
        try:
            logger.info("Starting conversation response generation (v3 - Realtime API)")

            # Speech-to-Speech: Realtime API를 통한 응답 생성
            response_text = await self.speech_to_speech.generate_response_from_audio(
                audio_file=audio_file,
                language=language,
                system_prompt=system_prompt,
            )

            logger.info("Conversation response generation completed successfully (v3)")

            return {
                "response": response_text,
            }

        except Exception as e:
            logger.error(
                f"Error in generate_conversation_response (v3): {e}", exc_info=True
            )
            raise

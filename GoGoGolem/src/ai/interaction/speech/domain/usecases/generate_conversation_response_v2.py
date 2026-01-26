"""
음성 입력을 받아서 대화 응답을 직접 생성하는 UseCase (v2)

Speech-to-Speech API를 사용하여 단일 API 호출로 응답을 생성합니다.
기존 v1의 2단계 파이프라인(음성→텍스트→응답)을 단일 단계로 통합하여
레이턴시를 줄입니다.
"""

import logging
from typing import Dict, Any, BinaryIO, Optional
from opentelemetry import trace
from opentelemetry.trace import Status, StatusCode

from interaction.speech.domain.ports.speech_to_speech import SpeechToSpeechPort
from interaction.core.utils.trace_context import get_trace_id
from interaction.core.utils.tracing import get_tracer

logger = logging.getLogger(__name__)
tracer = get_tracer(__name__)


class GenerateConversationResponseUseCaseV2:
    """
    음성 파일을 받아서 대화 응답을 직접 생성하는 UseCase (v2)

    SpeechToSpeechPort를 사용하여 단일 API 호출로 응답을 생성합니다.
    """

    def __init__(
        self,
        speech_to_speech: SpeechToSpeechPort,
    ):
        """
        UseCase 초기화

        Args:
            speech_to_speech: Speech-to-Speech 포트 구현체
        """
        self.speech_to_speech = speech_to_speech
        logger.info("GenerateConversationResponseUseCaseV2 initialized")

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
            v1과 달리 transcription은 반환하지 않습니다.
            단일 API 호출로 음성 이해와 응답 생성을 수행합니다.
        """
        try:
            logger.info("Starting conversation response generation (v2 - single step)")
            trace_id = get_trace_id()

            # Speech-to-Speech: 음성에서 직접 응답 생성
            with tracer.start_as_current_span("speech_to_speech") as s2s_span:
                if trace_id:
                    s2s_span.set_attribute("trace_id", trace_id)
                s2s_span.set_attribute("language", language)
                if system_prompt:
                    s2s_span.set_attribute("has_system_prompt", True)

                response_text = await self.speech_to_speech.generate_response_from_audio(
                    audio_file=audio_file,
                    language=language,
                    system_prompt=system_prompt,
                )

                s2s_span.set_attribute("response_length", len(response_text))
                logger.info("Conversation response generation completed successfully (v2)")

            return {
                "response": response_text,
            }

        except Exception as e:
            logger.error(
                f"Error in generate_conversation_response (v2): {e}", exc_info=True
            )
            current_span = trace.get_current_span()
            if current_span:
                current_span.record_exception(e)
                current_span.set_status(Status(StatusCode.ERROR, str(e)))
            raise

"""
음성 입력을 받아서 대화 응답을 생성하는 UseCase

1. SpeechToTextPort를 통해 음성을 텍스트로 변환
2. TextToTextPort를 통해 텍스트 응답 생성
"""

import logging
from typing import Dict, Any, BinaryIO
from opentelemetry import trace
from opentelemetry.trace import Status, StatusCode

from interaction.speech.domain.ports.speech_to_text import SpeechToTextPort
from interaction.speech.domain.ports.text_to_text import TextToTextPort
from interaction.core.utils.trace_context import get_trace_id
from interaction.core.utils.tracing import get_tracer

logger = logging.getLogger(__name__)
tracer = get_tracer(__name__)


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
            trace_id = get_trace_id()

            # 1. 음성을 텍스트로 변환 (Speech-to-Text)
            logger.info("Step 1: Converting speech to text")
            with tracer.start_as_current_span("stt") as stt_span:
                if trace_id:
                    stt_span.set_attribute("trace_id", trace_id)
                stt_span.set_attribute("language", language)

                transcription_text = (
                    await self.speech_to_text.transcribe_user_audio_to_text(
                        audio_file, language=language
                    )
                )

                if not transcription_text:
                    raise ValueError("Failed to transcribe audio: empty result")

                stt_span.set_attribute("transcription_length", len(transcription_text))
                logger.info(f"Transcription successful: {transcription_text}")

            # 2. 텍스트로부터 응답 생성 (Text-to-Text)
            logger.info("Step 2: Generating response from text")
            with tracer.start_as_current_span("llm_call") as llm_span:
                if trace_id:
                    llm_span.set_attribute("trace_id", trace_id)
                llm_span.set_attribute("input_length", len(transcription_text))

                response_text = (
                    await self.text_to_text.create_response_from_user_audio_text(
                        transcription_text
                    )
                )

                llm_span.set_attribute("response_length", len(response_text))
                logger.info("Response generation successful")

            logger.info("Conversation response generation completed successfully")

            return {
                "transcription": transcription_text,
                "response": response_text,
            }

        except Exception as e:
            logger.error(f"Error in generate_conversation_response: {e}", exc_info=True)
            current_span = trace.get_current_span()
            if current_span:
                current_span.record_exception(e)
                current_span.set_status(Status(StatusCode.ERROR, str(e)))
            raise

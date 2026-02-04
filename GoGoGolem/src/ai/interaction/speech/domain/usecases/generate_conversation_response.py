"""
음성 입력을 받아서 대화 응답을 생성하는 UseCase

1. SpeechToTextPort를 통해 음성을 텍스트로 변환
2. TextToTextPort를 통해 텍스트 응답 생성

멀티턴 대화를 지원하여 이전 대화 이력을 기반으로 응답을 생성합니다.
세션별 대화 이력을 내부에서 관리합니다.
"""

import logging
from typing import Dict, Any
from interaction.speech.domain.ports.speech_to_text import SpeechToTextPort
from interaction.speech.domain.ports.text_to_text import TextToTextPort
from interaction.speech.domain.entity.conversation import ConversationHistory
from interaction.speech.domain.entity.voice_input import VoiceInput

logger = logging.getLogger(__name__)


class GenerateConversationResponseUseCase:
    """
    음성 파일을 받아서 대화 응답을 생성하는 UseCase (멀티턴 대화 지원)

    세션별 대화 이력을 내부에서 관리합니다.
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
        self._sessions: Dict[str, ConversationHistory] = {}
        logger.info("GenerateConversationResponseUseCase initialized")

    def _get_or_create_history(self, session_id: str) -> ConversationHistory:
        """세션의 대화 이력을 가져오거나 생성"""
        if session_id not in self._sessions:
            self._sessions[session_id] = ConversationHistory()
            logger.info(f"New conversation session created: {session_id}")
        return self._sessions[session_id]

    def clear_session(self, session_id: str) -> None:
        """세션 대화 이력 삭제 (메모리 정리)"""
        if session_id in self._sessions:
            message_count = len(self._sessions[session_id])
            del self._sessions[session_id]
            logger.info(
                f"Conversation session cleared: {session_id} ({message_count} messages)"
            )

    async def execute(
        self,
        voice_input: VoiceInput,
        session_id: str,
        language: str = "ko",
    ) -> Dict[str, Any]:
        """
        음성 파일을 처리하여 대화 응답을 생성

        Args:
            voice_input: 음성 입력 데이터
            session_id: 대화 세션 ID (WebSocket 연결별 고유 ID)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            {
                "transcription": str,  # 음성 인식 결과
                "response": str,        # 생성된 응답
            }
        """
        try:
            # 세션의 대화 이력 가져오기
            history = self._get_or_create_history(session_id)
            is_first_turn = history.is_empty()

            logger.info(
                f"Starting conversation response generation "
                f"(session_id={session_id}, first_turn={is_first_turn}, "
                f"history_length={len(history)})"
            )

            # 1. 음성을 텍스트로 변환 (Speech-to-Text)
            logger.info("Step 1: Converting speech to text")
            transcription_text = (
                await self.speech_to_text.transcribe_user_audio_to_text(
                    voice_input, language=language
                )
            )

            if not transcription_text:
                raise ValueError("Failed to transcribe audio: empty result")

            logger.info(f"Transcription successful: {transcription_text}")

            # 2. 텍스트로부터 응답 생성 (Text-to-Text with conversation history)
            logger.info("Step 2: Generating response from text")
            response_text = (
                await self.text_to_text.create_response_from_user_audio_text(
                    text=transcription_text,
                    conversation_history=history,
                )
            )

            # 3. 대화 이력에 저장
            history.add_user_message(transcription_text)
            history.add_assistant_message(response_text)

            logger.info(
                f"Conversation response generation completed "
                f"(session_id={session_id}, history_length={len(history)})"
            )

            return {
                "transcription": transcription_text,
                "response": response_text,
            }

        except Exception as e:
            logger.error(f"Error in generate_conversation_response: {e}", exc_info=True)
            raise

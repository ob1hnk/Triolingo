"""
음성 입력을 받아서 대화 응답을 직접 생성하는 UseCase (v2)

Speech-to-Speech API를 사용하여 단일 API 호출로 응답을 생성합니다.
기존 v1의 2단계 파이프라인(음성→텍스트→응답)을 단일 단계로 통합하여
레이턴시를 줄입니다.

멀티턴 대화를 지원하여 이전 대화 이력을 기반으로 응답을 생성합니다.
세션별 대화 이력을 내부에서 관리합니다.
"""

import logging
from typing import Dict, Any
from interaction.speech.domain.ports.speech_to_speech import SpeechToSpeechPort
from interaction.speech.domain.entity.conversation import ConversationHistory
from interaction.speech.domain.entity.voice_input import VoiceInput

logger = logging.getLogger(__name__)


class GenerateConversationResponseUseCaseV2:
    """
    음성 파일을 받아서 대화 응답을 직접 생성하는 UseCase (v2)

    SpeechToSpeechPort를 사용하여 단일 API 호출로 응답을 생성합니다.
    세션별 대화 이력을 내부에서 관리합니다.
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
        self._sessions: Dict[str, ConversationHistory] = {}
        logger.info("GenerateConversationResponseUseCaseV2 initialized")

    def _get_or_create_history(self, session_id: str) -> ConversationHistory:
        """세션의 대화 이력을 가져오거나 생성"""
        if session_id not in self._sessions:
            self._sessions[session_id] = ConversationHistory()
            logger.info(f"New conversation session created (v2): {session_id}")
        return self._sessions[session_id]

    def clear_session(self, session_id: str) -> None:
        """세션 대화 이력 삭제 (메모리 정리)"""
        if session_id in self._sessions:
            message_count = len(self._sessions[session_id])
            del self._sessions[session_id]
            logger.info(
                f"Conversation session cleared (v2): {session_id} ({message_count} messages)"
            )

    async def execute(
        self,
        voice_input: VoiceInput,
        session_id: str,
        language: str = "ko",
    ) -> Dict[str, Any]:
        """
        음성 파일을 처리하여 대화 응답을 직접 생성

        Args:
            voice_input: 음성 입력 데이터
            session_id: 대화 세션 ID (WebSocket 연결별 고유 ID)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            {
                "response": str,  # AI가 생성한 응답
            }

        Note:
            v1과 달리 transcription은 반환하지 않습니다.
            단일 API 호출로 음성 이해와 응답 생성을 수행합니다.
        """
        try:
            # 세션의 대화 이력 가져오기
            history = self._get_or_create_history(session_id)
            is_first_turn = history.is_empty()

            logger.info(
                f"Starting conversation response generation (v2 - single step, "
                f"session_id={session_id}, first_turn={is_first_turn}, "
                f"history_length={len(history)})"
            )

            # Speech-to-Speech: 음성에서 직접 응답 생성
            response_text = await self.speech_to_speech.generate_response_from_audio(
                voice_input=voice_input,
                conversation_history=history,
                language=language,
            )

            # 대화 이력에 저장 (v2는 transcription 없으므로 assistant만)
            history.add_assistant_message(response_text)

            logger.info(
                f"Conversation response generation completed (v2) "
                f"(session_id={session_id}, history_length={len(history)})"
            )

            return {
                "response": response_text,
            }

        except Exception as e:
            logger.error(
                f"Error in generate_conversation_response (v2): {e}", exc_info=True
            )
            raise

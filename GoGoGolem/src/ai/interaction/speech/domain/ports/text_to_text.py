"""
Text-to-Text 포트 인터페이스

텍스트로부터 응답을 생성하는 기능을 정의합니다.
멀티턴 대화를 지원하여 이전 대화 이력을 기반으로 응답을 생성합니다.
"""

from abc import ABC, abstractmethod
from typing import Optional

from interaction.speech.domain.entity.conversation import ConversationHistory


class TextToTextPort(ABC):
    """텍스트로부터 응답을 생성하는 포트 인터페이스"""

    @abstractmethod
    async def create_response_from_user_audio_text(
        self,
        text: str,
        conversation_history: Optional[ConversationHistory] = None,
    ) -> str:
        """
        사용자 오디오에서 변환된 텍스트로부터 응답 생성

        Args:
            text: 사용자 오디오에서 변환된 텍스트
            conversation_history: 이전 대화 이력 (멀티턴 대화 지원)

        Returns:
            생성된 응답 텍스트 (str)
        """
        pass

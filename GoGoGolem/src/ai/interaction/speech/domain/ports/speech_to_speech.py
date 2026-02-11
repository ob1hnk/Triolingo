"""
Speech-to-Speech 포트 인터페이스 (v2)

음성 입력을 받아 AI 응답을 직접 생성하는 기능을 정의합니다.
OpenAI의 Speech-to-Speech API를 활용하며, 텍스트 출력만 사용합니다.
기존 2단계 파이프라인(음성→텍스트→응답)을 단일 단계로 통합하여 레이턴시를 줄입니다.

멀티턴 대화를 지원하여 이전 대화 이력을 기반으로 응답을 생성합니다.
"""

from abc import ABC, abstractmethod
from typing import Optional

from interaction.speech.domain.entity.conversation import ConversationHistory
from interaction.speech.domain.entity.voice_input import VoiceInput


class SpeechToSpeechPort(ABC):
    """
    음성 입력을 받아 AI 응답을 직접 생성하는 포트 인터페이스

    이 포트는 OpenAI의 gpt-4o-audio-preview 모델처럼
    음성을 직접 이해하고 응답을 생성하는 모델을 위한 것입니다.
    텍스트 출력 모드만 사용하여 AI 응답 텍스트를 반환합니다.
    멀티턴 대화를 지원합니다.
    """

    @abstractmethod
    async def generate_response_from_audio(
        self,
        voice_input: VoiceInput,
        conversation_history: Optional[ConversationHistory] = None,
        language: str = "ko",
    ) -> str:
        """
        사용자 오디오로부터 AI 응답을 직접 생성

        Args:
            voice_input: 음성 입력 데이터
            conversation_history: 이전 대화 이력 (멀티턴 대화 지원)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            AI가 생성한 응답 텍스트 (str)

        Note:
            이 메서드는 별도의 transcription을 반환하지 않습니다.
            단일 API 호출로 음성 이해와 응답 생성을 수행합니다.
            conversation_history가 제공되면 이전 대화 맥락을 포함하여 응답을 생성합니다.
        """
        pass

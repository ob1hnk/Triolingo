"""
Speech-to-Text 포트 인터페이스

음성을 텍스트로 변환하는 기능을 정의합니다.
"""

from abc import ABC, abstractmethod

from interaction.speech.domain.entity.voice_input import VoiceInput


class SpeechToTextPort(ABC):
    """음성을 텍스트로 변환하는 포트 인터페이스"""

    @abstractmethod
    async def transcribe_user_audio_to_text(
        self, voice_input: VoiceInput, language: str = "ko"
    ) -> str:
        """
        사용자 오디오를 텍스트로 변환

        Args:
            voice_input: 음성 입력 데이터
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            변환된 텍스트 (str)
        """
        pass

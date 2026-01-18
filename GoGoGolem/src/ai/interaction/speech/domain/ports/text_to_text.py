"""
Text-to-Text 포트 인터페이스

텍스트로부터 응답을 생성하는 기능을 정의합니다.
"""

from abc import ABC, abstractmethod


class TextToTextPort(ABC):
    """텍스트로부터 응답을 생성하는 포트 인터페이스"""

    @abstractmethod
    async def create_response_from_user_audio_text(self, text: str) -> str:
        """
        사용자 오디오에서 변환된 텍스트로부터 응답 생성

        Args:
            text: 사용자 오디오에서 변환된 텍스트

        Returns:
            생성된 응답 텍스트 (str)
        """
        pass

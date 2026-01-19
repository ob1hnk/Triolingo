"""
Text-to-Text 포트 인터페이스

텍스트로부터 응답을 생성하는 기능을 정의합니다.
"""

from abc import ABC, abstractmethod


class TextToTextPort(ABC):
    """텍스트로부터 응답을 생성하는 포트 인터페이스"""

    @abstractmethod
    async def generate_letter_content_from_user_input(self, text: str) -> str:
        pass

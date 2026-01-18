"""
Speech-to-Speech 포트 인터페이스 (v2)

음성 입력을 받아 AI 응답을 직접 생성하는 기능을 정의합니다.
OpenAI의 Speech-to-Speech API를 활용하며, 텍스트 출력만 사용합니다.
기존 2단계 파이프라인(음성→텍스트→응답)을 단일 단계로 통합하여 레이턴시를 줄입니다.
"""

from abc import ABC, abstractmethod
from typing import BinaryIO, Optional


class SpeechToSpeechPort(ABC):
    """
    음성 입력을 받아 AI 응답을 직접 생성하는 포트 인터페이스

    이 포트는 OpenAI의 gpt-4o-audio-preview 모델처럼
    음성을 직접 이해하고 응답을 생성하는 모델을 위한 것입니다.
    텍스트 출력 모드만 사용하여 AI 응답 텍스트를 반환합니다.
    """

    @abstractmethod
    async def generate_response_from_audio(
        self,
        audio_file: BinaryIO,
        language: str = "ko",
        system_prompt: Optional[str] = None,
    ) -> str:
        """
        사용자 오디오로부터 AI 응답을 직접 생성

        Args:
            audio_file: 오디오 파일 (BinaryIO 또는 bytes)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)
            system_prompt: 시스템 프롬프트 (선택사항, None이면 기본 프롬프트 사용)

        Returns:
            AI가 생성한 응답 텍스트 (str)

        Note:
            이 메서드는 별도의 transcription을 반환하지 않습니다.
            단일 API 호출로 음성 이해와 응답 생성을 수행합니다.
        """
        pass

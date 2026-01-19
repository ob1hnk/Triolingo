"""
Letter Response Repository 추상 인터페이스

편지 응답 데이터를 저장하고 조회하는 기능을 정의합니다.
이 추상 클래스를 구현하여 Firebase, MongoDB 등 다양한 저장소를 사용할 수 있습니다.
"""

from abc import ABC, abstractmethod
from typing import List, Optional

from GoGoGolem.src.ai.interaction.text.domain.entity.letter import Letter


class LetterResponseRepositoryPort(ABC):
    """
    Letter Response Repository 추상 인터페이스

    이 인터페이스를 구현하여 다양한 저장소(Firebase, MongoDB 등)를 사용할 수 있습니다.
    도메인 레이어는 이 인터페이스에만 의존하므로, 저장소 구현을 쉽게 교체할 수 있습니다.
    """

    @abstractmethod
    async def save(self, letter_response: Letter) -> str:
        """
        편지 응답 저장

        Args:
            letter_response: 저장할 편지 응답 데이터

        Returns:
            저장된 문서의 ID
        """
        pass

    @abstractmethod
    async def get_by_id(self, letter_id: str) -> Optional[Letter]:
        """
        ID로 편지 응답 조회

        Args:
            letter_id: 편지 응답 ID

        Returns:
            편지 응답 데이터 또는 None
        """
        pass

    @abstractmethod
    async def get_by_user_id(self, user_id: str) -> List[Letter]:
        """
        사용자 ID로 편지 응답 목록 조회

        Args:
            user_id: 사용자 ID

        Returns:
            편지 응답 목록
        """
        pass

    @abstractmethod
    async def update(self, letter_id: str, letter_response: Letter) -> None:
        """
        편지 응답 업데이트

        Args:
            letter_id: 편지 응답 ID
            letter_response: 업데이트할 데이터
        """
        pass

    @abstractmethod
    async def delete(self, letter_id: str) -> None:
        """
        편지 응답 삭제

        Args:
            letter_id: 편지 응답 ID
        """
        pass

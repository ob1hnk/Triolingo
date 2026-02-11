"""
Letter Response Entity

편지 응답 도메인 엔티티를 정의합니다.
"""

from dataclasses import dataclass
from datetime import datetime
from typing import Optional


@dataclass
class Letter:
    """편지 응답 데이터 모델"""

    id: Optional[str] = None
    task_id: str = ""
    user_id: str = ""
    user_letter: str = ""
    generated_response_letter: str = ""
    created_at: Optional[datetime] = None
    updated_at: Optional[datetime] = None

    def to_dict(self) -> dict:
        """딕셔너리로 변환"""
        return {
            "id": self.id,
            "task_id": self.task_id,
            "user_id": self.user_id,
            "user_letter": self.user_letter,
            "generated_response_letter": self.generated_response_letter,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "updated_at": self.updated_at.isoformat() if self.updated_at else None,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "Letter":
        """딕셔너리에서 생성"""
        created_at = None
        updated_at = None

        if data.get("created_at"):
            if isinstance(data["created_at"], str):
                created_at = datetime.fromisoformat(data["created_at"])
            else:
                created_at = data["created_at"]

        if data.get("updated_at"):
            if isinstance(data["updated_at"], str):
                updated_at = datetime.fromisoformat(data["updated_at"])
            else:
                updated_at = data["updated_at"]

        return cls(
            id=data.get("id"),
            task_id=data.get("task_id", ""),
            user_id=data.get("user_id", ""),
            user_letter=data.get("user_letter", ""),
            generated_response_letter=data.get("generated_response_letter", ""),
            created_at=created_at,
            updated_at=updated_at,
        )

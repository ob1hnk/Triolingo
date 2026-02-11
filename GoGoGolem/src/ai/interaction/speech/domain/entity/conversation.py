"""
대화 관련 엔티티
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import List, Dict


class MessageRole(str, Enum):
    """메시지 역할"""

    USER = "user"
    ASSISTANT = "assistant"
    SYSTEM = "system"


@dataclass
class ConversationMessage:
    """
    대화 메시지 엔티티

    단일 대화 메시지를 나타냅니다.
    """

    role: MessageRole
    content: str

    def to_dict(self) -> Dict[str, str]:
        """OpenAI API 형식의 딕셔너리로 변환"""
        return {
            "role": self.role.value,
            "content": self.content,
        }

    @classmethod
    def user(cls, content: str) -> "ConversationMessage":
        """사용자 메시지 생성"""
        return cls(role=MessageRole.USER, content=content)

    @classmethod
    def assistant(cls, content: str) -> "ConversationMessage":
        """어시스턴트 메시지 생성"""
        return cls(role=MessageRole.ASSISTANT, content=content)

    @classmethod
    def system(cls, content: str) -> "ConversationMessage":
        """시스템 메시지 생성"""
        return cls(role=MessageRole.SYSTEM, content=content)


@dataclass
class ConversationHistory:
    """
    대화 이력 엔티티

    여러 대화 메시지를 순서대로 보관합니다.
    """

    messages: List[ConversationMessage] = field(default_factory=list)

    def add_user_message(self, content: str) -> None:
        """사용자 메시지 추가"""
        self.messages.append(ConversationMessage.user(content))

    def add_assistant_message(self, content: str) -> None:
        """어시스턴트 메시지 추가"""
        self.messages.append(ConversationMessage.assistant(content))

    def add_message(self, message: ConversationMessage) -> None:
        """메시지 추가"""
        self.messages.append(message)

    def to_list(self) -> List[Dict[str, str]]:
        """OpenAI API 형식의 리스트로 변환"""
        return [msg.to_dict() for msg in self.messages]

    def is_empty(self) -> bool:
        """대화 이력이 비어있는지 확인"""
        return len(self.messages) == 0

    def clear(self) -> None:
        """대화 이력 초기화"""
        self.messages.clear()

    def __len__(self) -> int:
        """메시지 개수"""
        return len(self.messages)

    def __iter__(self):
        """메시지 순회"""
        return iter(self.messages)

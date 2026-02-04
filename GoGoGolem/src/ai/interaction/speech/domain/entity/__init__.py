"""
Speech 도메인 엔티티
"""

from interaction.speech.domain.entity.voice_input import VoiceInput
from interaction.speech.domain.entity.conversation import (
    ConversationMessage,
    ConversationHistory,
    MessageRole,
)

__all__ = [
    "VoiceInput",
    "ConversationMessage",
    "ConversationHistory",
    "MessageRole",
]

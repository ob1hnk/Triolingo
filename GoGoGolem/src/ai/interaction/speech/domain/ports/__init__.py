"""Speech 도메인 포트 인터페이스 모듈"""

from .speech_to_text import SpeechToTextPort
from .text_to_text import TextToTextPort
from .speech_to_speech import SpeechToSpeechPort

__all__ = ["SpeechToTextPort", "TextToTextPort", "SpeechToSpeechPort"]

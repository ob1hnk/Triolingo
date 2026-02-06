"""
Realtime API 기반 음성 처리 컴포넌트

OpenAI Realtime API를 사용하여 실시간 음성 처리를 수행합니다.
Server VAD를 지원하며, 세션 내에서 자동으로 대화 컨텍스트를 유지합니다.
"""

import logging
from typing import Optional

from interaction.core.components.realtime_components.realtime_llm_component import (
    RealtimeLLMComponent,
)
from interaction.speech.prompts.text_to_text_v2 import SYSTEM_PROMPT, MODEL_CONFIG

logger = logging.getLogger(__name__)


class LLMRealtimeSpeechV1(RealtimeLLMComponent):
    """
    Realtime API 기반 음성 처리 컴포넌트

    OpenAI Realtime API를 사용하여 실시간으로 음성을 처리합니다.
    Server VAD를 활성화하여 자동으로 발화/침묵을 감지합니다.
    세션 내에서 멀티턴 대화 컨텍스트를 자동 유지합니다.
    """

    def __init__(
        self,
        api_key: str,
        model: str = "gpt-realtime-mini",
        base_url: str = "wss://api.openai.com/v1/realtime",
        default_system_prompt: Optional[str] = None,
        transcription_model: str = "gpt-4o-mini-transcribe",
        transcription_language: str = "ko",
        timeout: float = 60.0,
    ):
        """
        초기화

        Args:
            api_key: OpenAI API 키
            model: Realtime 모델명 (기본값: gpt-realtime-mini)
            base_url: Realtime API WebSocket URL
            default_system_prompt: 기본 시스템 프롬프트 (None이면 text_to_text_v2 프롬프트 사용)
            transcription_model: 입력 오디오 transcription 모델
            transcription_language: transcription 언어 (기본: ko)
            timeout: 연결 타임아웃 (초)
        """
        super().__init__(
            api_key=api_key,
            base_url=base_url,
            model=model,
            timeout=timeout,
        )

        self.default_system_prompt = default_system_prompt or SYSTEM_PROMPT
        self.transcription_model = transcription_model
        self.transcription_language = transcription_language
        self.temperature = MODEL_CONFIG.get("temperature", 0.8)

        logger.info(
            f"LLMRealtimeSpeechV1 initialized with model: {model}, "
            f"transcription: {transcription_model} ({transcription_language})"
        )

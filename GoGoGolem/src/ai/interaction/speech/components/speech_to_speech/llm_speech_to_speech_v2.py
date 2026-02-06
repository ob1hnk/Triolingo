"""
RealTime LLM 기반 Speech-to-Speech 컴포넌트 구현 (v2) - Realtime API 사용

OpenAI Realtime API를 사용하여 실시간 음성 처리를 수행합니다.
방식 B (실시간 스트리밍)를 지원하며, RealtimeLLMComponent를 상속합니다.
"""

import logging
from typing import Optional

from interaction.speech.domain.ports.speech_to_speech import SpeechToSpeechPort
from interaction.core.components.realtime_components.realtime_llm_component import (
    RealtimeLLMComponent,
)
from interaction.speech.domain.entity.conversation import ConversationHistory
from interaction.speech.domain.entity.voice_input import VoiceInput
from interaction.speech.prompts import SYSTEM_PROMPT, MODEL_CONFIG

logger = logging.getLogger(__name__)


class LLMSpeechToSpeechV2(RealtimeLLMComponent, SpeechToSpeechPort):
    """
    Speech-to-Speech 구현 (v2) - Realtime API 사용

    OpenAI Realtime API를 사용하여 실시간으로 음성을 처리합니다.
    RealtimeLLMComponent를 상속하고 SpeechToSpeechPort 인터페이스를 구현합니다.
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
            default_system_prompt: 기본 시스템 프롬프트 (None이면 text_to_text_v1 프롬프트 사용)
            transcription_model: 입력 오디오 transcription 모델
            transcription_language: transcription 언어 (기본: ko)
            timeout: 연결 타임아웃 (초)
        """
        # RealtimeLLMComponent 초기화
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
            f"LLMSpeechToSpeechV2 initialized with model: {model}, "
            f"transcription: {transcription_model} ({transcription_language})"
        )

    async def generate_response_from_audio(
        self,
        voice_input: VoiceInput,
        conversation_history: Optional[ConversationHistory] = None,
        language: str = "ko",
    ) -> str:
        """
        사용자 오디오로부터 AI 응답을 직접 생성 (Realtime API 사용)

        Args:
            voice_input: 음성 입력 데이터 (VoiceInput 엔티티)
            conversation_history: 대화 이력 (멀티턴 지원)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            AI가 생성한 응답 텍스트 (str)
        """
        try:
            is_first_turn = conversation_history is None or conversation_history.is_empty()
            history_length = 0 if conversation_history is None else len(conversation_history)

            logger.info(
                f"Generating response from audio using Realtime API "
                f"(language: {language}, first_turn: {is_first_turn}, "
                f"history_length: {history_length})"
            )

            # 1. 오디오 데이터 준비
            audio_bytes = voice_input.data
            logger.debug(f"Audio prepared: {len(audio_bytes)} bytes")

            # 2. 시스템 프롬프트 구성
            instructions = self.default_system_prompt

            # 이전 대화 이력 추가 (멀티턴)
            if conversation_history and not conversation_history.is_empty():
                history_text = ""
                for msg in conversation_history:
                    history_text += f"{msg.role.value}: {msg.content}\n"
                instructions = f"{instructions}{history_text}"
                logger.debug(f"Added {len(conversation_history)} messages from history")

            # 3. WebSocket 연결 및 응답 생성
            async with self:
                # 세션 설정
                await self.configure_session(
                    instructions=instructions,
                    modalities=["text"],  # 텍스트 출력만 사용
                    input_audio_transcription={
                        "model": self.transcription_model,
                        "language": language,
                    },
                    temperature=self.temperature,
                )

                # 오디오 처리 및 응답 생성 (부모 클래스 메서드 사용)
                result = await super().generate_response_from_audio(
                    audio_bytes=audio_bytes,
                    modalities=["text"],
                )

            response_text = result.get("response_text", "")

            if not response_text:
                raise ValueError("Empty response from Realtime API")

            logger.info(f"Response generation successful: {response_text[:50]}...")
            return response_text

        except Exception as e:
            logger.error(f"Error in generate_response_from_audio (v2): {e}", exc_info=True)
            raise

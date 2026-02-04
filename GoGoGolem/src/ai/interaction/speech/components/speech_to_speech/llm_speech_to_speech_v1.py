"""
LLM 기반 Speech-to-Speech 컴포넌트 구현 (v1)

OpenAI의 gpt-4o-audio-preview 모델을 사용하여 음성 입력을 직접 이해하고
AI 응답을 생성합니다. 기존 2단계 파이프라인(음성→텍스트→응답)을
단일 단계로 통합하여 레이턴시를 줄입니다.

멀티턴 대화를 지원하여 이전 대화 이력을 기반으로 응답을 생성합니다.
"""

import base64
import logging
from typing import List, Dict, Any, Optional
from litellm import Router

from interaction.speech.domain.ports.speech_to_speech import SpeechToSpeechPort
from interaction.speech.domain.entity.conversation import ConversationHistory
from interaction.speech.domain.entity.voice_input import VoiceInput
from interaction.core.components.llm_components.llm_component import LLMComponent
from interaction.speech.prompts.text_to_text_v2 import SYSTEM_PROMPT, MODEL_CONFIG

logger = logging.getLogger(__name__)


class LLMSpeechToSpeechV1(LLMComponent, SpeechToSpeechPort):
    """
    Speech-to-Speech 구현 (v1)

    OpenAI의 gpt-4o-audio-preview 모델을 사용하여
    음성 입력을 직접 이해하고 AI 응답 텍스트를 생성합니다.
    """

    # 지원하는 오디오 형식
    SUPPORTED_AUDIO_FORMATS = ["wav", "mp3", "flac", "m4a", "ogg", "webm"]

    def __init__(
        self,
        router: Router,
        model: str = "gpt-4o-audio-preview",
    ):
        """
        초기화

        Args:
            router: LiteLLM Router 인스턴스
            model: 사용할 모델명 (기본값: gpt-4o-audio-preview)
        """
        super().__init__(prompt_path="", router=router)
        self.model = model
        self.temperature = MODEL_CONFIG.get("temperature", 0.7)
        self.max_tokens = MODEL_CONFIG.get("max_tokens", 10000)
        logger.info(f"LLMSpeechToSpeechV1 initialized with model: {model}")

    def _detect_audio_format(self, audio_bytes: bytes) -> str:
        """
        오디오 데이터의 형식을 감지

        Args:
            audio_bytes: 오디오 데이터

        Returns:
            오디오 형식 문자열 (예: "wav", "mp3")
        """
        # 매직 바이트로 형식 감지
        if audio_bytes[:4] == b"RIFF" and audio_bytes[8:12] == b"WAVE":
            return "wav"
        elif audio_bytes[:3] == b"ID3" or audio_bytes[:2] == b"\xff\xfb":
            return "mp3"
        elif audio_bytes[:4] == b"fLaC":
            return "flac"
        elif audio_bytes[4:8] == b"ftyp":
            return "m4a"
        elif audio_bytes[:4] == b"OggS":
            return "ogg"
        elif audio_bytes[:4] == b"\x1a\x45\xdf\xa3":
            return "webm"
        else:
            # 기본값으로 wav 사용
            logger.warning("Could not detect audio format, assuming wav")
            return "wav"

    def _build_audio_message(
        self, audio_bytes: bytes, audio_format: str
    ) -> Dict[str, Any]:
        """
        OpenAI Audio API 형식의 메시지 구성

        Args:
            audio_bytes: 오디오 데이터
            audio_format: 오디오 형식

        Returns:
            API 메시지 딕셔너리
        """
        # Base64 인코딩
        audio_base64 = base64.b64encode(audio_bytes).decode("utf-8")

        # OpenAI audio input 형식
        return {
            "role": "user",
            "content": [
                {
                    "type": "input_audio",
                    "input_audio": {
                        "data": audio_base64,
                        "format": audio_format,
                    },
                }
            ],
        }

    async def generate_response_from_audio(
        self,
        voice_input: VoiceInput,
        conversation_history: Optional[ConversationHistory] = None,
        language: str = "ko",
    ) -> str:
        """
        사용자 오디오로부터 AI 응답을 직접 생성

        Args:
            voice_input: 음성 입력 데이터
            conversation_history: 이전 대화 이력 (멀티턴 대화 지원)
            language: 오디오 언어 코드 (기본값: "ko" - 한국어)

        Returns:
            AI가 생성한 응답 텍스트 (str)
        """
        try:
            is_first_turn = (
                conversation_history is None or conversation_history.is_empty()
            )
            logger.info(
                f"Generating response from audio (language: {language}, "
                f"first_turn={is_first_turn}, "
                f"history_length={0 if is_first_turn else len(conversation_history)})"
            )

            # 1. 오디오 데이터 준비
            audio_bytes = voice_input.data
            audio_format = voice_input.format or self._detect_audio_format(audio_bytes)
            logger.debug(f"Audio format detected: {audio_format}")

            # 2. 메시지 구성
            messages: List[Dict[str, Any]] = [
                {"role": "system", "content": SYSTEM_PROMPT},
            ]

            # 이전 대화 이력 추가 (멀티턴)
            if conversation_history and not conversation_history.is_empty():
                for msg in conversation_history:
                    messages.append(msg.to_dict())
                logger.debug(f"Added {len(conversation_history)} messages from history")

            # 현재 사용자 오디오 메시지 추가
            messages.append(self._build_audio_message(audio_bytes, audio_format))

            # 3. API 호출 (text 출력만 사용)
            logger.info(
                f"Calling LLM with model: {self.model}, total messages: {len(messages)}"
            )
            response = await self.router.acompletion(
                model=self.model,
                messages=messages,
                modalities=["text"],  # 텍스트 출력만 사용 (음성 출력 비활성화)
                temperature=self.temperature,
                max_tokens=self.max_tokens,
            )

            # 4. 응답에서 텍스트 추출
            response_text = self.parse_content(response)

            if not response_text:
                raise ValueError("Empty response from LLM")

            logger.info(f"Response generation successful: {response_text[:50]}...")
            return response_text

        except Exception as e:
            logger.error(f"Error in generate_response_from_audio: {e}", exc_info=True)
            raise

"""
LLM 기반 Text-to-Text 컴포넌트 구현 (v1)

LLMComponent를 사용하여 텍스트로부터 응답을 생성합니다.
멀티턴 대화를 지원하여 이전 대화 이력을 기반으로 응답을 생성합니다.
"""

import logging
from typing import Optional, List, Dict, Any
from litellm import Router

from interaction.speech.prompts.text_to_text_v2 import SYSTEM_PROMPT, MODEL_CONFIG
from interaction.speech.domain.ports.text_to_text import TextToTextPort
from interaction.speech.domain.entity.conversation import ConversationHistory
from interaction.core.components.llm_components.llm_component import LLMComponent

logger = logging.getLogger(__name__)


class LLMTextToTextV1(LLMComponent, TextToTextPort):
    """LLM을 사용한 Text-to-Text 구현 (멀티턴 대화 지원)"""

    def __init__(self, router: Router):
        """
        초기화

        Args:
            router: LiteLLM Router 인스턴스
        """
        super().__init__(prompt_path="", router=router)
        logger.info("LLMTextToTextV1 initialized")

    async def create_response_from_user_audio_text(
        self,
        text: str,
        conversation_history: Optional[ConversationHistory] = None,
    ) -> str:
        """
        사용자 오디오에서 변환된 텍스트로부터 응답 생성

        Args:
            text: 사용자 오디오에서 변환된 텍스트
            conversation_history: 이전 대화 이력 (멀티턴 대화 지원)

        Returns:
            생성된 응답 텍스트 (str)
        """
        try:
            is_first_turn = (
                conversation_history is None or conversation_history.is_empty()
            )
            logger.info(
                f"Generating response from text: {text[:50]}... "
                f"(first_turn={is_first_turn}, "
                f"history_length={0 if is_first_turn else len(conversation_history)})"
            )

            # 메시지 구성
            messages: List[Dict[str, Any]] = [
                {
                    "role": "system",
                    "content": SYSTEM_PROMPT,
                },
            ]

            # 이전 대화 이력 추가 (멀티턴)
            if conversation_history and not conversation_history.is_empty():
                for msg in conversation_history:
                    messages.append(msg.to_dict())
                logger.debug(f"Added {len(conversation_history)} messages from history")

            # 현재 사용자 메시지 추가
            messages.append(
                {
                    "role": "user",
                    "content": text,
                }
            )

            model = MODEL_CONFIG["model"]
            temperature = MODEL_CONFIG["temperature"]
            max_tokens = MODEL_CONFIG["max_tokens"]

            # LLM 호출
            logger.info(
                f"Calling LLM with model: {model}, total messages: {len(messages)}"
            )
            llm_response = await self.call_llm(
                model=model,
                messages=messages,
                temperature=temperature,
                max_tokens=max_tokens,
            )

            # LLM 응답에서 텍스트 추출
            response_text = self.parse_content(llm_response)

            logger.info(f"Response generation successful: {response_text[:50]}...")
            return response_text

        except Exception as e:
            logger.error(
                f"Error in create_response_from_user_audio_text: {e}", exc_info=True
            )
            raise

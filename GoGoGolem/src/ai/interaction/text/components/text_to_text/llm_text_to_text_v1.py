"""
LLM 기반 Text-to-Text 컴포넌트 구현 (v1)

LLMComponent를 사용하여 편지 응답을 생성합니다.
"""

import logging
from litellm import Router

from interaction.text.prompts.text_to_text_v1 import SYSTEM_PROMPT, MODEL_CONFIG
from interaction.text.domain.ports.text_to_text import TextToTextPort
from interaction.core.components.llm_components.llm_component import LLMComponent

logger = logging.getLogger(__name__)


class LLMTextToTextV1(LLMComponent, TextToTextPort):
    """LLM을 사용한 Text-to-Text 편지 응답 생성 구현"""

    def __init__(self, router: Router):
        """
        초기화

        Args:
            router: LiteLLM Router 인스턴스
        """
        super().__init__(prompt_path="", router=router)
        logger.info("LLMTextToTextV1 (Letter) initialized")

    async def generate_letter_content_from_user_input(self, text: str) -> str:
        """
        사용자가 작성한 편지로부터 부모 캐릭터의 응답 생성

        Args:
            text: 사용자(아이)가 작성한 편지 내용

        Returns:
            생성된 부모 캐릭터의 편지 응답 (str)
        """
        try:
            logger.info(f"Generating letter response from user input: {text[:50]}...")

            messages = [
                {
                    "role": "system",
                    "content": SYSTEM_PROMPT,
                },
                {
                    "role": "user",
                    "content": text,
                },
            ]

            model = MODEL_CONFIG["model"]
            temperature = MODEL_CONFIG["temperature"]
            max_tokens = MODEL_CONFIG["max_tokens"]

            llm_response = await self.call_llm(
                model=model,
                messages=messages,
                temperature=temperature,
                max_tokens=max_tokens,
            )

            response_text = self.parse_content(llm_response)

            logger.info("Letter response generation successful")
            return response_text

        except Exception as e:
            logger.error(
                f"Error in generate_letter_content_from_user_input: {e}", exc_info=True
            )
            raise

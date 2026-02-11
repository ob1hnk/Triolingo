"""
Generate Letter Response UseCase

사용자의 편지 입력을 받아 부모 캐릭터의 응답을 생성하고 Firebase에 저장합니다.
"""

import logging

from interaction.text.domain.entity.letter import Letter
from interaction.text.domain.ports.text_to_text import TextToTextPort
from interaction.text.domain.repository.letter_response import (
    LetterResponseRepositoryPort,
)

logger = logging.getLogger(__name__)


class GenerateLetterResponseUseCase:
    """
    편지 응답 생성 UseCase

    사용자가 작성한 편지를 받아 부모 캐릭터의 응답을 생성하고,
    Firebase에 저장한 후 결과를 반환합니다.
    """

    def __init__(
        self,
        text_to_text: TextToTextPort,
        letter_repository: LetterResponseRepositoryPort,
    ):
        """
        초기화

        Args:
            text_to_text: 텍스트 응답 생성 포트
            letter_repository: 편지 저장소 포트
        """
        self.text_to_text = text_to_text
        self.letter_repository = letter_repository
        logger.info("GenerateLetterResponseUseCase initialized")

    async def execute(
        self,
        input_data: dict,
    ) -> dict:
        """
        편지 응답 생성 실행

        Args:
            input_data: 사용자 ID와 편지 내용이 포함된 입력 데이터

        Returns:
            생성된 편지 ID, 원본 편지, 부모 응답이 포함된 출력 데이터
        """
        try:
            logger.info(f"Generating letter response for user: {input_data['user_id']}")

            # Step 1: LLM을 사용하여 부모 캐릭터의 응답 생성
            generated_response_letter = (
                await self.text_to_text.generate_letter_content_from_user_input(
                    text=input_data["user_letter"]
                )
            )

            logger.info("Parent response generated successfully")

            # Step 2: Firebase에 편지와 응답 저장
            letter_response = Letter(
                task_id=input_data.get("task_id", ""),
                user_id=input_data["user_id"],
                user_letter=input_data["user_letter"],
                generated_response_letter=generated_response_letter,
            )

            letter_id = await self.letter_repository.save(letter_response)

            logger.info(f"Letter response saved with ID: {letter_id}")

            return {
                "letter_id": letter_id,
                "user_letter": input_data["user_letter"],
                "generated_response_letter": generated_response_letter,
            }

        except Exception as e:
            logger.error(f"Error in GenerateLetterResponseUseCase: {e}", exc_info=True)
            raise

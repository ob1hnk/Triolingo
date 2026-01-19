"""
텍스트 처리 API 라우터 (v1)

편지 응답 생성 API 엔드포인트
"""

import logging

from fastapi import APIRouter, HTTPException

from interaction.server.dto.text import (
    GenerateLetterRequest,
    GenerateLetterResponse,
)
from interaction.text.di.container import TextContainer

logger = logging.getLogger(__name__)

router = APIRouter()


@router.post(
    "/letter/generate",
    response_model=GenerateLetterResponse,
    summary="편지 응답 생성",
    description="사용자의 편지를 받아 부모 캐릭터의 응답을 생성합니다.",
)
async def generate_letter_response(request: GenerateLetterRequest):
    """
    편지 응답 생성 API

    사용자가 작성한 편지를 받아서 부모 캐릭터의 응답을 LLM으로 생성하고,
    Firebase에 저장한 후 결과를 반환합니다.
    """
    import interaction.server.app as app_module

    app = app_module.app

    text_container: TextContainer = app.state.text_container

    usecase = text_container.generate_letter_response_usecase()

    try:
        logger.info(f"Generating letter response for user: {request.user_id}")

        result = await usecase.execute(
            {
                "user_id": request.user_id,
                "user_letter": request.user_letter,
            }
        )

        logger.info(f"Letter response generated: {result['letter_id']}")

        return GenerateLetterResponse(
            letter_id=result["letter_id"],
            user_letter=result["user_letter"],
            generated_response_letter=result["generated_response_letter"],
        )

    except Exception as e:
        logger.error(f"Error generating letter response: {e}", exc_info=True)
        raise HTTPException(
            status_code=500,
            detail=f"Failed to generate letter response: {str(e)}",
        )

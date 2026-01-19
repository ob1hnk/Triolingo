"""
텍스트 처리 API 라우터 (v1)

편지 응답 생성 API 엔드포인트 (비동기 Job Queue 패턴)
"""

import asyncio
import logging
import uuid

from fastapi import APIRouter, HTTPException, status

from interaction.server.dto.text import (
    GenerateLetterRequest,
    GenerateLetterResponse,
)
from interaction.text.di.container import TextContainer

logger = logging.getLogger(__name__)

router = APIRouter()


@router.post(
    "/generate-letter",
    response_model=GenerateLetterResponse,
    status_code=status.HTTP_202_ACCEPTED,
    summary="편지 응답 생성 (비동기)",
    description="사용자의 편지를 받아 백그라운드에서 부모 캐릭터의 응답을 생성합니다.",
)
async def generate_letter(request: GenerateLetterRequest):
    """
    편지 응답 생성 API (비동기)

    요청을 받으면 accepted를 반환하고,
    백그라운드에서 usecase가 LLM 응답을 생성하여 Firebase에 저장합니다.
    """
    import interaction.server.app as app_module

    app = app_module.app

    text_container: TextContainer = app.state.text_container
    usecase = text_container.generate_letter_response_usecase()

    task_id = str(uuid.uuid4())

    try:
        logger.info(
            f"Received letter generation request for user: {request.user_id}, task: {task_id}"
        )

        # 백그라운드에서 usecase 실행
        asyncio.create_task(
            usecase.execute(
                {
                    "user_id": request.user_id,
                    "user_letter": request.user_letter,
                }
            )
        )

        return GenerateLetterResponse(
            status="accepted",
            task_id=task_id,
        )

    except Exception as e:
        logger.error(f"Error creating letter task: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to create letter task: {str(e)}",
        )

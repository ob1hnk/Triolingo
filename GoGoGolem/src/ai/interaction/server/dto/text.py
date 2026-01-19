"""
텍스트 처리 API를 위한 DTO 정의

편지 응답 생성 API의 요청/응답 모델
"""

from pydantic import BaseModel, Field


class GenerateLetterRequest(BaseModel):
    """편지 응답 생성 요청"""

    user_id: str = Field(..., description="사용자 ID")
    user_letter: str = Field(..., description="사용자가 작성한 편지 내용")


class GenerateLetterResponse(BaseModel):
    """편지 응답 생성 결과"""

    letter_id: str = Field(..., description="생성된 편지 ID")
    user_letter: str = Field(..., description="원본 사용자 편지")
    generated_response_letter: str = Field(..., description="생성된 부모 응답")

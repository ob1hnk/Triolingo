"""
음성 처리 WebSocket 통신을 위한 DTO 정의

Unity에서 VAD로 세션별로 쪼갠 음성을 WebSocket으로 전송하는 프로토콜
"""

from enum import Enum
from typing import Optional
from pydantic import BaseModel, Field


class MessageType(str, Enum):
    """WebSocket 메시지 타입"""

    # 클라이언트 -> 서버
    SESSION_START = "session_start"  # 세션 시작
    AUDIO_CHUNK = "audio_chunk"  # 오디오 청크 전송
    SESSION_END = "session_end"  # 세션 종료

    # 서버 -> 클라이언트
    ACK = "ack"  # 수신 확인
    PROCESSING = "processing"  # 처리 중
    RESULT = "result"  # 최종 결과
    ERROR = "error"  # 에러 발생


class SessionStartRequest(BaseModel):
    """세션 시작 요청"""

    type: MessageType = MessageType.SESSION_START
    trace_id: str = Field(..., description="분산 추적용 Trace ID")
    session_id: str = Field(..., description="고유 세션 ID")
    audio_format: Optional[str] = Field(
        default="wav", description="오디오 포맷 (wav, mp3, ogg 등)"
    )
    sample_rate: Optional[int] = Field(default=16000, description="샘플 레이트 (Hz)")
    channels: Optional[int] = Field(default=1, description="채널 수 (1=mono, 2=stereo)")


class AudioChunkRequest(BaseModel):
    """오디오 청크 전송 요청"""

    type: MessageType = MessageType.AUDIO_CHUNK
    trace_id: str = Field(..., description="분산 추적용 Trace ID")
    session_id: str = Field(..., description="세션 ID")
    chunk_index: int = Field(..., description="청크 인덱스 (0부터 시작)")
    audio_data: str = Field(..., description="Base64로 인코딩된 오디오 데이터")
    is_last_chunk: bool = Field(default=False, description="마지막 청크 여부")


class SessionEndRequest(BaseModel):
    """세션 종료 요청"""

    type: MessageType = MessageType.SESSION_END
    trace_id: str = Field(..., description="분산 추적용 Trace ID")
    session_id: str = Field(..., description="세션 ID")


class AckResponse(BaseModel):
    """수신 확인 응답"""

    type: MessageType = MessageType.ACK
    session_id: str
    message: str = "Received"
    chunk_index: Optional[int] = None  # AUDIO_CHUNK에 대한 ACK인 경우


class ProcessingResponse(BaseModel):
    """처리 중 응답"""

    type: MessageType = MessageType.PROCESSING
    session_id: str
    status: str = "Processing audio..."
    progress: Optional[float] = Field(
        None, ge=0.0, le=1.0, description="처리 진행률 (0.0 ~ 1.0)"
    )


class ResultResponse(BaseModel):
    """최종 결과 응답"""

    type: MessageType = MessageType.RESULT
    session_id: str
    text: str = Field(..., description="생성된 텍스트 응답")
    transcription: Optional[str] = Field(
        None, description="음성 인식 결과 (원본 텍스트)"
    )


class ErrorResponse(BaseModel):
    """에러 응답"""

    type: MessageType = MessageType.ERROR
    session_id: str
    error_code: str
    error_message: str


# Union 타입으로 모든 요청/응답 타입 정의
WebSocketRequest = SessionStartRequest | AudioChunkRequest | SessionEndRequest
WebSocketResponse = AckResponse | ProcessingResponse | ResultResponse | ErrorResponse

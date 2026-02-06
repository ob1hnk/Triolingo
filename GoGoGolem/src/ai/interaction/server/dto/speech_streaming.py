"""
실시간 스트리밍 WebSocket 통신을 위한 DTO

OpenAI Realtime API를 사용한 실시간 음성 처리 프로토콜입니다.
배치 처리용 speech.py와 분리하여 스트리밍 전용으로 사용합니다.
"""

from enum import Enum
from pydantic import BaseModel


class StreamMessageType(str, Enum):
    """스트리밍 WebSocket 메시지 타입"""

    # 클라이언트 -> 서버
    STREAM_START = "stream_start"      # 스트리밍 세션 시작
    STREAM_AUDIO = "stream_audio"      # 오디오 청크 (즉시 전송)
    STREAM_COMMIT = "stream_commit"    # 오디오 입력 완료

    # 서버 -> 클라이언트
    STREAM_ACK = "stream_ack"          # 수신 확인
    TEXT_DELTA = "text_delta"          # 응답 텍스트 델타
    TRANSCRIPT = "transcript"          # 음성 인식 결과
    STREAM_END = "stream_end"          # 스트리밍 세션 종료
    STREAM_ERROR = "stream_error"      # 에러


# ============ Request DTOs ============

class StreamStartRequest(BaseModel):
    """스트리밍 세션 시작 요청"""
    type: StreamMessageType = StreamMessageType.STREAM_START
    session_id: str
    language: str = "ko"


class StreamAudioRequest(BaseModel):
    """오디오 청크 즉시 전송"""
    type: StreamMessageType = StreamMessageType.STREAM_AUDIO
    session_id: str
    audio_data: str  # Base64


class StreamCommitRequest(BaseModel):
    """오디오 입력 완료"""
    type: StreamMessageType = StreamMessageType.STREAM_COMMIT
    session_id: str


# ============ Response DTOs ============

class StreamAckResponse(BaseModel):
    """수신 확인"""
    type: StreamMessageType = StreamMessageType.STREAM_ACK
    session_id: str
    message: str = "OK"


class TextDeltaResponse(BaseModel):
    """응답 텍스트 델타"""
    type: StreamMessageType = StreamMessageType.TEXT_DELTA
    session_id: str
    delta: str


class TranscriptResponse(BaseModel):
    """음성 인식 결과"""
    type: StreamMessageType = StreamMessageType.TRANSCRIPT
    session_id: str
    transcript: str


class StreamEndResponse(BaseModel):
    """스트리밍 종료"""
    type: StreamMessageType = StreamMessageType.STREAM_END
    session_id: str
    full_text: str  # 전체 응답 텍스트


class StreamErrorResponse(BaseModel):
    """에러"""
    type: StreamMessageType = StreamMessageType.STREAM_ERROR
    session_id: str
    error_code: str
    error_message: str


# Union 타입
StreamRequest = StreamStartRequest | StreamAudioRequest | StreamCommitRequest
StreamResponse = StreamAckResponse | TextDeltaResponse | TranscriptResponse | StreamEndResponse | StreamErrorResponse

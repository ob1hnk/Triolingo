"""
Realtime API WebSocket 메시지 타입 정의

Server VAD 방식의 실시간 음성 처리를 위한 메시지 타입입니다.
"""

from enum import Enum

from pydantic import BaseModel


class RealtimeMessageType(str, Enum):
    """Realtime WebSocket 메시지 타입"""

    # Client → Server
    STREAM_START = "STREAM_START"
    STREAM_AUDIO = "STREAM_AUDIO"
    STREAM_STOP = "STREAM_STOP"

    # Server → Client
    STREAM_ACK = "STREAM_ACK"
    SPEECH_STARTED = "SPEECH_STARTED"
    TRANSCRIPT = "TRANSCRIPT"
    TEXT_DELTA = "TEXT_DELTA"
    RESPONSE_END = "RESPONSE_END"
    STREAM_ERROR = "STREAM_ERROR"


# ============ Client to Server Messages ============


class StreamStartRequest(BaseModel):
    """스트리밍 시작 요청"""

    type: str = RealtimeMessageType.STREAM_START
    session_id: str
    language: str = "ko"


class StreamAudioRequest(BaseModel):
    """오디오 청크 전송"""

    type: str = RealtimeMessageType.STREAM_AUDIO
    session_id: str
    audio_data: str  # Base64 encoded


class StreamStopRequest(BaseModel):
    """스트리밍 종료 요청"""

    type: str = RealtimeMessageType.STREAM_STOP
    session_id: str


# ============ Server to Client Messages ============


class StreamAckResponse(BaseModel):
    """수신 확인 응답"""

    type: str = RealtimeMessageType.STREAM_ACK
    session_id: str
    message: str = ""


class SpeechStartedResponse(BaseModel):
    """발화 감지 알림 (Server VAD)"""

    type: str = RealtimeMessageType.SPEECH_STARTED
    session_id: str


class TranscriptResponse(BaseModel):
    """음성 인식 결과"""

    type: str = RealtimeMessageType.TRANSCRIPT
    session_id: str
    transcript: str


class TextDeltaResponse(BaseModel):
    """응답 텍스트 델타 (스트리밍)"""

    type: str = RealtimeMessageType.TEXT_DELTA
    session_id: str
    delta: str


class ResponseEndResponse(BaseModel):
    """응답 완료"""

    type: str = RealtimeMessageType.RESPONSE_END
    session_id: str
    full_text: str


class StreamErrorResponse(BaseModel):
    """에러 응답"""

    type: str = RealtimeMessageType.STREAM_ERROR
    session_id: str
    error_code: str
    error_message: str

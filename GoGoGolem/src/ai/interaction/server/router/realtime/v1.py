"""
Realtime API WebSocket 라우터 (v1) - Server VAD 방식

OpenAI Realtime API를 사용하여 실시간 양방향 음성 처리를 수행합니다.
Server VAD를 활성화하여 OpenAI가 발화/침묵을 자동으로 감지하고 응답을 생성합니다.

"""

import asyncio
import logging
import base64
from typing import Optional

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from interaction.speech.di.container import SpeechContainer
from interaction.speech.components.realtime.llm_realtime_speech_v1 import (
    LLMRealtimeSpeechV1,
)
from interaction.server.dto.realtime import RealtimeMessageType as MessageType

logger = logging.getLogger(__name__)

router = APIRouter()


async def stream_openai_responses(
    client_ws: WebSocket,
    realtime: LLMRealtimeSpeechV1,
    session_id: str,
):
    """
    OpenAI Realtime API 응답을 클라이언트로 스트리밍

    Server VAD가 활성화되어 있으므로:
    - speech_started: 발화 감지
    - speech_stopped: 침묵 감지 → 자동 응답 생성
    - response.text.delta: 텍스트 스트리밍
    - response.done: 응답 완료
    """
    full_text = ""

    try:
        # Server VAD 멀티턴: RESPONSE_DONE에서 중단하지 않고 계속 대기
        async for event in realtime.receive_events(break_on_response_done=False):
            event_type = event.get("type", "")

            # 발화 감지
            if event_type == realtime.EventType.INPUT_AUDIO_BUFFER_SPEECH_STARTED:
                await client_ws.send_json(
                    {
                        "type": MessageType.SPEECH_STARTED,
                        "session_id": session_id,
                    }
                )
                logger.debug(f"Speech started: {session_id}")

            # 음성 인식 결과
            elif (
                event_type
                == realtime.EventType.CONVERSATION_ITEM_TRANSCRIPTION_COMPLETED
            ):
                transcript = event.get("transcript", "")
                if transcript:
                    await client_ws.send_json(
                        {
                            "type": MessageType.TRANSCRIPT,
                            "session_id": session_id,
                            "transcript": transcript,
                        }
                    )
                    logger.debug(f"Transcript: {transcript[:50]}...")

            # 텍스트 델타
            elif event_type == realtime.EventType.RESPONSE_TEXT_DELTA:
                delta = event.get("delta", "")
                if delta:
                    full_text += delta
                    await client_ws.send_json(
                        {
                            "type": MessageType.TEXT_DELTA,
                            "session_id": session_id,
                            "delta": delta,
                        }
                    )

            # 응답 완료
            elif event_type == realtime.EventType.RESPONSE_DONE:
                await client_ws.send_json(
                    {
                        "type": MessageType.RESPONSE_END,
                        "session_id": session_id,
                        "full_text": full_text,
                    }
                )
                logger.info(f"Response completed: {full_text[:50]}...")
                full_text = ""  # 다음 응답을 위해 초기화
                # break 하지 않음 - 멀티턴을 위해 계속 대기

            # 에러 (OpenAI 에러 구조: error.type, error.code, error.message, error.param, error.event_id)
            elif event_type == realtime.EventType.ERROR:
                error_data = event.get("error", {})
                error_type = error_data.get("type", "unknown_error")
                error_code = error_data.get("code", "unknown")
                error_msg = error_data.get("message", "Unknown error")
                error_param = error_data.get("param")
                error_event_id = error_data.get("event_id")

                # 상세 로깅
                logger.error(
                    f"OpenAI error: type={error_type}, code={error_code}, "
                    f"message={error_msg}, param={error_param}, event_id={error_event_id}"
                )

                await client_ws.send_json(
                    {
                        "type": MessageType.STREAM_ERROR,
                        "session_id": session_id,
                        "error_code": f"{error_type}:{error_code}",
                        "error_message": error_msg,
                    }
                )
                break

    except asyncio.CancelledError:
        logger.info(f"Response streaming cancelled: {session_id}")
    except Exception as e:
        logger.error(f"Error streaming responses: {e}", exc_info=True)
        try:
            await client_ws.send_json(
                {
                    "type": MessageType.STREAM_ERROR,
                    "session_id": session_id,
                    "error_code": "STREAM_ERROR",
                    "error_message": str(e),
                }
            )
        except Exception:
            pass


async def handle_realtime_websocket(
    websocket: WebSocket,
    realtime_factory,
):
    """
    WebSocket 연결을 처리하는 핸들러 (Server VAD 방식)

    서버가 프록시 역할을 하여:
    1. 클라이언트 → 서버 → OpenAI (오디오 전달)
    2. OpenAI → 서버 → 클라이언트 (응답 스트리밍)

    Server VAD가 활성화되어 있으므로 클라이언트는 COMMIT을 보내지 않습니다.
    OpenAI가 자동으로 발화/침묵을 감지하고 응답을 생성합니다.
    """
    await websocket.accept()
    logger.info("WebSocket connection accepted (Realtime v1 - Server VAD)")

    realtime: Optional[LLMRealtimeSpeechV1] = None
    session_id: Optional[str] = None
    response_task: Optional[asyncio.Task] = None

    try:
        while True:
            data = await websocket.receive_json()
            message_type = data.get("type")

            try:
                if message_type == MessageType.STREAM_START:
                    session_id = data.get("session_id", "unknown")
                    language = data.get("language", "ko")

                    logger.info(f"Stream starting: {session_id} (language: {language})")

                    # 1. Realtime 컴포넌트 생성 (Factory)
                    realtime = realtime_factory()

                    # 2. OpenAI Realtime API 연결
                    await realtime.connect()

                    # 3. Server VAD 활성화된 세션 설정
                    await realtime.configure_session(
                        instructions=realtime.default_system_prompt,
                        modalities=["text"],
                        input_audio_transcription={
                            "model": realtime.transcription_model,
                            "language": language,
                        },
                        turn_detection={
                            "type": "server_vad",
                            "threshold": 0.5,
                            "prefix_padding_ms": 300,
                            "silence_duration_ms": 500,
                        },
                        temperature=realtime.temperature,
                    )

                    # 4. 응답 스트리밍 태스크 시작 (백그라운드)
                    response_task = asyncio.create_task(
                        stream_openai_responses(websocket, realtime, session_id)
                    )

                    # 5. ACK 응답
                    await websocket.send_json(
                        {
                            "type": MessageType.STREAM_ACK,
                            "session_id": session_id,
                            "message": "Stream started - OpenAI connected with Server VAD",
                        }
                    )
                    logger.info(
                        f"OpenAI Realtime connected with Server VAD: {session_id}"
                    )

                elif message_type == MessageType.STREAM_AUDIO:
                    if not realtime:
                        await websocket.send_json(
                            {
                                "type": MessageType.STREAM_ERROR,
                                "session_id": data.get("session_id", "unknown"),
                                "error_code": "NOT_STARTED",
                                "error_message": "Stream not started. Send STREAM_START first.",
                            }
                        )
                        continue

                    # Base64 디코딩
                    audio_data = data.get("audio_data", "")
                    try:
                        audio_bytes = base64.b64decode(audio_data)
                    except Exception as e:
                        await websocket.send_json(
                            {
                                "type": MessageType.STREAM_ERROR,
                                "session_id": session_id,
                                "error_code": "DECODE_ERROR",
                                "error_message": f"Failed to decode audio: {str(e)}",
                            }
                        )
                        continue

                    # 즉시 OpenAI로 전달 (COMMIT 없음 - Server VAD가 자동 감지)
                    await realtime.send_audio(audio_bytes)
                    # ACK 생략 (성능 최적화 - 오디오 청크마다 ACK 불필요)

                elif message_type == MessageType.STREAM_STOP:
                    logger.info(f"Stream stop requested: {session_id}")
                    break

                else:
                    await websocket.send_json(
                        {
                            "type": MessageType.STREAM_ERROR,
                            "session_id": session_id or "unknown",
                            "error_code": "UNKNOWN_TYPE",
                            "error_message": f"Unknown message type: {message_type}",
                        }
                    )

            except ValueError as e:
                await websocket.send_json(
                    {
                        "type": MessageType.STREAM_ERROR,
                        "session_id": session_id or "unknown",
                        "error_code": "VALIDATION_ERROR",
                        "error_message": str(e),
                    }
                )

    except WebSocketDisconnect:
        logger.info(f"WebSocket disconnected: {session_id}")

    except Exception as e:
        logger.error(f"WebSocket error: {e}", exc_info=True)

    finally:
        # 정리
        if response_task and not response_task.done():
            response_task.cancel()
            try:
                await response_task
            except asyncio.CancelledError:
                pass
        if realtime:
            await realtime.close()
            logger.info(f"OpenAI Realtime connection closed: {session_id}")


@router.websocket("/ws/realtime/v1")
async def websocket_realtime_v1(websocket: WebSocket):
    """
    실시간 음성 처리 WebSocket 엔드포인트 (v1 - Server VAD)

    OpenAI Realtime API를 사용하여 실시간 양방향 스트리밍을 수행합니다.
    Server VAD가 활성화되어 OpenAI가 자동으로 발화/침묵을 감지합니다.

    프로토콜:
    1. STREAM_START: 스트리밍 시작, OpenAI 연결 (Server VAD 활성화)
    2. STREAM_AUDIO: 오디오 청크 즉시 전달 (여러 번, COMMIT 불필요)
    3. STREAM_STOP: 스트리밍 종료

    응답:
    - STREAM_ACK: 수신 확인
    - SPEECH_STARTED: 발화 감지됨 (OpenAI VAD)
    - TRANSCRIPT: 음성 인식 결과
    - TEXT_DELTA: 응답 텍스트 델타 (실시간)
    - RESPONSE_END: 응답 완료, 전체 텍스트
    - STREAM_ERROR: 에러

    Server VAD 동작:
    - 클라이언트는 오디오만 계속 전송
    - OpenAI가 발화 시작/종료를 자동 감지
    - 침묵 감지 시 자동으로 응답 생성
    - 멀티턴 대화 컨텍스트 자동 유지
    """
    import interaction.server.app as app_module

    app = app_module.app
    speech_container: SpeechContainer = app.state.speech_container

    # Factory에서 컴포넌트 생성 함수 전달
    await handle_realtime_websocket(
        websocket,
        realtime_factory=speech_container.realtime_speech_v1,
    )


@router.get("/realtime/v1/status")
async def realtime_status():
    """
    Realtime API 상태 확인 엔드포인트
    """
    return {
        "status": "active",
        "version": "v1",
        "mode": "server_vad",
        "endpoint": "/ws/realtime/v1",
        "features": {
            "server_vad": True,
            "auto_response": True,
            "multi_turn": True,
            "transcription": True,
        },
        "protocol": {
            "client_messages": ["STREAM_START", "STREAM_AUDIO", "STREAM_STOP"],
            "server_messages": [
                "STREAM_ACK",
                "SPEECH_STARTED",
                "TRANSCRIPT",
                "TEXT_DELTA",
                "RESPONSE_END",
                "STREAM_ERROR",
            ],
        },
    }

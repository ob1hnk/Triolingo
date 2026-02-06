"""
음성 처리 WebSocket 라우터 (v3) - 실시간 스트리밍

OpenAI Realtime API를 사용하여 실시간 스트리밍 음성 처리를 수행합니다.
서버가 프록시 역할을 하여 클라이언트와 OpenAI 간 양방향 중계를 수행합니다.

프로토콜:
- STREAM_START: OpenAI Realtime 세션 연결
- STREAM_AUDIO: 오디오 청크 즉시 OpenAI로 전달
- STREAM_COMMIT: 오디오 입력 완료, 응답 요청
- TEXT_DELTA: 응답 텍스트 델타 (실시간)
- STREAM_END: 스트리밍 종료
"""

import asyncio
import logging
import base64
from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from pydantic import ValidationError

from interaction.server.dto.speech_streaming import (
    StreamMessageType,
    StreamStartRequest,
    StreamAudioRequest,
    StreamCommitRequest,
    StreamAckResponse,
    TextDeltaResponse,
    TranscriptResponse,
    StreamEndResponse,
    StreamErrorResponse,
)
from interaction.speech.di.container import SpeechContainer
from interaction.speech.components.speech_to_speech.llm_speech_to_speech_v2 import (
    LLMSpeechToSpeechV2,
)

logger = logging.getLogger(__name__)

router = APIRouter()


async def stream_openai_responses(
    client_ws: WebSocket,
    realtime: LLMSpeechToSpeechV2,
    session_id: str,
):
    """
    OpenAI Realtime API 응답을 클라이언트로 스트리밍

    OpenAI에서 오는 이벤트를 실시간으로 클라이언트에게 전달합니다.
    """
    full_text = ""

    try:
        async for event in realtime.receive_events():
            event_type = event.get("type", "")

            # 음성 인식 결과
            if event_type == realtime.EventType.CONVERSATION_ITEM_TRANSCRIPTION_COMPLETED:
                transcript = event.get("transcript", "")
                if transcript:
                    await client_ws.send_json(
                        TranscriptResponse(
                            type=StreamMessageType.TRANSCRIPT,
                            session_id=session_id,
                            transcript=transcript,
                        ).model_dump()
                    )
                    logger.debug(f"Transcript sent: {transcript[:50]}...")

            # 텍스트 델타
            elif event_type == realtime.EventType.RESPONSE_TEXT_DELTA:
                delta = event.get("delta", "")
                if delta:
                    full_text += delta
                    await client_ws.send_json(
                        TextDeltaResponse(
                            type=StreamMessageType.TEXT_DELTA,
                            session_id=session_id,
                            delta=delta,
                        ).model_dump()
                    )

            # 응답 완료
            elif event_type == realtime.EventType.RESPONSE_DONE:
                await client_ws.send_json(
                    StreamEndResponse(
                        type=StreamMessageType.STREAM_END,
                        session_id=session_id,
                        full_text=full_text,
                    ).model_dump()
                )
                logger.info(f"Stream completed: {full_text[:50]}...")
                break

            # 에러
            elif event_type == realtime.EventType.ERROR:
                error_msg = event.get("error", {}).get("message", "Unknown error")
                await client_ws.send_json(
                    StreamErrorResponse(
                        type=StreamMessageType.STREAM_ERROR,
                        session_id=session_id,
                        error_code="OPENAI_ERROR",
                        error_message=error_msg,
                    ).model_dump()
                )
                logger.error(f"OpenAI error: {error_msg}")
                break

    except Exception as e:
        logger.error(f"Error streaming responses: {e}", exc_info=True)
        try:
            await client_ws.send_json(
                StreamErrorResponse(
                    type=StreamMessageType.STREAM_ERROR,
                    session_id=session_id,
                    error_code="STREAM_ERROR",
                    error_message=str(e),
                ).model_dump()
            )
        except Exception:
            pass


async def handle_websocket_v3_streaming(
    websocket: WebSocket,
    realtime_factory,
):
    """
    WebSocket 연결을 처리하는 핸들러 (v3 - 실시간 스트리밍)

    서버가 프록시 역할을 하여:
    1. 클라이언트 → 서버 → OpenAI (오디오 전달)
    2. OpenAI → 서버 → 클라이언트 (응답 스트리밍)
    """
    await websocket.accept()
    logger.info("WebSocket connection accepted (v3 - Realtime Streaming)")

    realtime: LLMSpeechToSpeechV2 | None = None
    session_id: str | None = None
    response_task: asyncio.Task | None = None

    try:
        while True:
            data = await websocket.receive_json()
            message_type = data.get("type")

            try:
                # ============ STREAM_START ============
                if message_type == StreamMessageType.STREAM_START:
                    request = StreamStartRequest(**data)
                    session_id = request.session_id

                    logger.info(f"Stream starting: {session_id}")

                    # 1. Realtime 컴포넌트 생성 (Factory)
                    realtime = realtime_factory()

                    # 2. OpenAI Realtime API 연결
                    await realtime.connect()

                    # 3. 세션 설정
                    await realtime.configure_session(
                        instructions=realtime.default_system_prompt,
                        modalities=["text"],
                        input_audio_transcription={
                            "model": realtime.transcription_model,
                            "language": request.language,
                        },
                        temperature=realtime.temperature,
                    )

                    # 4. ACK 응답
                    await websocket.send_json(
                        StreamAckResponse(
                            type=StreamMessageType.STREAM_ACK,
                            session_id=session_id,
                            message="Stream started - OpenAI connected",
                        ).model_dump()
                    )
                    logger.info(f"OpenAI Realtime connected: {session_id}")

                # ============ STREAM_AUDIO ============
                elif message_type == StreamMessageType.STREAM_AUDIO:
                    request = StreamAudioRequest(**data)

                    if not realtime:
                        await websocket.send_json(
                            StreamErrorResponse(
                                type=StreamMessageType.STREAM_ERROR,
                                session_id=request.session_id,
                                error_code="NOT_STARTED",
                                error_message="Stream not started. Send STREAM_START first.",
                            ).model_dump()
                        )
                        continue

                    # Base64 디코딩
                    try:
                        audio_bytes = base64.b64decode(request.audio_data)
                    except Exception as e:
                        await websocket.send_json(
                            StreamErrorResponse(
                                type=StreamMessageType.STREAM_ERROR,
                                session_id=request.session_id,
                                error_code="DECODE_ERROR",
                                error_message=f"Failed to decode audio: {str(e)}",
                            ).model_dump()
                        )
                        continue

                    # 즉시 OpenAI로 전달
                    await realtime.send_audio(audio_bytes)

                    # ACK
                    await websocket.send_json(
                        StreamAckResponse(
                            type=StreamMessageType.STREAM_ACK,
                            session_id=request.session_id,
                            message="Audio forwarded",
                        ).model_dump()
                    )

                # ============ STREAM_COMMIT ============
                elif message_type == StreamMessageType.STREAM_COMMIT:
                    request = StreamCommitRequest(**data)

                    if not realtime:
                        await websocket.send_json(
                            StreamErrorResponse(
                                type=StreamMessageType.STREAM_ERROR,
                                session_id=request.session_id,
                                error_code="NOT_STARTED",
                                error_message="Stream not started.",
                            ).model_dump()
                        )
                        continue

                    # 오디오 커밋 및 응답 요청
                    await realtime.commit_audio()
                    await realtime.request_response(modalities=["text"])

                    # ACK
                    await websocket.send_json(
                        StreamAckResponse(
                            type=StreamMessageType.STREAM_ACK,
                            session_id=request.session_id,
                            message="Audio committed - generating response",
                        ).model_dump()
                    )

                    # 응답 스트리밍 시작 (백그라운드 태스크)
                    response_task = asyncio.create_task(
                        stream_openai_responses(websocket, realtime, request.session_id)
                    )

                else:
                    await websocket.send_json(
                        StreamErrorResponse(
                            type=StreamMessageType.STREAM_ERROR,
                            session_id=session_id or "unknown",
                            error_code="UNKNOWN_TYPE",
                            error_message=f"Unknown message type: {message_type}",
                        ).model_dump()
                    )

            except ValidationError as e:
                await websocket.send_json(
                    StreamErrorResponse(
                        type=StreamMessageType.STREAM_ERROR,
                        session_id=session_id or "unknown",
                        error_code="VALIDATION_ERROR",
                        error_message=str(e),
                    ).model_dump()
                )

    except WebSocketDisconnect:
        logger.info(f"WebSocket disconnected: {session_id}")

    except Exception as e:
        logger.error(f"WebSocket error: {e}", exc_info=True)

    finally:
        # 정리
        if response_task and not response_task.done():
            response_task.cancel()
        if realtime:
            await realtime.close()
            logger.info(f"OpenAI Realtime connection closed: {session_id}")


@router.websocket("/ws/speech/v3")
async def websocket_speech_v3(websocket: WebSocket):
    """
    음성 처리 WebSocket 엔드포인트 (v3 - 실시간 스트리밍)

    OpenAI Realtime API를 사용하여 실시간 양방향 스트리밍을 수행합니다.
    서버가 프록시 역할을 하여 클라이언트와 OpenAI 간 중계합니다.

    프로토콜:
    1. STREAM_START: 스트리밍 시작, OpenAI 연결
    2. STREAM_AUDIO: 오디오 청크 즉시 전달 (여러 번)
    3. STREAM_COMMIT: 오디오 입력 완료, 응답 생성 시작

    응답:
    - STREAM_ACK: 수신 확인
    - TEXT_DELTA: 응답 텍스트 델타 (실시간)
    - TRANSCRIPT: 음성 인식 결과
    - STREAM_END: 스트리밍 종료, 전체 텍스트
    - STREAM_ERROR: 에러
    """
    import interaction.server.app as app_module

    app = app_module.app
    speech_container: SpeechContainer = app.state.speech_container

    # Factory에서 컴포넌트 생성 함수 전달
    await handle_websocket_v3_streaming(
        websocket,
        realtime_factory=speech_container.speech_to_speech_v2,
    )

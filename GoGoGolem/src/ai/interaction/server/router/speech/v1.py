"""
음성 처리 WebSocket 라우터 (v1)

APIRouter를 사용한 버전
Unity에서 VAD로 세션별로 쪼갠 음성을 받아서 처리하는 엔드포인트
"""

import logging
import io
import base64
from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from pydantic import ValidationError

from interaction.server.dto.speech import (
    MessageType,
    SessionStartRequest,
    AudioChunkRequest,
    SessionEndRequest,
    AckResponse,
    ProcessingResponse,
    ResultResponse,
    ErrorResponse,
)
from interaction.speech.domain.usecases.generate_conversation_response import (
    GenerateConversationResponseUseCase,
)
from interaction.speech.di.container import SpeechContainer
from interaction.server.core.session_manager import session_manager

logger = logging.getLogger(__name__)

router = APIRouter()


async def handle_websocket(
    websocket: WebSocket,
    usecase: GenerateConversationResponseUseCase,
):
    """
    WebSocket 연결을 처리하는 핸들러

    프로토콜:
    1. SESSION_START: 세션 시작, session_id와 오디오 포맷 정보 전송
    2. AUDIO_CHUNK: 오디오 청크를 순차적으로 전송 (base64 인코딩)
    3. SESSION_END: 세션 종료, 모든 청크를 합쳐서 처리 시작

    처리 흐름:
    - 세션 시작 시 세션 생성
    - 각 청크를 받을 때마다 ACK 응답
    - 세션 종료 시 모든 청크를 합쳐서 generate_conversation_response 호출
    - 결과를 텍스트로 반환
    """
    await websocket.accept()
    logger.info("WebSocket connection accepted")

    current_session_id: str | None = None

    try:
        while True:
            # 클라이언트로부터 메시지 수신
            data = await websocket.receive_json()
            logger.debug(f"Received message: {data}")

            try:
                # 메시지 타입에 따라 파싱
                message_type = data.get("type")

                if message_type == MessageType.SESSION_START:
                    request = SessionStartRequest(**data)
                    current_session_id = request.session_id

                    # 세션 생성
                    session_manager.create_session(
                        request.session_id,
                        request.audio_format or "wav",
                        request.sample_rate or 16000,
                        request.channels or 1,
                    )

                    # ACK 응답
                    await websocket.send_json(
                        AckResponse(
                            type=MessageType.ACK,
                            session_id=request.session_id,
                            message="Session started",
                        ).model_dump()
                    )
                    logger.info(f"Session started: {request.session_id}")

                elif message_type == MessageType.AUDIO_CHUNK:
                    request = AudioChunkRequest(**data)
                    current_session_id = request.session_id

                    # 세션 존재 확인
                    if not session_manager.has_session(request.session_id):
                        await websocket.send_json(
                            ErrorResponse(
                                type=MessageType.ERROR,
                                session_id=request.session_id,
                                error_code="SESSION_NOT_FOUND",
                                error_message="Session not found. Please start a session first.",
                            ).model_dump()
                        )
                        continue

                    # Base64 디코딩
                    try:
                        audio_bytes = base64.b64decode(request.audio_data)
                    except Exception as e:
                        logger.error(f"Failed to decode audio data: {e}")
                        await websocket.send_json(
                            ErrorResponse(
                                type=MessageType.ERROR,
                                session_id=request.session_id,
                                error_code="DECODE_ERROR",
                                error_message=f"Failed to decode audio data: {str(e)}",
                            ).model_dump()
                        )
                        continue

                    # 청크 저장
                    session_manager.add_chunk(
                        request.session_id, request.chunk_index, audio_bytes
                    )

                    # ACK 응답
                    await websocket.send_json(
                        AckResponse(
                            type=MessageType.ACK,
                            session_id=request.session_id,
                            message="Chunk received",
                            chunk_index=request.chunk_index,
                        ).model_dump()
                    )
                    logger.debug(
                        f"Chunk {request.chunk_index} received for session {request.session_id}"
                    )

                elif message_type == MessageType.SESSION_END:
                    request = SessionEndRequest(**data)
                    current_session_id = request.session_id

                    # 세션 존재 확인
                    if not session_manager.has_session(request.session_id):
                        await websocket.send_json(
                            ErrorResponse(
                                type=MessageType.ERROR,
                                session_id=request.session_id,
                                error_code="SESSION_NOT_FOUND",
                                error_message="Session not found.",
                            ).model_dump()
                        )
                        continue

                    # 처리 시작 알림
                    await websocket.send_json(
                        ProcessingResponse(
                            type=MessageType.PROCESSING,
                            session_id=request.session_id,
                            status="Processing audio and generating response...",
                            progress=0.5,
                        ).model_dump()
                    )

                    try:
                        # 모든 청크를 합쳐서 오디오 파일 생성
                        audio_bytes = session_manager.get_audio(request.session_id)

                        if len(audio_bytes) == 0:
                            raise ValueError("No audio data received")

                        # BytesIO로 변환 (generate_conversation_response가 파일 객체를 받을 수 있도록)
                        audio_file = io.BytesIO(audio_bytes)
                        audio_file.name = f"audio_{request.session_id}.wav"

                        # UseCase 호출 (의존성 주입으로 받은 usecase 사용)
                        result = await usecase.execute(audio_file)

                        # 세션 완료 표시 및 제거
                        session_manager.mark_complete(request.session_id)
                        session_manager.remove_session(request.session_id)

                        # 결과 응답
                        await websocket.send_json(
                            ResultResponse(
                                type=MessageType.RESULT,
                                session_id=request.session_id,
                                text=result.get("response", ""),
                                transcription=result.get("transcription"),
                            ).model_dump()
                        )
                        logger.info(
                            f"Session completed successfully: {request.session_id}"
                        )

                    except Exception as e:
                        logger.error(
                            f"Error processing session {request.session_id}: {e}",
                            exc_info=True,
                        )
                        await websocket.send_json(
                            ErrorResponse(
                                type=MessageType.ERROR,
                                session_id=request.session_id,
                                error_code="PROCESSING_ERROR",
                                error_message=f"Failed to process audio: {str(e)}",
                            ).model_dump()
                        )
                        # 에러 발생 시 세션 제거
                        session_manager.remove_session(request.session_id)

                else:
                    # 알 수 없는 메시지 타입
                    await websocket.send_json(
                        ErrorResponse(
                            type=MessageType.ERROR,
                            session_id=current_session_id or "unknown",
                            error_code="UNKNOWN_MESSAGE_TYPE",
                            error_message=f"Unknown message type: {message_type}",
                        ).model_dump()
                    )

            except ValidationError as e:
                logger.error(f"Validation error: {e}")
                await websocket.send_json(
                    ErrorResponse(
                        type=MessageType.ERROR,
                        session_id=current_session_id or "unknown",
                        error_code="VALIDATION_ERROR",
                        error_message=f"Invalid message format: {str(e)}",
                    ).model_dump()
                )

            except Exception as e:
                logger.error(f"Unexpected error: {e}", exc_info=True)
                await websocket.send_json(
                    ErrorResponse(
                        type=MessageType.ERROR,
                        session_id=current_session_id or "unknown",
                        error_code="INTERNAL_ERROR",
                        error_message=f"Internal server error: {str(e)}",
                    ).model_dump()
                )

    except WebSocketDisconnect:
        logger.info(f"WebSocket disconnected. Session: {current_session_id}")
        # 연결 종료 시 세션 정리
        if current_session_id:
            session_manager.remove_session(current_session_id)

    except Exception as e:
        logger.error(f"WebSocket error: {e}", exc_info=True)
        # 연결 종료 시 세션 정리
        if current_session_id:
            session_manager.remove_session(current_session_id)
        raise


@router.websocket("/ws/speech/v1")
async def websocket_speech_v1(websocket: WebSocket):
    """
    음성 처리 WebSocket 엔드포인트 (v1)

    Unity에서 VAD로 세션별로 쪼갠 음성을 받아서 처리합니다.

    프로토콜:
    1. SESSION_START: 세션 시작
    2. AUDIO_CHUNK: 오디오 청크 전송 (여러 번 가능)
    3. SESSION_END: 세션 종료 및 처리 시작

    응답:
    - ACK: 수신 확인
    - PROCESSING: 처리 중
    - RESULT: 최종 결과 (텍스트 응답)
    - ERROR: 에러 발생
    """
    import interaction.server.app as app_module

    app = app_module.app

    speech_container: SpeechContainer = app.state.speech_container

    # 컨테이너에서 usecase 가져오기
    usecase = speech_container.generate_conversation_response_usecase()

    await handle_websocket(websocket, usecase)

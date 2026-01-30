"""
Realtime API WebSocket 라우터 (v1) - Placeholder

향후 실시간 스트리밍 방식 (방식 B)을 위한 엔드포인트입니다.
현재는 구현이 필요함을 알리는 placeholder입니다.

TODO: 실시간 양방향 오디오 스트리밍 구현
- Unity 클라이언트에서 오디오 청크를 실시간으로 전송
- OpenAI Realtime API로 즉시 포워딩
- AI 응답을 실시간으로 Unity에 스트리밍
"""

import logging
from fastapi import APIRouter, WebSocket, WebSocketDisconnect

logger = logging.getLogger(__name__)

router = APIRouter()


@router.websocket("/ws/realtime/v1")
async def websocket_realtime_v1(websocket: WebSocket):
    """
    실시간 음성 처리 WebSocket 엔드포인트 (v1) - Placeholder

    향후 구현 예정:
    - 실시간 양방향 오디오 스트리밍
    - OpenAI Realtime API와의 WebSocket 브릿지
    - 저지연 음성 대화

    현재 상태:
    - 연결은 받지만 기능 미구현
    - 클라이언트에 "구현 예정" 메시지 전송 후 종료
    """
    await websocket.accept()
    logger.info("Realtime WebSocket connection accepted (v1 - placeholder)")

    try:
        # 구현 예정 메시지 전송
        await websocket.send_json({
            "type": "info",
            "message": "Realtime streaming endpoint is not yet implemented. "
                      "Please use /api/v2/ws/speech/v2 for current functionality.",
            "status": "not_implemented",
        })

        # 클라이언트 메시지 대기 (연결 유지용)
        while True:
            data = await websocket.receive_json()
            logger.debug(f"Received message (placeholder): {data}")

            # 모든 메시지에 대해 미구현 응답
            await websocket.send_json({
                "type": "error",
                "error_code": "NOT_IMPLEMENTED",
                "error_message": "Realtime streaming is not yet implemented. "
                               "This endpoint is a placeholder for future development.",
            })

    except WebSocketDisconnect:
        logger.info("Realtime WebSocket disconnected (v1 - placeholder)")
    except Exception as e:
        logger.error(f"Realtime WebSocket error: {e}", exc_info=True)


@router.get("/realtime/v1/status")
async def realtime_status():
    """
    Realtime API 상태 확인 엔드포인트
    """
    return {
        "status": "placeholder",
        "message": "Realtime streaming endpoint is under development",
        "available_endpoints": {
            "current": "/api/v2/ws/speech/v2",
            "future": "/api/realtime/ws/realtime/v1",
        },
        "features": {
            "implemented": [
                "Session-based audio processing (Method A)",
                "Realtime API integration via LLMSpeechToSpeechV2",
            ],
            "planned": [
                "Real-time bidirectional audio streaming (Method B)",
                "WebSocket bridge to OpenAI Realtime API",
                "Low-latency voice conversation",
            ],
        },
    }

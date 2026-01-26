"""
FastAPI 애플리케이션 메인 파일

음성 처리 WebSocket API를 제공합니다.
"""

import logging
from datetime import datetime, timezone
from dotenv import load_dotenv

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from interaction.core.di.container import CoreContainer
from interaction.core.di.config import CoreConfig
from interaction.server.router.speech.v1 import router as speech_router_v1
from interaction.server.router.speech.v2 import router as speech_router_v2
from interaction.speech.di.container import SpeechContainer
from interaction.core.utils.tracing import setup_tracing

# 환경 변수 로드
load_dotenv(override=True)

# 로깅 설정
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

# OpenTelemetry Tracing 초기화
setup_tracing()


def create_app() -> FastAPI:
    """FastAPI 애플리케이션 생성 및 설정"""

    core_config = CoreConfig()

    core_container = CoreContainer()
    core_container.config.from_pydantic(core_config)

    speech_container = SpeechContainer()
    speech_container.core_container().config.from_pydantic(core_config)
    speech_container.wire(
        modules=[
            "interaction.server.router.speech.v1",
            "interaction.server.router.speech.v2",
        ]
    )

    app = FastAPI(
        title="GoGo Golem AI Server",
        description="음성 처리 및 대화 생성 API",
        version="1.0.0",
    )

    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],  # 프로덕션에서는 특정 도메인으로 제한
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    app.state.core_container = core_container
    app.state.speech_container = speech_container
    app.state.config = core_config

    # 라우터 등록
    app.include_router(speech_router_v1, prefix="/api/v1", tags=["speech-v1"])
    app.include_router(speech_router_v2, prefix="/api/v2", tags=["speech-v2"])

    @app.get("/")
    async def root():
        """헬스 체크 엔드포인트"""
        return {
            "status": "ok",
            "message": "GoGo Golem AI Server is running",
            "version": "1.0.0",
        }

    @app.get("/health")
    async def health():
        """헬스 체크 엔드포인트"""
        return {
            "status": "healthy",
            "timestamp": datetime.now(timezone.utc).isoformat(),
        }

    return app


app = create_app()

if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000, reload=True)

from dependency_injector import containers, providers
from interaction.core.infra.model_router import ModelRouter
from interaction.core.infra.firebase_client import create_firebase_client
from interaction.core.di.config import CoreConfig


def create_router_client(
    openai_api_key,
    aws_access_key_id,
    aws_secret_access_key,
    aws_region_name,
):
    """개별 설정값들을 받아서 ModelRouter 클라이언트를 생성하고 반환합니다."""
    core_config = CoreConfig(
        openai_api_key=openai_api_key,
        aws_access_key_id=aws_access_key_id,
        aws_secret_access_key=aws_secret_access_key,
        aws_region_name=aws_region_name,
    )

    model_router = ModelRouter(
        config=core_config.to_model_router_config(),
        num_retries=2,
        allowed_fails=1,
        cooldown_time=30.0,
        timeout=60.0,
        set_verbose=True,
    )

    return model_router.create_client()


class CoreContainer(containers.DeclarativeContainer):
    """Core 모듈의 의존성 주입 컨테이너"""

    # Configuration
    config = providers.Configuration()

    # Infrastructure
    model_router = providers.Singleton(
        create_router_client,
        openai_api_key=config.openai_api_key,
        aws_access_key_id=config.aws_access_key_id,
        aws_secret_access_key=config.aws_secret_access_key,
        aws_region_name=config.aws_region_name,
    )

    # Firebase Client
    firebase_client = providers.Singleton(
        create_firebase_client,
        credentials_path=config.firebase_credentials_path,
    )

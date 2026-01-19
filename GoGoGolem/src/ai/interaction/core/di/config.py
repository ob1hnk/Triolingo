from typing import Dict, Any
from pydantic_settings import BaseSettings


class CoreConfig(BaseSettings):
    openai_api_key: str | None = None
    aws_access_key_id: str | None = None
    aws_secret_access_key: str | None = None
    aws_region_name: str | None = None

    # Firebase configuration
    firebase_credentials_path: str | None = None
    firebase_database_url: str | None = None

    def to_model_router_config(self) -> Dict[str, Any]:
        """ModelRouter에 필요한 설정 딕셔너리를 반환합니다."""
        return {
            "openai_api_key": self.openai_api_key,
            "aws_access_key_id": self.aws_access_key_id,
            "aws_secret_access_key": self.aws_secret_access_key,
            "aws_region_name": self.aws_region_name,
        }

    def to_firebase_config(self) -> Dict[str, Any]:
        """Firebase에 필요한 설정 딕셔너리를 반환합니다."""
        return {
            "credentials_path": self.firebase_credentials_path,
            "database_url": self.firebase_database_url,
        }

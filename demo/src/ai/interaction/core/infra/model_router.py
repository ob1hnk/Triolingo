import logging
from typing import Dict, List, Any
from litellm import Router
from .model_configs import ModelConfigs

logger = logging.getLogger(__name__)


class ModelRouter:
    """
    LiteLLM Router를 활용한 모델 라우터 클래스
    다양한 LLM provider를 관리
    """

    def __init__(
        self,
        config: Dict[str, Any],
        num_retries: int = 3,
        allowed_fails: int = 2,
        cooldown_time: float = 60.0,
        timeout: float = 60.0,
        cache_responses: bool = False,
        enable_pre_call_checks: bool = True,
        set_verbose: bool = False,
    ):
        """
        ModelRouter 초기화

        Args:
            config: API 키 및 설정 정보
            num_retries: 재시도 횟수
            allowed_fails: 허용 실패 횟수
            cooldown_time: 실패 후 쿨다운 시간 (초)
            timeout: 요청 타임아웃 (초)
            cache_responses: 응답 캐싱 여부
            enable_pre_call_checks: 사전 호출 검사 활성화
            set_verbose: 상세 로깅 활성화
        """
        self.config = config
        self.num_retries = num_retries
        self.allowed_fails = allowed_fails
        self.cooldown_time = cooldown_time
        self.timeout = timeout
        self.cache_responses = cache_responses
        self.enable_pre_call_checks = enable_pre_call_checks
        self.set_verbose = set_verbose

        self.model_list = []
        self.default_fallbacks = []

        self._setup_models()
        self._setup_fallbacks()

        logger.info(f"ModelRouter initialized with {len(self.model_list)} models")

    def _setup_models(self):
        """사용 가능한 API 키에 따라 모델 리스트를 설정합니다."""

        # OpenAI 모델 설정
        if self._has_openai_config():
            self._add_openai_models()

        if not self.model_list:
            logger.warning("No models configured. Please check your API keys.")

    def _has_openai_config(self) -> bool:
        """OpenAI 설정이 있는지 확인"""
        return "openai_api_key" in self.config and self.config["openai_api_key"]

    def _add_openai_models(self):
        """OpenAI 모델들을 추가합니다."""
        for model_name in ModelConfigs.OPENAI_MODELS:
            model_config = ModelConfigs.get_model_config(model_name)
            if model_config:
                self.model_list.append(
                    {
                        "model_name": model_name,
                        "litellm_params": {
                            "model": model_name,
                            "api_key": self.config["openai_api_key"],
                            "timeout": self.timeout,
                        },
                    }
                )

    def _setup_fallbacks(self):
        """폴백 모델들을 설정합니다."""
        fallback_groups = {}

        for model in self.model_list:
            model_name = model["model_name"]
            fallbacks = ModelConfigs.get_fallbacks(model_name)
            if fallbacks:
                available_fallbacks = [
                    fb
                    for fb in fallbacks
                    if any(m["model_name"] == fb for m in self.model_list)
                ]
                if available_fallbacks:
                    fallback_groups[model_name] = available_fallbacks

        for primary_model, fallback_models in fallback_groups.items():
            self.default_fallbacks.append({primary_model: fallback_models})

        logger.info(f"Fallback groups configured: {fallback_groups}")

    def get_available_models(self) -> List[str]:
        """사용 가능한 모델 목록을 반환합니다."""
        return [model["model_name"] for model in self.model_list]

    def create_client(self) -> Router:
        """
        LiteLLM Router 클라이언트를 생성하고 반환합니다.

        Returns:
            Router: 설정된 라우팅 전략과 모델 리스트를 가진 LiteLLM Router 인스턴스
        """
        router_kwargs = {
            "model_list": self.model_list,
            "fallbacks": self.default_fallbacks,
            "num_retries": self.num_retries,
            "allowed_fails": self.allowed_fails,
            "cooldown_time": self.cooldown_time,
            "timeout": self.timeout,
            "cache_responses": self.cache_responses,
            "enable_pre_call_checks": self.enable_pre_call_checks,
            "set_verbose": self.set_verbose,
        }

        try:
            router = Router(**router_kwargs)
            logger.info(
                f"Router client created successfully with {len(self.model_list)} models"
            )
            return router
        except Exception as e:
            logger.error(f"Failed to create router client: {e}")
            raise

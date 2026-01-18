"""
LLM 호출 재시도를 위한 유틸리티
"""

import logging
from tenacity import (
    retry,
    stop_after_attempt,
    wait_random_exponential,
    RetryCallState,
)

logger = logging.getLogger(__name__)


def log_retry_attempt(retry_state: RetryCallState):
    """
    Retry 시도를 로깅하는 콜백

    Args:
        retry_state: tenacity의 retry 상태 객체
    """
    exception = retry_state.outcome.exception()
    logger.warning(
        f"Retrying LLM call (attempt {retry_state.attempt_number}/3) "
        f"due to: {type(exception).__name__}: {str(exception)}"
    )


def with_llm_retry(
    max_attempts: int = 3,
    min_wait: int = 1,
    max_wait: int = 10,
):
    """
    LLM 호출에 재시도 로직을 추가하는 데코레이터 팩토리

    wait_random_exponential을 사용하여 지수 백오프 적용

    Args:
        max_attempts: 최대 시도 횟수 (기본값: 3)
        min_wait: 최소 대기 시간(초) (기본값: 1)
        max_wait: 최대 대기 시간(초) (기본값: 10)

    Returns:
        재시도 로직이 적용된 데코레이터
    """
    return retry(
        stop=stop_after_attempt(max_attempts),
        wait=wait_random_exponential(min=min_wait, max=max_wait),
        reraise=True,
        before_sleep=log_retry_attempt,
    )

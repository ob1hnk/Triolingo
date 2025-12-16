"""
Trace Context 전파를 위한 유틸리티

ContextVar를 사용하여 trace_id를 전파합니다.
"""

from contextvars import ContextVar
from typing import Optional

# ContextVar로 trace_id 저장
trace_id_var: ContextVar[Optional[str]] = ContextVar("trace_id", default=None)


def set_trace_id(trace_id: str) -> None:
    """현재 컨텍스트에 trace_id 설정"""
    trace_id_var.set(trace_id)


def get_trace_id() -> Optional[str]:
    """현재 컨텍스트에서 trace_id 가져오기"""
    return trace_id_var.get()


def clear_trace_id() -> None:
    """현재 컨텍스트에서 trace_id 제거"""
    trace_id_var.set(None)


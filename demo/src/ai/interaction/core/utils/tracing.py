"""
OpenTelemetry Tracing 초기화 및 유틸리티
"""

import logging
import os
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk.resources import Resource

logger = logging.getLogger(__name__)


def setup_tracing() -> None:
    """OpenTelemetry Tracing 초기화"""
    # OTLP endpoint 설정 (환경 변수 또는 기본값)
    otlp_endpoint = os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "http://tempo:4318")
    service_name = os.getenv("OTEL_SERVICE_NAME", "ai-server")

    logger.info(f"Setting up OpenTelemetry tracing: endpoint={otlp_endpoint}, service={service_name}")

    # Resource 생성 (서비스 정보 포함)
    resource = Resource.create(
        {
            "service.name": service_name,
            "service.version": "1.0.0",
        }
    )

    # TracerProvider 설정
    trace.set_tracer_provider(TracerProvider(resource=resource))
    tracer_provider = trace.get_tracer_provider()

    # OTLP Exporter 생성
    otlp_exporter = OTLPSpanExporter(
        endpoint=otlp_endpoint + "/v1/traces",
    )

    # BatchSpanProcessor로 성능 최적화
    span_processor = BatchSpanProcessor(otlp_exporter)
    tracer_provider.add_span_processor(span_processor)

    logger.info("OpenTelemetry tracing initialized successfully")


def get_tracer(name: str) -> trace.Tracer:
    """Tracer 인스턴스 가져오기"""
    return trace.get_tracer(name)


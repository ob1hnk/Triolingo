"""
OpenTelemetry Tracing 테스트 스크립트

서버가 제대로 Trace를 생성하고 Tempo로 전송하는지 테스트합니다.
"""

import asyncio
import logging
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk.resources import Resource

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def setup_test_tracing():
    """테스트용 Tracing 설정"""
    resource = Resource.create({"service.name": "test-client"})
    trace.set_tracer_provider(TracerProvider(resource=resource))
    tracer_provider = trace.get_tracer_provider()

    otlp_exporter = OTLPSpanExporter(
        endpoint="http://localhost:4318/v1/traces"
    )

    span_processor = BatchSpanProcessor(otlp_exporter)
    tracer_provider.add_span_processor(span_processor)

    logger.info("Test tracing initialized")


async def test_trace():
    """간단한 Trace 생성 테스트"""
    setup_test_tracing()
    tracer = trace.get_tracer(__name__)

    with tracer.start_as_current_span("test_operation") as span:
        span.set_attribute("test.attribute", "test_value")
        span.set_attribute("test.number", 42)

        # 중첩 Span
        with tracer.start_as_current_span("nested_operation") as nested_span:
            nested_span.set_attribute("nested.attribute", "nested_value")
            await asyncio.sleep(0.1)  # 시뮬레이션

        await asyncio.sleep(0.1)

    logger.info("Test trace created. Check Grafana Tempo to see it.")
    logger.info("Query: {service.name=\"test-client\"}")


if __name__ == "__main__":
    asyncio.run(test_trace())


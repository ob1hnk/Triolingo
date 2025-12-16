# OpenTelemetry Tracing 설정 가이드

## 개요

Unity 클라이언트와 AI 서버 간의 레이턴시를 추적하기 위한 OpenTelemetry 설정 가이드입니다.

## 아키텍처

```
Unity Client → WebSocket (trace_id) → AI Server → OpenTelemetry → Tempo → Grafana
```

## 1. 인프라 시작

### 1.1 Tempo + Grafana 시작

```bash
cd demo/src/ai
docker-compose up -d tempo grafana
```

서비스 확인:
- Tempo: http://localhost:3200 (API)
- Grafana: http://localhost:3000

### 1.2 AI 서버 시작

```bash
# 의존성 설치 (처음 한 번만)
uv sync

# 서버 시작
docker-compose up -d app

# 로그 확인
docker-compose logs -f app
```

## 2. Grafana 설정

### 2.1 Tempo 데이터 소스 추가

1. Grafana 접속: http://localhost:3000
2. Configuration (⚙️) → Data Sources → Add data source
3. **Tempo** 선택
4. 설정:
   - **URL**: `http://tempo:3200`
   - **Basic Auth**: 비활성화
   - **Save & Test** 클릭

### 2.2 Trace 조회

1. **Explore** (🔍) 메뉴 클릭
2. Data source: **Tempo** 선택
3. Query 입력:
   ```
   {service.name="ai-server"}
   ```
4. 또는 trace_id로 검색:
   ```
   {trace_id="YOUR_TRACE_ID"}
   ```

### 2.3 대시보드 생성 (선택)

1. **Dashboards** → **New Dashboard**
2. **Add visualization** → **Tempo** 선택
3. 패널 타입:
   - **Trace Timeline**: 시간별 Trace 시각화
   - **Service Map**: 서비스 간 관계도
   - **Latency Histogram**: 레이턴시 분포

## 3. Unity에서 테스트

### 3.1 Unity 실행

1. Unity에서 프로젝트 열기
2. VADDemo 씬 실행
3. 음성 입력 시작

### 3.2 Trace 확인

Unity Console에서 `trace_id` 로그 확인:
```
[trace_id=xxxx-xxxx-xxxx] Recording started
```

이 `trace_id`를 Grafana에서 검색하여 전체 Trace 확인 가능.

## 4. 서버 Span 구조

각 요청은 다음 Span으로 구성됩니다:

```
Trace: {trace_id}
 ├─ ws_receive        (WebSocket 메시지 수신)
 ├─ session_merge     (오디오 청크 병합)
 ├─ stt               (Speech-to-Text)
 ├─ llm_call          (LLM 호출)
 └─ ws_send           (응답 전송)
```

## 5. 문제 해결

### 5.1 Tempo에 Trace가 안 보일 때

1. 서버 로그 확인:
   ```bash
   docker-compose logs app | grep -i "opentelemetry\|tracing"
   ```

2. Tempo 연결 확인:
   ```bash
   curl http://localhost:3200/api/search
   ```

3. 환경 변수 확인:
   ```bash
   docker-compose exec app env | grep OTEL
   ```

### 5.2 Grafana에서 데이터 소스 연결 실패

- Tempo가 실행 중인지 확인: `docker-compose ps`
- Tempo URL이 올바른지 확인: `http://tempo:3200` (Docker 네트워크 내부)

### 5.3 Unity trace_id가 전달되지 않을 때

- Unity Console에서 `trace_id` 로그 확인
- WebSocket 메시지에 `trace_id` 필드가 포함되어 있는지 확인
- 서버 로그에서 `trace_id` 수신 여부 확인

## 6. 고급 설정

### 6.1 샘플링 설정

프로덕션에서 모든 Trace를 수집하지 않으려면 샘플링 설정:

```python
# interaction/core/utils/tracing.py
from opentelemetry.sdk.trace.sampling import TraceIdRatioBased

# 10%만 샘플링
sampler = TraceIdRatioBased(0.1)
trace.set_tracer_provider(TracerProvider(resource=resource, sampler=sampler))
```

### 6.2 Span 메타데이터 추가

더 많은 정보를 추적하려면 Span에 attribute 추가:

```python
span.set_attribute("user_id", user_id)
span.set_attribute("audio_duration_seconds", duration)
span.set_attribute("model_version", "1.0.0")
```

## 7. 참고 자료

- [OpenTelemetry Python](https://opentelemetry.io/docs/instrumentation/python/)
- [Grafana Tempo](https://grafana.com/docs/tempo/latest/)
- [OTLP Protocol](https://opentelemetry.io/docs/specs/otlp/)


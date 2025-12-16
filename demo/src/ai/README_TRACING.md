# OpenTelemetry Tracing 사용 가이드

## 빠른 시작

### 1. 인프라 시작

```bash
cd demo/src/ai
docker-compose up -d tempo grafana
```

### 2. 서버 시작

```bash
docker-compose up -d app
```

### 3. Grafana 접속 및 설정

1. http://localhost:3000 접속
2. Configuration → Data Sources → Add data source → **Tempo**
3. URL: `http://tempo:3200` → Save & Test

### 4. Trace 조회

1. Explore 메뉴 클릭
2. Data source: Tempo 선택
3. Query: `{service.name="ai-server"}`

## Unity에서 Span 생성 (선택적)

Unity에서도 Span을 생성하려면 `OpenTelemetryHelper`를 사용하세요:

```csharp
// VAD 감지 시
OpenTelemetryHelper.StartSpan("unity.vad_detection", traceId, new Dictionary<string, object> {
    {"energy", currentEnergy},
    {"rms", rms}
});
// ... VAD 처리 ...
OpenTelemetryHelper.EndSpan("unity.vad_detection", traceId);

// PCM 변환 시
OpenTelemetryHelper.StartSpan("unity.pcm_conversion", traceId);
byte[] pcmData = ConvertToPCM16(samples);
OpenTelemetryHelper.EndSpan("unity.pcm_conversion", traceId, new Dictionary<string, object> {
    {"pcm_size_bytes", pcmData.Length}
});

// WebSocket 전송 시
OpenTelemetryHelper.StartSpan("unity.ws_send_audio_chunk", traceId);
// ... 전송 ...
OpenTelemetryHelper.EndSpan("unity.ws_send_audio_chunk", traceId);
```

## 테스트

### 서버 Tracing 테스트

```bash
cd demo/src/ai
python test_tracing.py
```

Grafana에서 `{service.name="test-client"}`로 검색하여 확인.

### 전체 플로우 테스트

1. Unity 실행
2. 음성 입력
3. Unity Console에서 `trace_id` 확인
4. Grafana에서 해당 `trace_id`로 검색

## 문제 해결

### Trace가 안 보일 때

1. **서버 로그 확인**:
   ```bash
   docker-compose logs app | grep -i tracing
   ```

2. **Tempo 연결 확인**:
   ```bash
   curl http://localhost:3200/api/search
   ```

3. **환경 변수 확인**:
   ```bash
   docker-compose exec app env | grep OTEL
   ```

### Grafana 연결 실패

- Tempo가 실행 중인지: `docker-compose ps`
- Docker 네트워크 확인: `docker network ls`

## Span 구조

```
Trace (Unity → Server)
 ├─ unity.vad_detection      (Unity)
 ├─ unity.pcm_conversion     (Unity)
 ├─ unity.ws_send            (Unity)
 ├─ ws_receive               (Server)
 ├─ session_merge            (Server)
 ├─ stt                      (Server)
 ├─ llm_call                (Server)
 └─ ws_send                  (Server)
```

## 참고

- 상세 설정: [TRACING_SETUP.md](./TRACING_SETUP.md)
- OpenTelemetry 문서: https://opentelemetry.io/


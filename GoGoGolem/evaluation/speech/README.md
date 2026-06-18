# 음성 대화 시스템 — 응답 시간 평가

> 발표용 평가 자료. 음성 파이프라인에 **OpenTelemetry tracing**을 적용해 수집한
> span 데이터를 분석하고, **파이프라인 모드(STT → LLM)** 와
> **Realtime API 모드**의 응답 시간을 정량 비교한다.

---

## 1. 평가 목적

사용자가 말을 끝낸 뒤 AI 응답이 시작되기까지의 **end-to-end 응답 지연**을
측정한다. 초기 구조(STT → LLM 직렬)와 Realtime API 전환 이후의 차이를
실측 데이터로 비교해 개선 효과를 정량화한다.

---

## 2. 계측 구조

`generate_conversation_response` usecase 안에 두 개의 span을 삽입했다.

```python
# GoGoGolem/src/ai/interaction/speech/domain/usecases/
#   generate_conversation_response.py

with tracer.start_as_current_span("stt") as stt_span:
    stt_span.set_attribute("language", language)
    transcription_text = await self.speech_to_text.transcribe_user_audio_to_text(...)
    stt_span.set_attribute("transcription_length", len(transcription_text))

with tracer.start_as_current_span("llm_call") as llm_span:
    llm_span.set_attribute("input_length", len(transcription_text))
    response_text = await self.text_to_text.create_response_from_user_audio_text(...)
    llm_span.set_attribute("response_length", len(response_text))
```

수집 인프라: **Grafana Tempo** (OTLP HTTP, docker-compose)

```
AI Server ──OTLP──► Tempo :4318 ──► Grafana :3000
```

Trace ID는 `ContextVar`로 전파되어 WebSocket 세션 전체에서 동일 trace로 묶인다.

---

## 3. 측정 모드

| 모드 | 구조 | 설명 |
|---|---|---|
| **Pipeline** | STT → LLM (직렬) | Whisper STT 완료 후 GPT-4o 호출 |
| **Realtime API** | 스트리밍 동시 처리 | 음성 입력과 응답 생성을 병렬로 처리 |

---

## 4. 결과 요약

| 모드 | 평균 응답 시간 | 구성 |
|---|---|---|
| Pipeline | **5,544 ms** | STT 2,287 ms + LLM 3,257 ms |
| Realtime API | **2,294 ms** | 스트리밍 동시 처리 |

**Realtime API가 파이프라인 대비 약 2.4배 빠름**

- 파이프라인의 직렬 병목(STT 완료 후 LLM 시작)을 스트리밍 병렬 처리로 해소
- 그래프: [response_time.png](response_time.png)

---

## 5. 파일 구성

| 파일 | 내용 |
|---|---|
| `sample_traces.json` | Tempo에서 수집한 실측 trace 데이터 (5회 × 2모드) |
| `analyze_traces.py` | trace 데이터 로드 → 통계 집계 → 그래프 생성 |
| `response_time.png` | 분석 결과 그래프 (발표 슬라이드용) |

---

## 6. 그래프 재생성

```bash
python evaluation/speech/analyze_traces.py
```

출력:
- 평균 응답 시간 bar chart (좌)
- 샘플별 지연 scatter plot (우)

---

## 7. 발표 슬라이드용 요약 멘트 (예시)

- **무엇을**: 음성 입력 → AI 응답까지의 end-to-end 응답 지연 측정.
- **어떻게**: OpenTelemetry로 STT/LLM 각 단계에 span 계측 → Grafana Tempo 수집
  → `sample_traces.json` 으로 저장 → `analyze_traces.py` 로 통계 및 시각화.
- **결과**: Realtime API 전환으로 5,544 ms → 2,294 ms, **약 2.4배 단축**.
- **의의**: 파이프라인의 직렬 병목을 스트리밍 병렬 처리로 해소해 사용자 체감
  응답성을 크게 개선.

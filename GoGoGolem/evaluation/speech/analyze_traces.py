"""
음성 대화 시스템 — OpenTelemetry 트레이스 분석 및 시각화
==========================================================

개요
----
GoGoGolem 음성 파이프라인에 OpenTelemetry tracing을 적용해 수집한 span 데이터를
분석한다. 각 대화 턴은 두 개의 span으로 계측된다:

  stt      : Whisper STT — 음성 → 텍스트 변환 시간
  llm_call : GPT-4o  LLM — 텍스트 → 응답 생성 시간

파이프라인 모드에서는 두 span이 직렬로 실행되어 합산 지연이 발생한다.
Realtime API 모드는 입력 스트리밍과 응답 생성을 동시에 처리해 지연을 줄인다.

트레이싱 구현 위치
------------------
GoGoGolem/src/ai/interaction/speech/domain/usecases/generate_conversation_response.py
  with tracer.start_as_current_span("stt") as stt_span:
      ...
  with tracer.start_as_current_span("llm_call") as llm_span:
      ...

수집 인프라: Grafana Tempo (docker-compose), OTLP HTTP endpoint

데이터
------
sample_traces.json — Tempo에서 수집한 실측 trace 데이터 (5회 × 2모드)

실행
----
    python evaluation/speech/analyze_traces.py
"""

from __future__ import annotations

import json
import statistics
from pathlib import Path

import matplotlib
import matplotlib.patches as mpatches
import matplotlib.pyplot as plt

DATA_PATH = Path(__file__).resolve().parent / "sample_traces.json"
OUTPUT_PATH = Path(__file__).resolve().parent / "response_time.png"


# ──────────────────────────────────────────────────────────────────────────
# 1. 데이터 로드 및 파싱
# ──────────────────────────────────────────────────────────────────────────

def load_traces(path: Path) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def extract_pipeline_times(traces: list[dict]) -> tuple[list[float], list[float]]:
    """각 trace에서 stt/llm_call span duration을 추출한다."""
    stt_times, llm_times = [], []
    for trace in traces:
        span_map = {s["name"]: s["duration_ms"] for s in trace["spans"]}
        stt_times.append(span_map["stt"])
        llm_times.append(span_map["llm_call"])
    return stt_times, llm_times


def extract_realtime_times(traces: list[dict]) -> list[float]:
    return [t["duration_ms"] for t in traces]


# ──────────────────────────────────────────────────────────────────────────
# 2. 통계 요약
# ──────────────────────────────────────────────────────────────────────────

def print_summary(stt: list[float], llm: list[float], rt: list[float]) -> None:
    total_pipeline = [s + l for s, l in zip(stt, llm)]

    print("=" * 60)
    print("음성 대화 시스템 — 응답 시간 분석 결과")
    print("=" * 60)

    print("\n[파이프라인 모드 (STT → LLM 직렬)]")
    print(f"  STT      : mean={statistics.mean(stt):.0f}ms  "
          f"std={statistics.stdev(stt):.0f}ms  "
          f"min={min(stt):.0f}ms  max={max(stt):.0f}ms")
    print(f"  LLM      : mean={statistics.mean(llm):.0f}ms  "
          f"std={statistics.stdev(llm):.0f}ms  "
          f"min={min(llm):.0f}ms  max={max(llm):.0f}ms")
    print(f"  합계     : mean={statistics.mean(total_pipeline):.0f}ms  "
          f"std={statistics.stdev(total_pipeline):.0f}ms")

    print("\n[Realtime API 모드]")
    print(f"  응답시간 : mean={statistics.mean(rt):.0f}ms  "
          f"std={statistics.stdev(rt):.0f}ms  "
          f"min={min(rt):.0f}ms  max={max(rt):.0f}ms")

    ratio = statistics.mean(total_pipeline) / statistics.mean(rt)
    print(f"\n→ Realtime이 파이프라인 대비 {ratio:.1f}배 빠름")
    print("=" * 60)


# ──────────────────────────────────────────────────────────────────────────
# 3. 시각화 (대표값 bar chart + 개별 샘플 scatter)
# ──────────────────────────────────────────────────────────────────────────

STT_COLOR = "#AEC7E8"
LLM_COLOR = "#1F77B4"
RT_COLOR  = "#D62728"


def plot(stt: list[float], llm: list[float], rt: list[float]) -> None:
    matplotlib.rcParams["axes.unicode_minus"] = False

    stt_mean = statistics.mean(stt)
    llm_mean = statistics.mean(llm)
    rt_mean  = statistics.mean(rt)

    fig, axes = plt.subplots(1, 2, figsize=(13, 5))

    # ── 왼쪽: 평균 응답 시간 bar chart ──────────────────────────────────
    ax = axes[0]
    ax.barh(1, stt_mean, color=STT_COLOR, height=0.5)
    ax.barh(1, llm_mean, left=stt_mean, color=LLM_COLOR, height=0.5)
    ax.barh(0, rt_mean, color=RT_COLOR, height=0.5)

    ax.text(stt_mean / 2, 1, f"{stt_mean:.0f}ms",
            ha="center", va="center", fontsize=10, fontweight="bold", color="#1a3a5c")
    ax.text(stt_mean + llm_mean / 2, 1, f"{llm_mean:.0f}ms",
            ha="center", va="center", fontsize=10, fontweight="bold", color="white")
    ax.text(rt_mean / 2, 0, f"{rt_mean:.0f}ms",
            ha="center", va="center", fontsize=10, fontweight="bold", color="white")

    ax.set_yticks([0, 1])
    ax.set_yticklabels(["Realtime API", "STT + LLM"], fontsize=11, fontweight="bold")
    ax.set_xlim(0, 6500)
    ax.set_xlabel("ms")
    ax.set_title("Average Response Time (ms)", fontsize=13, fontweight="bold", loc="left")

    stt_patch = mpatches.Patch(color=STT_COLOR, label="STT (Whisper)")
    llm_patch = mpatches.Patch(color=LLM_COLOR, label="LLM (GPT-4o)")
    ax.legend(handles=[stt_patch, llm_patch], loc="upper right", frameon=False, fontsize=9)
    for spine in ["top", "right"]:
        ax.spines[spine].set_visible(False)

    total_mean = stt_mean + llm_mean
    fig.text(0.27, 0.02,
             f"{total_mean:.0f}ms → {rt_mean:.0f}ms   "
             f"Realtime is ~{total_mean / rt_mean:.1f}x faster",
             ha="center", fontsize=10, fontweight="bold", color=RT_COLOR)

    # ── 오른쪽: 개별 샘플 scatter (STT / LLM / Realtime) ──────────────
    ax2 = axes[1]
    n = len(stt)
    x = list(range(1, n + 1))

    total_pipeline = [s + l for s, l in zip(stt, llm)]
    ax2.plot(x, stt, "o--", color=STT_COLOR, label="STT", linewidth=1.2)
    ax2.plot(x, llm, "s--", color=LLM_COLOR, label="LLM", linewidth=1.2)
    ax2.plot(x, total_pipeline, "^-", color="#2ca02c", label="STT+LLM total", linewidth=1.5)
    ax2.plot(x, rt[:n], "D-", color=RT_COLOR, label="Realtime", linewidth=1.5)

    ax2.set_xticks(x)
    ax2.set_xlabel("Sample")
    ax2.set_ylabel("ms")
    ax2.set_title("Per-Sample Latency", fontsize=13, fontweight="bold", loc="left")
    ax2.legend(fontsize=9, frameon=False)
    ax2.grid(axis="y", linestyle=":", alpha=0.4)
    for spine in ["top", "right"]:
        ax2.spines[spine].set_visible(False)

    plt.tight_layout()
    plt.savefig(OUTPUT_PATH, dpi=150, bbox_inches="tight")
    print(f"\n[저장] {OUTPUT_PATH}")


# ──────────────────────────────────────────────────────────────────────────
# 4. 메인
# ──────────────────────────────────────────────────────────────────────────

def main() -> None:
    data = load_traces(DATA_PATH)
    stt, llm = extract_pipeline_times(data["pipeline_traces"])
    rt = extract_realtime_times(data["realtime_traces"])

    print_summary(stt, llm, rt)
    plot(stt, llm, rt)
    print("완료.")


if __name__ == "__main__":
    main()

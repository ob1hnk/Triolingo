"""
편지 시스템 - 힌트 위장도(Hint Disguise) 평가 프레임워크
=========================================================

목적
----
부모 NPC가 생성한 답장 편지 안에, 다음 퀘스트의 핵심 힌트(next_quest_hint)가
"부모의 추억담"처럼 얼마나 자연스럽게 위장(disguise)되어 녹아들었는지를
정량적으로 평가한다.

파이프라인
----------
각 테스트 케이스마다:
    1) 실제 운영 프롬프트(SYSTEM_PROMPT)로 편지 생성  (gpt-4.1-mini, temp 0.7)
    2) LLM-as-judge 가 생성된 편지를 1~5점으로 채점     (gpt-4.1-mini, temp 0.0)
    3) 점수 + 근거 수집
마지막에:
    4) 케이스별 점수 bar chart + 전체 평균선을 matplotlib 로 출력

실행
----
    cd GoGoGolem/src/ai
    .venv/bin/python ../../evaluation/letter_system/hint_disguise_eval.py

환경
----
    OPENAI_API_KEY 를 GoGoGolem/src/ai/.env 에서 로드한다.
"""

from __future__ import annotations

import json
import os
import sys
from dataclasses import dataclass
from pathlib import Path

import matplotlib
import matplotlib.pyplot as plt
from dotenv import load_dotenv
from openai import OpenAI

# 실제 운영 프롬프트를 그대로 가져와 평가한다.
# (평가 대상과 운영 대상이 100% 일치해야 평가가 의미를 가진다)
_AI_ROOT = Path(__file__).resolve().parents[2] / "src" / "ai"
sys.path.insert(0, str(_AI_ROOT))
from interaction.text.prompts.text_to_text_v1 import SYSTEM_PROMPT  # noqa: E402

# ──────────────────────────────────────────────────────────────────────────
# 0. 설정
# ──────────────────────────────────────────────────────────────────────────

load_dotenv(_AI_ROOT / ".env")

GEN_MODEL = "gpt-4.1-mini"
GEN_TEMPERATURE = 0.7  # 편지 생성: 운영과 동일한 온도
JUDGE_MODEL = "gpt-4.1-mini"
JUDGE_TEMPERATURE = 0.0  # 채점: 재현성을 위해 결정론적으로

OUTPUT_DIR = Path(__file__).resolve().parent
CHART_PATH = OUTPUT_DIR / "hint_disguise_scores.png"
RESULT_JSON_PATH = OUTPUT_DIR / "hint_disguise_results.json"

client = OpenAI(api_key=os.environ["OPENAI_API_KEY"])


# ──────────────────────────────────────────────────────────────────────────
# 1. 평가 루브릭 (1~5점)
# ──────────────────────────────────────────────────────────────────────────
#
# 핵심 평가 축은 두 가지다.
#   (A) 위장(Disguise): 힌트가 "게임 지시문"이 아니라 "부모의 추억담"으로
#       자연스럽게 녹아들었는가?
#   (B) 정보 누설(Leakage): 도메인(예: "우물", "막힌 곳")만 언급하고,
#       정확한 해결 단계(solution steps)는 노출하지 않았는가?
#
# 1점에 가까울수록 직접 노출, 5점에 가까울수록 완벽한 위장.

RUBRIC = """
[힌트 위장도 평가 루브릭 — 1~5점]

5점 (완벽한 위장):
  - 힌트가 전적으로 부모의 개인적 추억/일화 형태로 등장한다.
  - 도메인 키워드(예: "우물", "막힌 곳")만 자연스럽게 언급하고,
    정확한 해결 단계는 전혀 노출하지 않는다.
  - 아이는 "지금" 이것이 힌트인지 알 수 없고, 퀘스트를 만난 뒤에야
    회상하며 깨닫는다("아, 그때 그 말이 이거였구나!").
  - 게임/퀘스트라는 단어가 전혀 등장하지 않으며, 몰입을 깨지 않는다.

4점 (자연스러운 위장, 사소한 흠):
  - 추억담/조언 형태로 잘 녹아 있으나, 힌트라는 의도가 살짝 비친다.
  - 도메인만 언급하지만 표현이 약간 작위적이거나, 해결의 방향을
    너무 친절히 짚어 준다.

3점 (위장 시도, 그러나 티가 남):
  - 추억담의 외형은 갖췄으나, 읽는 즉시 "이건 힌트구나" 하고 느껴진다.
  - 또는 해결 단계의 일부가 은근히 드러난다.

2점 (약한 위장 / 부분 노출):
  - 추억담 형식이 형식적이며, 해결 방법을 사실상 설명한다.
  - "~하면 된다" 식의 직접적 방법 제시가 섞여 있다.

1점 (직접 노출 / 위장 실패):
  - 힌트가 게임 지시문처럼 직접 노출된다
    (예: "다음에는 우물을 고치는 퀘스트가 있을 텐데, 막힌 곳을 찾아보세요").
  - 도메인 + 해결 단계가 모두 평문으로 노출되거나, "퀘스트/다음 미션"
    같은 메타 표현이 등장해 몰입이 완전히 깨진다.
""".strip()


# ──────────────────────────────────────────────────────────────────────────
# 2. LLM-as-judge 프롬프트
# ──────────────────────────────────────────────────────────────────────────

JUDGE_SYSTEM_PROMPT = f"""
너는 게임 내러티브 품질 평가 전문가다. 어린이용 어드벤처 게임의 편지 시스템에서,
부모 NPC가 보낸 답장 편지 안에 "다음 퀘스트 힌트(next_quest_hint)"가 얼마나
자연스럽게 '부모의 추억담처럼' 위장되었는지를 엄격하고 일관되게 채점한다.

좋은 위장이란:
  - 힌트가 부모의 개인적 일화/추억/삶의 지혜로 등장하고,
  - 도메인(소재) 키워드만 언급하며 정확한 해결 단계는 노출하지 않고,
  - 아이가 '나중에 퀘스트를 만난 뒤' 회상하며 깨닫게 되는 형태다.

나쁜 위장이란:
  - "다음 퀘스트", "미션" 같은 메타 표현이 등장하거나,
  - "~을 찾아보세요/~하면 된다"처럼 해결 단계를 직접 지시하거나,
  - 게임 시스템 안내문처럼 읽혀 몰입을 깨는 경우다.

아래 루브릭을 반드시 기준으로 삼는다:

{RUBRIC}

반드시 아래 JSON 스키마로만 응답한다 (다른 텍스트 금지):
{{
  "score": <1~5 사이 정수>,
  "disguise_quality": "<위장(추억담화) 정도에 대한 한 문장 평가>",
  "leakage": "<해결 단계 누설 여부에 대한 한 문장 평가>",
  "reason": "<점수를 준 핵심 근거 2~3문장>"
}}
""".strip()


def build_judge_user_prompt(next_quest_hint: str, generated_letter: str) -> str:
    return f"""
[평가 입력]

다음 퀘스트 힌트(next_quest_hint):
{next_quest_hint}

생성된 부모의 답장 편지:
\"\"\"
{generated_letter}
\"\"\"

위 편지 안에서 next_quest_hint 가 '부모의 추억담처럼' 얼마나 자연스럽게
위장되었는지를 루브릭에 따라 1~5점으로 채점하라. JSON 으로만 답하라.
""".strip()


# ──────────────────────────────────────────────────────────────────────────
# 3. 테스트 케이스 5개
# ──────────────────────────────────────────────────────────────────────────

@dataclass
class TestCase:
    id: str
    title: str
    title_en: str
    player_name: str
    player_gender: str
    child_letter: str
    current_quest_completed: str
    next_quest_hint: str
    next_quest_action: str

    def to_user_message(self) -> str:
        return f"""<letter_context>
player_name: {self.player_name}
player_gender: {self.player_gender}
child_letter: {self.child_letter}
current_quest_completed: {self.current_quest_completed}
next_quest_hint: {self.next_quest_hint}
next_quest_action: {self.next_quest_action}
</letter_context>"""


TEST_CASES: list[TestCase] = [
    TestCase(
        id="TC1_well",
        title="막힌 우물",
        title_en="Blocked Well",
        player_name="민서",
        player_gender="딸",
        child_letter=(
            "엄마 아빠, 나 잘 도착했어! 오늘 숲에서 큰 나무가 길을 막고 있었는데 "
            "골렘이랑 같이 치웠어. 강가에서 할아버지를 만났는데 비가 와서 식량을 "
            "못 건네고 계셔서, 나뭇잎을 묶어서 꾸러미를 가볍게 만들어 날려 드렸어. "
            "지금은 정착지라는 마을에 왔어. 낯설지만 괜찮아. 보고 싶어!"
        ),
        current_quest_completed="숲을 빠져나와 강 건너 할아버지 돕기",
        next_quest_hint="우물이 막혔을 때 막힌 곳을 찾는 게 중요하다",
        next_quest_action="마을 사람들에게 다가가서 곤란한 일을 도와주기",
    ),
    TestCase(
        id="TC2_cave_light",
        title="어두운 동굴",
        title_en="Dark Cave",
        player_name="지호",
        player_gender="아들",
        child_letter=(
            "아빠! 오늘 마을 아저씨가 잃어버린 염소를 같이 찾아 줬어. 골렘이 "
            "발자국을 잘 따라가더라고. 아저씨가 고맙다고 빵을 주셨어. 근데 마을 "
            "뒤편에 어두운 동굴이 있는데 다들 무서워서 안 들어간대. 나는 좀 "
            "궁금해. 엄마는 잘 지내?"
        ),
        current_quest_completed="잃어버린 염소 찾아 주기",
        next_quest_hint="깜깜한 곳에서는 빛을 밝히는 것부터 해야 길이 보인다",
        next_quest_action="동굴 입구의 노인에게 말을 걸어 동굴 탐험 의뢰 받기",
    ),
    TestCase(
        id="TC3_bridge",
        title="무너진 다리",
        title_en="Broken Bridge",
        player_name="하늘",
        player_gender="딸",
        child_letter=(
            "엄마, 오늘 비가 진짜 많이 왔어. 마을 빨래터에서 아주머니들이 물이 "
            "넘쳐서 걱정하길래 골렘이랑 도랑을 텄어. 물이 쑥 빠지니까 다들 "
            "박수쳐 줬어! 내일은 강 건너 윗마을에 가 보려고 하는데, 다리가 "
            "좀 위험해 보였어. 잘 자!"
        ),
        current_quest_completed="빨래터 물길 터 주기",
        next_quest_hint="무너진 다리는 양쪽에서 단단한 돌부터 차근차근 쌓아 올려야 한다",
        next_quest_action="강가의 석공에게 다리에 대해 물어보기",
    ),
    TestCase(
        id="TC4_locked_door",
        title="잠긴 문의 숫자",
        title_en="Locked Door",
        player_name="서연",
        player_gender="딸",
        child_letter=(
            "아빠 아빠! 오늘 도서관 할머니를 도와서 책을 정리했어. 책마다 번호가 "
            "있어서 순서대로 꽂는 게 재밌더라. 골렘이 무거운 책을 들어 줬어. "
            "할머니가 옛날 이야기도 해 주셨어. 그런데 도서관 안쪽에 잠긴 문이 "
            "하나 있는데 열쇠가 없대. 신기해."
        ),
        current_quest_completed="도서관 책 정리 돕기",
        next_quest_hint="잠긴 문에는 보통 주변에 숨겨진 순서나 숫자 단서가 함께 있다",
        next_quest_action="도서관 사서에게 잠긴 문에 대해 물어보기",
    ),
    TestCase(
        id="TC5_river_crossing",
        title="강 건너기",
        title_en="River Crossing",
        player_name="도윤",
        player_gender="아들",
        child_letter=(
            "엄마! 오늘 시장에서 길 잃은 아이를 집까지 데려다줬어. 골렘이 같이 "
            "있어서 아이가 안 무서워했어. 시장 상인이 사과를 줬어. 맛있었어. "
            "내일은 강 건너 약초밭에 심부름을 가야 하는데, 다리가 없어서 어떻게 "
            "건너야 할지 모르겠어. 도와줘!"
        ),
        current_quest_completed="길 잃은 아이 집에 데려다주기",
        next_quest_hint="강을 건널 때는 물살이 약하고 얕은 곳을 먼저 찾아야 안전하다",
        next_quest_action="강가 어부에게 건널 만한 곳을 물어보기",
    ),
]


# ──────────────────────────────────────────────────────────────────────────
# 4. 생성 + 채점
# ──────────────────────────────────────────────────────────────────────────

def generate_letter(tc: TestCase) -> str:
    resp = client.chat.completions.create(
        model=GEN_MODEL,
        temperature=GEN_TEMPERATURE,
        max_tokens=1000,
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": tc.to_user_message()},
        ],
    )
    return resp.choices[0].message.content.strip()


def judge_letter(next_quest_hint: str, generated_letter: str) -> dict:
    resp = client.chat.completions.create(
        model=JUDGE_MODEL,
        temperature=JUDGE_TEMPERATURE,
        response_format={"type": "json_object"},
        messages=[
            {"role": "system", "content": JUDGE_SYSTEM_PROMPT},
            {
                "role": "user",
                "content": build_judge_user_prompt(next_quest_hint, generated_letter),
            },
        ],
    )
    data = json.loads(resp.choices[0].message.content)
    data["score"] = int(data["score"])
    return data


# ──────────────────────────────────────────────────────────────────────────
# 5. 시각화
# ──────────────────────────────────────────────────────────────────────────

def plot_scores(results: list[dict], average: float) -> None:
    matplotlib.rcParams["axes.unicode_minus"] = False

    labels = [r["title_en"] for r in results]
    scores = [r["score"] for r in results]

    cmap = matplotlib.colormaps["RdYlGn"]
    colors = [cmap((s - 1) / 4) for s in scores]

    fig, ax = plt.subplots(figsize=(10, 6))
    bars = ax.bar(labels, scores, color=colors, edgecolor="black", linewidth=0.6, zorder=3)

    for bar, s in zip(bars, scores):
        ax.text(
            bar.get_x() + bar.get_width() / 2,
            bar.get_height() + 0.08,
            str(s),
            ha="center", va="bottom", fontsize=12, fontweight="bold",
        )

    ax.axhline(average, color="#1f4e8c", linestyle="--", linewidth=2, zorder=4,
               label=f"Overall average: {average:.2f} / 5")

    ax.set_ylim(0, 5.6)
    ax.set_ylabel("Hint Disguise Score (1-5)", fontsize=12)
    ax.set_title("Letter System - Hint Disguise Evaluation", fontsize=14, fontweight="bold")
    ax.set_yticks(range(0, 6))
    ax.grid(axis="y", linestyle=":", alpha=0.5, zorder=0)
    ax.legend(fontsize=11, loc="lower right")
    plt.xticks(rotation=15, ha="right")
    plt.tight_layout()
    plt.savefig(CHART_PATH, dpi=150)
    print(f"\n[저장] 그래프 → {CHART_PATH}")


# ──────────────────────────────────────────────────────────────────────────
# 6. 메인
# ──────────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 70)
    print("편지 시스템 - 힌트 위장도(Hint Disguise) 평가 시작")
    print(f"생성 모델: {GEN_MODEL} (temp={GEN_TEMPERATURE}) | "
          f"채점 모델: {JUDGE_MODEL} (temp={JUDGE_TEMPERATURE})")
    print("=" * 70)

    results: list[dict] = []

    for i, tc in enumerate(TEST_CASES, 1):
        print(f"\n[{i}/{len(TEST_CASES)}] {tc.id} — {tc.title}")
        print(f"  next_quest_hint: {tc.next_quest_hint}")

        letter = generate_letter(tc)
        print(f"  ▷ 생성된 편지:\n    {letter}")

        verdict = judge_letter(tc.next_quest_hint, letter)
        print(f"  ▷ 점수: {verdict['score']} / 5")
        print(f"    - 위장: {verdict.get('disguise_quality', '')}")
        print(f"    - 누설: {verdict.get('leakage', '')}")
        print(f"    - 근거: {verdict.get('reason', '')}")

        results.append({
            "id": tc.id,
            "title": tc.title,
            "title_en": tc.title_en,
            "next_quest_hint": tc.next_quest_hint,
            "generated_letter": letter,
            "score": verdict["score"],
            "disguise_quality": verdict.get("disguise_quality", ""),
            "leakage": verdict.get("leakage", ""),
            "reason": verdict.get("reason", ""),
        })

    average = sum(r["score"] for r in results) / len(results)

    print("\n" + "=" * 70)
    print("집계 결과")
    print("=" * 70)
    print(f"{'케이스':<18}{'점수':>6}")
    print("-" * 26)
    for r in results:
        print(f"{r['title']:<18}{r['score']:>6}")
    print("-" * 26)
    print(f"{'전체 평균':<18}{average:>6.2f}")

    with open(RESULT_JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(
            {"average": average, "results": results},
            f, ensure_ascii=False, indent=2,
        )
    print(f"\n[저장] 원자료 → {RESULT_JSON_PATH}")

    plot_scores(results, average)
    print("\n완료.")


if __name__ == "__main__":
    main()

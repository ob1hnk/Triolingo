"""
One Euro 파라미터 스윕 + jitter↔lag 트레이드오프 곡선.

analyze_gestures.py의 필터/지표 함수를 재사용해, 같은 raw 녹화에 다양한
파라미터를 오프라인 재적용한다(재녹화 불필요). 정지 → jitter, 왕복 → lag.

산출: 콘솔 격자 스윕 표(+ 확정값 대비 더 나은 조합 추천), sweep_oneeuro.csv,
      plot_tradeoff.png(OneEuro beta 곡선 vs MA 참조점; 좌하단일수록 우수).
analyze_gestures.py 출력 파일은 건드리지 않는다.

사용법: python optimize_oneeuro.py [폴더]
"""
import os
import sys
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

import analyze_gestures as A

MD = np.array([0.5, 0.5, 0.08])           # maxDelta (런타임 기본값)
CUR = (2.0, 2.0)                            # 확정 설정 (minCutoff, beta)
MA_DEADZONE = 0.005
TRIM_S = 1.5                                # analyze_gestures.py와 동일한 전이구간 트리밍
MIN_FPS = 70.0                              # 부하 녹화 제외 (dt 왜곡 → 지표 오염)


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))
    recs = A.load(folder, TRIM_S)
    dropped = [r for r in recs if r["fps_med"] < MIN_FPS]
    recs    = [r for r in recs if r["fps_med"] >= MIN_FPS]
    if dropped:
        print(f"[부하 제외] median fps < {MIN_FPS:.0f} 녹화 {len(dropped)}개 제외")
    for r in recs:
        r["type"] = "still" if r["amp"] < 0.4 else "motion"
    stills = [r for r in recs if r["type"] == "still"]
    motions = [r for r in recs if r["type"] == "motion"]
    if not stills or not motions:
        print(f"정지 {len(stills)}개 / 왕복 {len(motions)}개 — 둘 다 필요합니다.")
        sys.exit(1)
    print(f"정지 {len(stills)}개 / 왕복 {len(motions)}개로 스윕 (전이구간 trim={TRIM_S}s)\n")

    # 필터는 워밍업 위해 전체 raw에 적용하고, 측정만 중앙 안정 구간(mask)으로 한다
    def oneeuro(mc, b):
        jit, lag = [], []
        for r in stills:
            f = A.apply_one_euro(r["raw"], r["dt"], mc, b, MD)
            jit.append(A.jitter_rms(f[r["mask"]]))
        for r in motions:
            f = A.apply_one_euro(r["raw"], r["dt"], mc, b, MD)
            m, a = r["mask"], r["axis"]
            lag.append(A.estimate_lag_ms(r["t"][m], r["raw"][m, a], f[m, a]))
        return float(np.mean(jit)), float(np.nanmean(lag))

    def movavg(buf):
        jit, lag = [], []
        for r in stills:
            f = A.apply_moving_average(r["raw"], buf, MA_DEADZONE, MD)
            jit.append(A.jitter_rms(f[r["mask"]]))
        for r in motions:
            f = A.apply_moving_average(r["raw"], buf, MA_DEADZONE, MD)
            m, a = r["mask"], r["axis"]
            lag.append(A.estimate_lag_ms(r["t"][m], r["raw"][m, a], f[m, a]))
        return float(np.mean(jit)), float(np.nanmean(lag))

    # ── 격자 스윕 ──────────────────────────────────────────────
    mcs = [0.5, 1, 2, 3, 5, 8]
    betas = [0.05, 0.1, 0.2, 0.5, 1, 2, 4]
    cur_j, cur_l = oneeuro(*CUR)
    print(f"현재 설정 mc={CUR[0]}, β={CUR[1]}:  jitter={cur_j*1000:.2f}e-3, lag={cur_l:.1f}ms\n")

    rows = []
    for mc in mcs:
        for b in betas:
            j, l = oneeuro(mc, b)
            better = (j <= cur_j and l <= cur_l and (j < cur_j or l < cur_l))
            rows.append((mc, b, j, l, better))

    # CSV 저장
    csv = os.path.join(folder, "sweep_oneeuro.csv")
    with open(csv, "w", encoding="utf-8") as f:
        f.write("min_cutoff,beta,jitter,lag_ms,beats_current\n")
        for mc, b, j, l, bt in rows:
            f.write(f"{mc},{b},{j:.6f},{l:.4f},{int(bt)}\n")
    print(f"격자 결과 저장 → {csv}")

    # 현재값을 둘 다에서 이기는 조합
    dom = [r for r in rows if r[4]]
    if dom:
        # 개선 균형이 가장 좋은 것 추천 (정규화 합 최소)
        dom.sort(key=lambda r: r[2] / cur_j + r[3] / cur_l)
        print("\n현재값보다 떨림·지연 둘 다 나은 조합 (상위 5):")
        print(f"  {'mc':>4} {'beta':>5} {'jitter(e-3)':>12} {'lag(ms)':>8}")
        for mc, b, j, l, _ in dom[:5]:
            print(f"  {mc:>4} {b:>5} {j*1000:>12.2f} {l:>8.1f}")
        rmc, rb, rj, rl, _ = dom[0]
        print(f"\n추천: minCutoff={rmc}, beta={rb}  "
              f"(떨림 {(1-rj/cur_j)*100:+.0f}%, 지연 {(1-rl/cur_l)*100:+.0f}% vs 현재)")
    else:
        print(f"\n현재 설정(mc={CUR[0]}, β={CUR[1]})을 둘 다에서 능가하는 조합 없음 → 이미 좋은 튜닝입니다.")

    _plot_tradeoff(oneeuro, movavg, cur_j, cur_l, folder)


def _plot_tradeoff(oneeuro, movavg, cur_j, cur_l, folder):
    fig, ax = plt.subplots(figsize=(8, 6))
    betas = [0.2, 0.5, 1, 2, 3, 4]

    # 흐린 MA 참조 (스케일/맥락용, 주인공 아님)
    bufs = [2, 3, 4, 6, 8, 12]
    mp = [movavg(b) for b in bufs]
    mj = [p[0] * 1000 for p in mp]; ml = [p[1] for p in mp]
    ax.plot(ml, mj, "-s", color="#cfcfcf", ms=4, lw=1.2, label="MovingAverage (reference)")

    # 맥락용 다른 minCutoff 곡선 (mc=2가 계열 안 어디 앉는지, 얇게)
    for mc, col in [(1, "#f0a868"), (5, "#a6a6d6")]:
        pts = [oneeuro(mc, b) for b in betas]
        jj = [p[0] * 1000 for p in pts]; ll = [p[1] for p in pts]
        ax.plot(ll, jj, "-o", color=col, ms=3, lw=1.3, alpha=0.9, label=f"One Euro (minCutoff={mc})")

    # 주인공: 확정 minCutoff=2의 beta 스윕 곡선 (이 곡선 위에서 동작점 선택)
    pts = [oneeuro(2, b) for b in betas]
    j = [p[0] * 1000 for p in pts]; l = [p[1] for p in pts]
    ax.plot(l, j, "-o", color="#2ca02c", ms=6, lw=2.6, label="One Euro (minCutoff=2), β sweep")
    for b, lx, jy in zip(betas, l, j):
        ax.annotate(f"β={b}", (lx, jy), fontsize=8, color="#2ca02c",
                    textcoords="offset points", xytext=(6, -11))

    cj = cur_j * 1000
    ax.plot([cur_l], [cj], "*", color="black", ms=20, zorder=6, label="chosen (β=2) = knee")

    # knee 근거: chosen 직전(β1→2)/직후(β2→3) 교환비 비교 (β2 직후 붕괴 확인)
    i1, i2, i3 = betas.index(1), betas.index(2), betas.index(3)
    dl_b, dj_b = l[i1] - l[i2], j[i2] - j[i1]   # β1→2 (싸다)
    dl_a, dj_a = l[i2] - l[i3], j[i3] - j[i2]   # β2→3 (붕괴)
    box = (f"Why β=2 (the knee):\n"
           f"  β 1→2:  −{dl_b:.0f} ms lag  for  +{dj_b:.1f} jitter   ({dl_b/dj_b:.1f} ms per jitter)\n"
           f"  β 2→3:  −{dl_a:.0f} ms lag  for  +{dj_a:.1f} jitter   ({dl_a/dj_a:.1f} ms per jitter)\n"
           f"→ right after β=2, buying lag costs ~{ (dl_b/dj_b)/(dl_a/dj_a):.0f}× more jitter")
    ax.text(0.97, 0.97, box, transform=ax.transAxes, fontsize=9, va="top", ha="right",
            bbox=dict(boxstyle="round", fc="#f5f5f5", ec="#999999"))

    ax.set_xlabel("lag (ms)  —  lower = more responsive")
    ax.set_ylabel("jitter, frame-to-frame RMS (×10⁻³)  —  lower = smoother")
    ax.set_title("Why minCutoff=2, β=2: the knee of the One Euro jitter–lag tradeoff")
    ax.grid(alpha=0.3); ax.legend(fontsize=8, loc="lower left")
    fig.tight_layout()
    p = os.path.join(folder, "plot_tradeoff.png")
    fig.savefig(p, dpi=150); plt.close(fig)
    print(f"트레이드오프 곡선 저장 → {p}")


if __name__ == "__main__":
    main()

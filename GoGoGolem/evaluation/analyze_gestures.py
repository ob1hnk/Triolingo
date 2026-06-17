"""
제스처 떨림 보정 필터 비교 분석 (None / MovingAverage / OneEuro).

핵심: CSV의 raw 컬럼은 활성 필터와 무관하게 기록되므로, 녹화 하나의 raw에
세 필터를 오프라인 재적용하면 같은 손동작에 대한 공정한 비교가 된다(재녹화 불필요).
손 올리고/내리는 전이구간은 측정을 오염시키므로 앞뒤 --trim-s초를 잘라 중앙
안정 구간만 분류·측정에 쓴다(필터는 워밍업 위해 전체 raw에 적용 후 출력만 자름).

지표 (녹화별 측정 후 mean ± std → 오차막대):
  jitter = 정지 raw에 필터 적용 후 프레임간 3D RMS (낮을수록 좋음)
  lag    = 왕복 raw에 필터 적용 후 raw 대비 지연 ms (낮을수록 좋음)
  fps    = 런타임 fps 컬럼을 녹화 모드별 평균 (오프라인 무관)

CSV 컬럼: time_s, dt_s, fps, filter_mode, raw_x..z, filt_x..z
사용법: python analyze_gestures.py [폴더] [--min-cutoff 2.0 --beta 2.0 --ma-buffer 4]
출력:   summary.csv + plot_*.png (그래프 라벨 영어 → 한글 폰트 불필요)
"""

import os
import sys
import glob
import argparse
import numpy as np
import pandas as pd
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

# Windows 한글 콘솔(cp949)에서 기호 출력 시 크래시 방지
try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

FILTERS = ["None", "MovingAverage", "OneEuro"]
COLORS = {"None": "#888888", "MovingAverage": "#1f77b4", "OneEuro": "#d62728"}
AXES = ["x", "y", "z"]


# ──────────────────────────────────────────────────────────────────────────
# 필터 파이썬 포팅 (GolemLandmarkAnimator.cs와 동일한 수식)
# ──────────────────────────────────────────────────────────────────────────
def _clamp_delta(r, prev, md):
    out = r.copy()
    for k in range(3):
        if md[k] < 1e30:
            out[k] = min(max(r[k], prev[k] - md[k]), prev[k] + md[k])
    return out


def _alpha(cutoff, dt):
    tau = 1.0 / (2.0 * np.pi * cutoff)
    return 1.0 / (1.0 + tau / dt)


def apply_none(raw, max_delta):
    out = np.empty_like(raw)
    prev = None
    for i in range(len(raw)):
        if prev is None:
            prev = raw[i].copy(); out[i] = raw[i]; continue
        r = _clamp_delta(raw[i], prev, max_delta); prev = r; out[i] = r
    return out


def apply_moving_average(raw, buffer_size, deadzone, max_delta):
    out = np.empty_like(raw)
    buf = np.zeros((buffer_size, 3)); idx = 0; cnt = 0
    filt = np.zeros(3); init = False
    for i in range(len(raw)):
        r = _clamp_delta(raw[i], filt, max_delta) if init else raw[i].copy()
        buf[idx] = r; idx = (idx + 1) % buffer_size; cnt = min(cnt + 1, buffer_size)
        avg = buf[:cnt].mean(axis=0)
        if (not init) or np.linalg.norm(avg - filt) > deadzone:
            filt = avg
        init = True; out[i] = filt
    return out


def apply_one_euro(raw, dt, min_cutoff, beta, max_delta, d_cutoff=1.0):
    out = np.empty_like(raw)
    prev = None; dprev = np.zeros(3)
    for i in range(len(raw)):
        h = dt[i]
        if prev is None:
            prev = raw[i].copy(); dprev = np.zeros(3); out[i] = raw[i]; continue
        if h <= 0:
            out[i] = prev; continue
        r = _clamp_delta(raw[i], prev, max_delta)
        d_alpha = _alpha(d_cutoff, h)
        d_raw = (r - prev) / h
        d_filt = d_alpha * d_raw + (1.0 - d_alpha) * dprev
        cutoff = min_cutoff + beta * np.abs(d_filt)
        a = _alpha(cutoff, h)
        filt = a * r + (1.0 - a) * prev
        prev = filt; dprev = d_filt; out[i] = filt
    return out


def apply_filter(name, raw, dt, p):
    if name == "None":
        return apply_none(raw, p["max_delta"])
    if name == "MovingAverage":
        return apply_moving_average(raw, p["ma_buffer"], p["ma_deadzone"], p["max_delta"])
    return apply_one_euro(raw, dt, p["min_cutoff"], p["beta"], p["max_delta"])


# ──────────────────────────────────────────────────────────────────────────
# 지표
# ──────────────────────────────────────────────────────────────────────────
def central_mask(t, trim_s):
    # 앞뒤 trim_s초(전이구간)를 잘라낸 중앙 안정 구간 마스크. 녹화가 짧으면 트리밍 생략.
    if len(t) < 16:
        return np.ones(len(t), bool)
    if (t[-1] - t[0]) <= 2.0 * trim_s + 1.0:
        return np.ones(len(t), bool)
    return (t >= t[0] + trim_s) & (t <= t[-1] - trim_s)


def dominant_axis(raw):
    # 좌우 움직임 축(x/y) 중 진폭 최대. z(깊이)는 노이즈 커서 분류/지연 축에서 제외.
    ranges = raw.max(axis=0) - raw.min(axis=0)
    ax = int(np.argmax(ranges[:2]))
    return ax, float(ranges[ax])


def jitter_rms(filt):
    # 프레임간 3D RMS. 정지 시 이상적으로 0이며, 느린 드리프트엔 둔감하고 고주파 떨림만 잡음.
    d = np.diff(filt, axis=0)
    return float(np.sqrt((d ** 2).sum(axis=1).mean()))


def estimate_lag_ms(t, raw_axis, filt_axis, max_lag_s=0.5):
    if len(t) < 16:
        return float("nan")
    dt = np.median(np.diff(t))
    if dt <= 0:
        return float("nan")
    fps = 1.0 / dt
    tu = np.arange(t[0], t[-1], dt)
    ru = np.interp(tu, t, raw_axis) - np.interp(tu, t, raw_axis).mean()
    fu = np.interp(tu, t, filt_axis) - np.interp(tu, t, filt_axis).mean()
    if ru.std() < 1e-6:
        return float("nan")
    max_lag = int(max_lag_s * fps)
    best_tau, best_c = 0, -np.inf
    for tau in range(0, max_lag + 1):
        a = fu[tau:]; b = ru[: len(ru) - tau]
        if len(a) < 16:
            break
        c = float(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-12))
        if c > best_c:
            best_c, best_tau = c, tau
    return best_tau / fps * 1000.0


# ──────────────────────────────────────────────────────────────────────────
def load(folder, trim_s):
    recs = []
    for f in sorted(glob.glob(os.path.join(folder, "gesture_*.csv"))):
        try:
            df = pd.read_csv(f, keep_default_na=False)  # "None" 문자열 보존
        except Exception as e:
            print(f"  [skip] {os.path.basename(f)}: {e}"); continue
        if df.empty or "raw_x" not in df.columns:
            continue
        t = df["time_s"].to_numpy(float)
        raw = df[["raw_x", "raw_y", "raw_z"]].to_numpy(float)
        dt = df["dt_s"].to_numpy(float)
        mode = str(df["filter_mode"].iloc[0]).strip()
        # 전이구간(올리고/내리는) 제외한 중앙 안정 구간 마스크
        mask = central_mask(t, trim_s)
        fps_all = df["fps"].to_numpy(float)
        sel = fps_all[mask] if mask.sum() > 0 else fps_all
        fps = float(sel.mean())
        fps_med = float(np.median(sel))   # 부하 판정은 스파이크에 강건한 median으로
        # 분류·축 선정은 전이구간을 뺀 중앙 진폭으로 (오분류 방지)
        ax, amp = dominant_axis(raw[mask])
        recs.append(dict(name=os.path.basename(f), t=t, raw=raw, dt=dt, mask=mask,
                         mode=mode, fps=fps, fps_med=fps_med, axis=ax, amp=amp))
    return recs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder", nargs="?", default=os.path.dirname(os.path.abspath(__file__)))
    ap.add_argument("--still-threshold", type=float, default=0.4,
                    help="중앙구간 x/y 진폭이 이 값보다 작으면 '정지'로 분류 (정지~0.1, 왕복~0.8+)")
    ap.add_argument("--trim-s", type=float, default=1.5,
                    help="각 녹화 앞뒤로 잘라낼 전이구간 길이(초). 손 올리고/내리는 스윙 제거")
    ap.add_argument("--min-fps", type=float, default=70.0,
                    help="중앙구간 median fps가 이 값 미만이면 시스템 부하 녹화로 보고 제외. "
                         "dt가 튀면 dt 기반 필터/지연이 왜곡됨. 0=제외 안 함")
    ap.add_argument("--min-cutoff", type=float, default=2.0)  # 확정 설정값
    ap.add_argument("--beta", type=float, default=2.0)        # 확정 설정값
    ap.add_argument("--ma-buffer", type=int, default=4)
    ap.add_argument("--ma-deadzone", type=float, default=0.005)
    ap.add_argument("--max-delta", type=float, nargs=3, default=[0.5, 0.5, 0.08],
                    help="프레임당 이상치 클램프 XYZ (Unity 런타임 기본값과 동일). 비활성=큰 값")
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    folder = args.folder
    outdir = args.out or folder
    os.makedirs(outdir, exist_ok=True)
    params = dict(min_cutoff=args.min_cutoff, beta=args.beta,
                  ma_buffer=args.ma_buffer, ma_deadzone=args.ma_deadzone,
                  max_delta=np.array(args.max_delta, float))

    recs = load(folder, args.trim_s)
    if not recs:
        print(f"CSV 없음: {folder}\\gesture_*.csv"); sys.exit(1)

    # 시스템 부하로 fps가 떨어진 녹화 제외 (dt 왜곡 → 필터/지연 오염)
    if args.min_fps > 0:
        dropped = [r for r in recs if r["fps_med"] < args.min_fps]
        recs    = [r for r in recs if r["fps_med"] >= args.min_fps]
        if dropped:
            print(f"\n[부하 제외] median fps < {args.min_fps:.0f} 녹화 {len(dropped)}개 제외:")
            for r in sorted(dropped, key=lambda x: x["name"]):
                print(f"  {r['name']:<46} median_fps={r['fps_med']:>5.1f}")
        if not recs:
            print("  [!] 모든 녹화가 부하 임계값 미만입니다. --min-fps를 낮추세요."); sys.exit(1)

    for r in recs:
        r["type"] = "still" if r["amp"] < args.still_threshold else "motion"

    print(f"\n녹화 {len(recs)}개 (정지 임계값={args.still_threshold}):")
    for r in sorted(recs, key=lambda x: x["name"]):
        print(f"  {r['name']:<46} mode={r['mode']:<14} {r['type']:<7} amp={r['amp']:>6.3f} fps={r['fps']:>5.1f}")
    stills = [r for r in recs if r["type"] == "still"]
    motions = [r for r in recs if r["type"] == "motion"]
    print(f"  -> 정지 {len(stills)}개 / 왕복 {len(motions)}개")
    if not stills:
        print("  [!] 정지 녹화가 없어 '떨림'을 측정할 수 없습니다. 손 가만히 든 녹화 1개를 추가하세요.")
    if not motions:
        print("  [!] 왕복 녹화가 없어 '지연'을 측정할 수 없습니다. 손 좌우로 흔든 녹화 1개를 추가하세요.")
    print(f"\n필터 파라미터: OneEuro(minCutoff={args.min_cutoff}, beta={args.beta}), "
          f"MA(buffer={args.ma_buffer}, deadzone={args.ma_deadzone}), maxDelta={args.max_delta}\n")

    # 각 필터를 모든 raw에 재적용(워밍업 위해 전체 적용 후 측정만 mask 구간).
    # 녹화별로 측정해 mean ± std(ddof=1) → 오차막대.
    rows = []
    for name in FILTERS:
        jit = []
        for r in stills:
            filt = apply_filter(name, r["raw"], r["dt"], params)
            jit.append(jitter_rms(filt[r["mask"]]))
        lag = []
        for r in motions:
            filt = apply_filter(name, r["raw"], r["dt"], params)
            m, a = r["mask"], r["axis"]
            lag.append(estimate_lag_ms(r["t"][m], r["raw"][m, a], filt[m, a]))
        jit = [v for v in jit if np.isfinite(v)]
        lag = [v for v in lag if np.isfinite(v)]
        rows.append(dict(
            filter=name,
            jitter=np.mean(jit) if jit else float("nan"),
            jitter_std=np.std(jit, ddof=1) if len(jit) > 1 else 0.0,
            jitter_n=len(jit),
            lag_ms=np.mean(lag) if lag else float("nan"),
            lag_std=np.std(lag, ddof=1) if len(lag) > 1 else 0.0,
            lag_n=len(lag)))
    summary = pd.DataFrame(rows)

    # FPS는 런타임 측정값 → 실제 녹화 모드별 평균 ± std
    fps_by_mode, fps_std, fps_n = {}, {}, {}
    for m in FILTERS:
        g = [r["fps"] for r in recs if r["mode"] == m]
        fps_by_mode[m] = np.mean(g) if g else float("nan")
        fps_std[m]     = np.std(g, ddof=1) if len(g) > 1 else 0.0
        fps_n[m]       = len(g)
    summary["runtime_fps"] = summary["filter"].map(fps_by_mode)
    summary["fps_std"]     = summary["filter"].map(fps_std)
    summary["fps_n"]       = summary["filter"].map(fps_n)

    summary = summary[["filter", "jitter", "jitter_std", "jitter_n",
                       "lag_ms", "lag_std", "lag_n",
                       "runtime_fps", "fps_std", "fps_n"]]

    pd.set_option("display.float_format", lambda v: f"{v:.4f}")
    print("=== 요약 (jitter/lag = 같은 raw에 오프라인 재적용, mean ± std) ===")
    print(summary.to_string(index=False))
    spath = os.path.join(outdir, "summary.csv")
    summary.to_csv(spath, index=False)
    print(f"\n요약 저장 → {spath}")

    _plot_motion(motions, params, outdir)
    _plot_still(stills, params, outdir)
    _plot_bars(summary, outdir)


def _plot_motion(motions, params, outdir):
    if not motions:
        return
    r = max(motions, key=lambda x: x["amp"])  # 진폭 최대 trace 대표
    a = r["axis"]; m = r["mask"]; t = r["t"][m] - r["t"][m][0]
    fig, axs = plt.subplots(len(FILTERS), 1, figsize=(10, 2.5 * len(FILTERS)), sharex=True)
    for ax, name in zip(axs, FILTERS):
        filt = apply_filter(name, r["raw"], r["dt"], params)
        ax.plot(t, r["raw"][m, a], color="#bbbbbb", lw=1.0, label="raw (webcam input)")
        ax.plot(t, filt[m, a], color=COLORS[name], lw=1.8, label=f"filtered ({name})")
        ax.set_ylabel(f"{AXES[a]} pos"); ax.grid(alpha=0.3)
        ax.set_title(f"{name}  (red lagging behind / smoothing of gray)")
        ax.legend(loc="upper right", fontsize=8)
    axs[-1].set_xlabel("time (s)")
    fig.suptitle("Same hand motion through each filter (raw vs filtered)")
    fig.tight_layout()
    p = os.path.join(outdir, "plot_motion_raw_vs_filt.png")
    fig.savefig(p, dpi=150); plt.close(fig); print(f"  → {p}")


def _plot_still(stills, params, outdir):
    if not stills:
        print("  (정지 녹화 없음 → plot_still_jitter 생략)")
        return
    r = stills[0]; m = r["mask"]; t = r["t"][m] - r["t"][m][0]
    a = 2  # z(깊이) = 가장 노이즈 큰 축 → 필터 차이가 가장 잘 보임
    fig, ax = plt.subplots(figsize=(6.7, 4))   # PPT용 가로 축소 (10의 ~2/3)
    for name in FILTERS:
        filt = apply_filter(name, r["raw"], r["dt"], params)
        sig = filt[m, a]
        # 고역통과: 0.4s 이동평균을 빼서 느린 드리프트 제거, 떨림만 남김
        base = pd.Series(sig).rolling(31, center=True, min_periods=1).mean().to_numpy()
        ax.plot(t, (sig - base) * 1000.0, color=COLORS[name], lw=1.2, label=name)
    ax.set_xlabel("time (s)"); ax.set_ylabel("z jitter, drift removed (×10⁻³ units)")
    ax.set_title("High-frequency jitter while holding still (closer to 0 = smoother)")
    ax.set_xlim(0, 4); ax.set_xticks(np.arange(0, 4.01, 0.5))   # 0~4초, 0.5초 눈금
    ax.legend(); ax.grid(alpha=0.3); fig.tight_layout()
    p = os.path.join(outdir, "plot_still_jitter.png")
    fig.savefig(p, dpi=150); plt.close(fig); print(f"  → {p}")


def _plot_bars(summary, outdir):
    fig, axes = plt.subplots(1, 3, figsize=(13, 4))
    s = summary.set_index("filter")
    cols = [COLORS[m] for m in s.index]
    ebar = dict(capsize=5, error_kw=dict(elinewidth=1.2, capthick=1.2))
    nj = int(s["jitter_n"].max()) if s["jitter_n"].notna().any() else 0
    nl = int(s["lag_n"].max())    if s["lag_n"].notna().any()    else 0
    if s["jitter"].notna().any():
        axes[0].bar(s.index, s["jitter"] * 1000.0,
                    yerr=s["jitter_std"].fillna(0) * 1000.0, color=cols, **ebar)
    axes[0].set_title(f"Jitter (lower=better, n={nj})"); axes[0].set_ylabel("RMS dev (×10⁻³ units)")
    if s["lag_ms"].notna().any():
        axes[1].bar(s.index, s["lag_ms"], yerr=s["lag_std"].fillna(0), color=cols, **ebar)
    axes[1].set_title(f"Lag (lower=better, n={nl})"); axes[1].set_ylabel("ms")
    axes[2].bar(s.index, s["runtime_fps"], yerr=s["fps_std"].fillna(0), color=cols, **ebar)
    axes[2].set_title("Runtime FPS (similar=no perf cost)"); axes[2].set_ylabel("FPS")
    for ax in axes:
        ax.grid(axis="y", alpha=0.3)
    fig.tight_layout()
    p = os.path.join(outdir, "plot_bars.png")
    fig.savefig(p, dpi=150); plt.close(fig); print(f"  → {p}")


if __name__ == "__main__":
    main()

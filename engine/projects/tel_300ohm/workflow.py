"""项目A: 电话滤波器 300Ω (示波器法). 由 origin_scripts/osc_*.py 提炼.

流程: 直通校准(Thru Vref) -> 多轮 DUT 测量, 纯插损 = 20*log10(Vout/Vref).
每频点前检查暂停/停止, 轮间等待 C# 确认(换 DUT).
"""
import math
import time
from pathlib import Path

import numpy as np

from ...protocol import Control, emit, log


def _db(v_out: float, v_ref: float) -> float:
    if v_out > 0 and v_ref > 0:
        return 20 * math.log10(v_out / v_ref)
    return -999.0


def run(cfg: dict, binding: dict, out_dir: Path, ctrl: Control,
        open_instruments, auto_confirm: bool, labels: list, demo: bool):
    steps = int(cfg["steps"])
    awg_vpp = float(cfg["awg_vpp"])
    init_delay = float(cfg["init_delay"])
    autoset_delay = float(cfg["autoset_delay"])
    freqs = np.logspace(np.log10(cfg["start_hz"]), np.log10(cfg["stop_hz"]), steps)

    awg, scope = open_instruments("tel_300ohm", binding, demo, awg_vpp)
    if demo:  # demo 不等待 autoset
        init_delay = min(init_delay, 0.01)
        autoset_delay = min(autoset_delay, 0.01)

    # ---- 第一步: 直通校准 ----
    emit({"type": "phase", "phase": "thru_confirm",
          "msg": "请将 AWG(串250Ω)直连示波器CH2(并300Ω),确认后开始校准"})
    ctrl.wait_confirm("thru_confirm", "请连接直通, 确认后开始校准", auto=auto_confirm)
    if ctrl.stopped:
        return []
    awg.output(True)
    v_ref_list = []
    emit({"type": "phase", "phase": "thru_run", "msg": "直通校准扫描中..."})
    for i, f in enumerate(freqs):
        ctrl.wait_if_paused()
        if ctrl.stopped:
            return []
        awg.set_freq(float(f))
        scope.track_freq(float(f), init_delay, autoset_delay)
        v = scope.read_amplitude()
        v_ref_list.append(v)
        thru_loss = (20 * math.log10(2 * v / awg_vpp) if v > 0 else -999.0)
        emit({"type": "point", "kind": "thru", "round": 0, "index": i,
              "freq": float(f), "v_ref": v, "thru_loss_db": thru_loss})
    awg.output(False)
    log("直通校准完成, 基准已存储")
    emit({"type": "phase", "phase": "thru_done", "msg": "直通校准完成"})

    # ---- 第二步: 多轮 DUT ----
    results_all = []
    n_rounds = max(len(labels), 1)
    for r in range(n_rounds):
        label = labels[r] if r < len(labels) else f"DUT_{r + 1}"
        emit({"type": "phase", "phase": "dut_confirm",
              "msg": f"请接入 {label}, 确认后开始第{r + 1}轮测量"})
        got = ctrl.wait_confirm(f"dut_{r + 1}", f"请接入 {label}", auto=auto_confirm,
                                auto_label=label)
        if ctrl.stopped:
            break
        if got:
            label = got
        if hasattr(scope, "thru"):
            scope.thru = False  # demo: 切到 DUT 响应
        awg.output(True)
        emit({"type": "phase", "phase": "dut_run",
              "msg": f"第{r + 1}轮 ({label}) 测量中..."})
        rows = []
        for i, f in enumerate(freqs):
            ctrl.wait_if_paused()
            if ctrl.stopped:
                break
            awg.set_freq(float(f))
            scope.track_freq(float(f), init_delay, autoset_delay)
            v_out = scope.read_amplitude()
            v_ref = v_ref_list[i]
            loss = _db(v_out, v_ref)
            rows.append((float(f), v_ref, v_out, loss))
            emit({"type": "point", "kind": "dut", "round": r + 1, "label": label,
                  "index": i, "freq": float(f), "v_ref": v_ref,
                  "v_out": v_out, "loss_db": loss})
        awg.output(False)
        if ctrl.stopped:
            break
        results_all.append({"label": label, "rows": rows})
        emit({"type": "round_done", "round": r + 1, "label": label})

    _save_report(out_dir, freqs, v_ref_list, awg_vpp, results_all)
    return results_all


def _save_report(out_dir: Path, freqs, v_ref_list, awg_vpp, results_all):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.ticker import FuncFormatter
    out_dir.mkdir(parents=True, exist_ok=True)

    # raw.csv
    with open(out_dir / "raw.csv", "w", encoding="utf-8") as f:
        f.write("round,label,freq_hz,v_ref_v,v_out_v,loss_db\n")
        for r, res in enumerate(results_all, 1):
            for freq, v_ref, v_out, loss in res["rows"]:
                f.write(f"{r},{res['label']},{freq:.3f},{v_ref:.6f},{v_out:.6f},{loss:.3f}\n")

    def fmt(x, pos):
        return f"{int(x / 1000)}k" if x >= 1000 else f"{int(x)}"

    for r, res in enumerate(results_all, 1):
        thru, raw, cal = [], [], []
        for i, (freq, v_ref, v_out, loss) in enumerate(res["rows"]):
            thru.append(20 * math.log10(2 * v_ref_list[i] / awg_vpp) if v_ref_list[i] > 0 else -999)
            raw.append(20 * math.log10(2 * v_out / awg_vpp) if v_out > 0 else -999)
            cal.append(loss)
        fh = [x[0] for x in res["rows"]]
        plt.figure(figsize=(12, 7))
        plt.semilogx(fh, raw, "b-o", label="未校准", linewidth=2)
        plt.semilogx(fh, thru, "g--s", label="直通", linewidth=2)
        plt.semilogx(fh, cal, "r-^", label="校准后", linewidth=2)
        plt.title(f"滤波器衰减曲线 - {res['label']} (第{r}轮)", fontweight="bold")
        plt.xlabel("频率 (Hz, 对数)")
        plt.ylabel("衰减 (dB)")
        plt.gca().xaxis.set_major_formatter(FuncFormatter(fmt))
        plt.grid(True, which="both", linestyle="--", alpha=0.7)
        plt.legend()
        plt.tight_layout()
        plt.savefig(out_dir / f"curve_round{r}.png", dpi=150)
        plt.close()

    files = ["raw.csv"] + [f"curve_round{r}.png" for r in range(1, len(results_all) + 1)]
    emit({"type": "result", "out_dir": str(out_dir), "files": files})

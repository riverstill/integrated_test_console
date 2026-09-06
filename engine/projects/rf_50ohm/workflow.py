"""项目B: 射频滤波器 50Ω (频谱仪法). 由 origin_scripts/spec_*.py 提炼.

流程: 无直通, 直接多轮电路测量(每轮一标签), 输出多sheet Excel + 对比图.
输入功率 Pin(dBm) = 10*log10((Vpp/2)^2 / 0.1), 衰减 = Pin - Pout.
"""
import time
from pathlib import Path

import numpy as np

from ...protocol import Control, emit, log


def run(cfg: dict, binding: dict, out_dir: Path, ctrl: Control,
        open_instruments, auto_confirm: bool, labels: list, demo: bool):
    import numpy as _np
    points = int(cfg["points"])
    awg_vpp = float(cfg["awg_vpp"])
    pin_dbm = 10 * _np.log10((awg_vpp / 2) ** 2 / 0.1)
    freqs = np.logspace(np.log10(cfg["start_hz"]), np.log10(cfg["stop_hz"]), points).astype(int)

    awg, spec = open_instruments("rf_50ohm", binding, demo, awg_vpp)
    if hasattr(spec, "pin_dbm"):
        spec.pin_dbm = float(pin_dbm)
    awg.output(True)

    all_meas = []
    n_rounds = max(len(labels), 1)
    for r in range(n_rounds):
        label = labels[r] if r < len(labels) else f"Circuit_{r + 1}"
        emit({"type": "phase", "phase": "dut_confirm",
              "msg": f"请连接 {label}, 确认后开始第{r + 1}轮测量"})
        got = ctrl.wait_confirm(f"dut_{r + 1}", f"请连接 {label}", auto=auto_confirm,
                                auto_label=label)
        if ctrl.stopped:
            break
        if got:
            label = got
        emit({"type": "phase", "phase": "dut_run",
              "msg": f"第{r + 1}轮 ({label}) 测量中..."})
        fs, pouts, atts = [], [], []
        for i, f in enumerate(freqs):
            ctrl.wait_if_paused()
            if ctrl.stopped:
                break
            f = int(f)
            awg.set_freq(f)
            p = spec.measure_at(f, span=float(cfg["span"]), settle=float(cfg["settle"]))
            att = float(pin_dbm) - float(p)
            fs.append(f)
            pouts.append(float(p))
            atts.append(att)
            emit({"type": "point", "kind": "dut", "round": r + 1, "label": label,
                  "index": i, "freq": f, "pout_dbm": float(p), "att_db": att})
        if ctrl.stopped:
            break
        all_meas.append({"label": label, "freqs": fs, "pouts": pouts, "atts": atts})
        emit({"type": "round_done", "round": r + 1, "label": label})

    try:
        awg.output(False)
    except Exception:
        pass
    _save_report(out_dir, cfg, all_meas)
    return all_meas


def _save_report(out_dir: Path, cfg: dict, all_meas: list):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    import openpyxl
    from matplotlib.ticker import FuncFormatter
    out_dir.mkdir(parents=True, exist_ok=True)

    wb = openpyxl.Workbook()
    wb.remove(wb.active)
    for m in all_meas:
        ws = wb.create_sheet((m["label"] or "Circuit")[:31])
        ws.append(["频率 (Hz)", "输出功率 (dBm)", "衰减 (dB)"])
        for f, p, a in zip(m["freqs"], m["pouts"], m["atts"]):
            ws.append([f, p, a])
    wb.save(out_dir / "Filter_Insertion_Loss_Report.xlsx")

    with open(out_dir / "raw.csv", "w", encoding="utf-8") as f:
        f.write("round,label,freq_hz,pout_dbm,att_db\n")
        for r, m in enumerate(all_meas, 1):
            for fq, p, a in zip(m["freqs"], m["pouts"], m["atts"]):
                f.write(f"{r},{m['label']},{fq},{p:.3f},{a:.3f}\n")

    def fmt(x, pos):
        return f"{int(x)}M" if x >= 1000 else (f"{int(x)}k" if x >= 1 else f"{x:.0f}")

    plt.figure(figsize=(14, 8))
    colors = ["red", "blue", "green", "orange", "purple", "brown", "pink", "gray", "cyan", "magenta"]
    for i, m in enumerate(all_meas):
        fk = [f / 1000 for f in m["freqs"]]
        plt.semilogx(fk, m["atts"], color=colors[i % len(colors)],
                     linewidth=2.5, marker="o", markersize=5, label=m["label"], alpha=0.85)
    plt.title(f"滤波器衰减对比 ({cfg['start_hz'] / 1000:.0f}kHz - {cfg['stop_hz'] / 1e6:.0f}MHz)", fontweight="bold")
    plt.xlabel("频率 (kHz, 对数)")
    plt.ylabel("衰减 (dB)")
    plt.gca().xaxis.set_major_formatter(FuncFormatter(fmt))
    plt.grid(True, which="both", linestyle="--", alpha=0.7)
    plt.legend(loc="best")
    plt.tight_layout()
    plt.savefig(out_dir / "Filter_Curve.png", dpi=150)
    plt.close()

    emit({"type": "result", "out_dir": str(out_dir),
          "files": ["raw.csv", "Filter_Insertion_Loss_Report.xlsx", "Filter_Curve.png"]})

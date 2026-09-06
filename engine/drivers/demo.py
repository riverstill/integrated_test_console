"""虚拟仪器: 无硬件时跑通全流程 (C# Demo 模式 / CI 验证).

响应曲线贴近真实滤波器, 让 UI/报告链路可完整验证.
"""
import math
import random


class _Base:
    def close(self):
        pass


class DemoAwg(_Base):
    def __init__(self):
        self.freq = 1000.0
        self.vpp = 2.0

    def setup_sine(self, vpp, load="HZ"):
        self.vpp = vpp

    def set_freq(self, f):
        self.freq = f

    def output(self, on):
        pass


class DemoScope(_Base):
    """模拟: 直通分压 + 一阶低通(fc=4kHz) DUT."""

    def __init__(self, awg: DemoAwg, thru: bool = True):
        self._awg = awg
        self.thru = thru
        self.series_r = 250.0
        self.shunt_r = 300.0
        self.fc = 4000.0

    def setup_ch2_amplitude(self):
        pass

    def track_freq(self, freq_hz, init_delay, autoset_delay):
        pass  # demo 不等待

    def read_amplitude(self) -> float:
        v_set = self._awg.vpp / 2  # 峰值
        # 直通分压
        v = v_set * self.shunt_r / (self.series_r + self.shunt_r)
        if not self.thru:
            f = self._awg.freq
            h = 1.0 / math.sqrt(1 + (f / self.fc) ** 2)
            v *= h
        v *= 1 + random.gauss(0, 0.003)
        return max(v, 1e-6)


class DemoSpec(_Base):
    """模拟: 输入功率 - 低通衰减 + 底噪."""

    def __init__(self, awg: DemoAwg):
        self._awg = awg
        self.pin_dbm = 0.0

    def setup_narrowband(self, rbw=10, vbw=10, att=0):
        pass

    def measure_at(self, freq_hz, span=100, settle=0.05) -> float:
        f = freq_hz
        # 三阶低通近似, fc=2MHz
        att = 30 * math.log10(1 + (f / 2e6) ** 3) + random.gauss(0, 0.15)
        floor = -90.0
        return max(self.pin_dbm - att, floor)

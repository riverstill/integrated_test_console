"""Siglent SSA 频谱仪驱动 (项目B)."""
import time


class SiglentSsaSpec:
    def __init__(self, visa_res):
        self._r = visa_res

    def setup_narrowband(self, rbw: float = 10, vbw: float = 10, att: float = 0):
        self._r.write("INIT:CONT OFF")
        self._r.write(f"BAND:RES {rbw:g}")
        self._r.write(f"BAND:VID {vbw:g}")
        self._r.write(f"POW:ATT {att:g}")
        self._r.write("SENS:DET:POW AVER")

    def measure_at(self, freq_hz: int, span: float = 100,
                   settle: float = 0.05) -> float:
        self._r.write(f"SENS:FREQ:CENT {freq_hz}")
        self._r.write(f"SENS:FREQ:SPAN {span:g}")
        time.sleep(settle)
        self._r.write("INIT:IMM")
        self._r.query("*OPC?")
        self._r.write("CALCulate:MARKer1:MAXimum")
        time.sleep(0.01)
        return float(self._r.query("CALCulate:MARKer1:Y?"))

    def close(self):
        self._r.close()

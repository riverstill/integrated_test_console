"""Tek TBS2000 示波器驱动 (项目A)."""
import time


class TekTbsScope:
    def __init__(self, visa_res):
        self._r = visa_res

    def setup_ch2_amplitude(self):
        self._r.write("CH1:SELect OFF")
        self._r.write("CH2:SELect ON")
        self._r.write("CH2:COUPLing AC")
        self._r.write("MEASUrement:IMMed:SOURce1 CH2")
        self._r.write("MEASUrement:IMMed:TYPe AMPlitude")

    def track_freq(self, freq_hz: float, init_delay: float, autoset_delay: float):
        period = 1.0 / freq_hz
        self._r.write(f"HORizontal:SCAle {period * 2 / 10}")
        self._r.write("CH2:SCAle 0.2")
        time.sleep(init_delay)
        self._r.write("AUTOSet EXECute")
        time.sleep(autoset_delay)

    def read_amplitude(self) -> float:
        return float(self._r.query("MEASUrement:IMMed:VALue?").strip())

    def close(self):
        self._r.close()

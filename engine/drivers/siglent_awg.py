"""Siglent AWG 通用驱动 (两项目共用, SCPI: C1:BSWV...)."""


class SiglentAwg:
    def __init__(self, visa_res):
        self._r = visa_res

    def setup_sine(self, vpp: float, load: str = "HZ"):
        self._r.write("C1:OUTP OFF")
        self._r.write("C1:BSWV WVTP,SINE")
        self._r.write(f"C1:BSWV AMP,{vpp}")
        # load: "HZ" -> Hi-Z, "50" -> 50Ω
        self._r.write(f"C1:OUTP LOAD,{load}")

    def set_freq(self, freq_hz: float):
        self._r.write(f"C1:BSWV FRQ,{freq_hz:g}")

    def output(self, on: bool):
        self._r.write(f"C1:OUTP {'ON' if on else 'OFF'}")

    def close(self):
        try:
            self.output(False)
        finally:
            self._r.close()

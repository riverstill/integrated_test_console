"""VISA 扫描 + *IDN? 自动识别. C# 框图仪器选择即调 `scan` 命令."""
import re

# 项目需要的仪器类型 -> IDN 匹配正则 (用于自动推荐)
INSTRUMENT_PATTERNS = {
    "awg_siglent": [r"SDG", r"Siglent.*SDG"],
    "scope_tek": [r"TBS\s?2", r"TEKTRONIX.*TBS"],
    "spec_siglent": [r"SSA", r"Siglent.*SSA"],
}


def match_type(idn: str) -> str:
    for itype, patterns in INSTRUMENT_PATTERNS.items():
        for pat in patterns:
            if re.search(pat, idn, re.IGNORECASE):
                return itype
    return "unknown"


def scan(demo: bool = False) -> list:
    """返回 [{visa, idn, itype}]. demo=True 或无 VISA 后端时返回虚拟设备."""
    if demo:
        return [
            {"visa": "DEMO::AWG::INSTR", "idn": "Siglent,SDG2042X,DEMO,1.0", "itype": "awg_siglent"},
            {"visa": "DEMO::SCOPE::INSTR", "idn": "TEKTRONIX,TBS2102B,DEMO,1.0", "itype": "scope_tek"},
            {"visa": "DEMO::SPEC::INSTR", "idn": "Siglent,SSA3021X,DEMO,1.0", "itype": "spec_siglent"},
        ]
    try:
        import pyvisa
    except ImportError:
        return scan(demo=True)
    try:
        rm = pyvisa.ResourceManager()
        resources = list(rm.list_resources())
    except Exception:
        return scan(demo=True)
    out = []
    for res in resources:
        try:
            inst = rm.open_resource(res)
            inst.timeout = 3000
            idn = inst.query("*IDN?").strip()
            inst.close()
        except Exception as e:
            idn = f"<不可达: {e}>"
        out.append({"visa": res, "idn": idn, "itype": match_type(idn)})
    try:
        rm.close()
    except Exception:
        pass
    return out

"""engine CLI: C# PyRunner 调用的唯一入口 (系统 python -u -m engine ...).

命令:
  list_projects
  get_schema --project <id>
  get_diagram --project <id>
  scan [--demo]
  run --project <id> --config cfg.json --binding bind.json --out dir
      [--demo] [--auto-confirm] [--labels L1,L2] [--rounds N]
  pack --project <id> --out x.itcpkg
  inspect_pkg --pkg x.itcpkg
  install_pkg --pkg x.itcpkg [--force]
"""
import argparse
import importlib
import json
import sys
from pathlib import Path

# 全链路 UTF-8: Windows 控制台代码页 (cp1252/GBK) 下 print 中文也不崩.
# C# 侧 PyRunner 已同步设置 StandardOutput/InputEncoding = UTF8.
for _s in (sys.stdin, sys.stdout, sys.stderr):
    try:
        _s.reconfigure(encoding="utf-8", errors="backslashreplace")
    except Exception:
        pass


def cmd_list_projects(_):
    from .registry import list_projects
    print(json.dumps(list_projects(), ensure_ascii=False, indent=2))


def cmd_get_schema(a):
    from .registry import get_project, load_json
    p = get_project(a.project)
    print(Path(p["_dir"], "config_schema.json").read_text(encoding="utf-8"))


def cmd_get_diagram(a):
    from .registry import get_project
    p = get_project(a.project)
    print(Path(p["_dir"], "diagram.json").read_text(encoding="utf-8"))


def cmd_scan(a):
    from .scanner import scan
    print(json.dumps(scan(demo=a.demo), ensure_ascii=False, indent=2))


def _load_json_or_default(path: str, project_dir: str) -> dict:
    if path:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    return json.loads(Path(project_dir, "default_config.json").read_text(encoding="utf-8"))


def open_instruments_factory(demo: bool):
    """返回 open(project_id, binding, demo, awg_vpp) -> (awg, second)."""
    def _open(project_id: str, binding: dict, is_demo: bool, awg_vpp: float):
        use_demo = is_demo or binding.get("demo") or \
            str(binding.get("awg", "")).startswith("DEMO")
        if use_demo:
            from .drivers.demo import DemoAwg, DemoScope, DemoSpec
            awg = DemoAwg()
            awg.setup_sine(awg_vpp, "HZ" if project_id == "tel_300ohm" else "50")
            if project_id == "tel_300ohm":
                return awg, DemoScope(awg, thru=True)
            spec = DemoSpec(awg)
            return awg, spec
        import pyvisa
        rm = pyvisa.ResourceManager()
        from .drivers.siglent_awg import SiglentAwg
        awg_res = rm.open_resource(binding["awg"])
        awg_res.timeout = 5000
        awg = SiglentAwg(awg_res)
        if project_id == "tel_300ohm":
            from .drivers.tek_tbs import TekTbsScope
            load = "HZ"
            awg.setup_sine(awg_vpp, load)
            sc_res = rm.open_resource(binding["scope"])
            sc_res.timeout = 5000
            scope = TekTbsScope(sc_res)
            scope.setup_ch2_amplitude()
            return awg, scope
        else:
            from .drivers.siglent_ssa import SiglentSsaSpec
            awg.setup_sine(awg_vpp, "50")
            sp_res = rm.open_resource(binding["spec"])
            sp_res.timeout = 5000
            spec = SiglentSsaSpec(sp_res)
            return awg, spec
    return _open


def cmd_run(a):
    from .protocol import Control, emit, log, start_stdin_listener
    from .registry import get_project
    p = get_project(a.project)
    cfg = _load_json_or_default(a.config, p["_dir"])
    binding = json.loads(Path(a.binding).read_text(encoding="utf-8")) if a.binding else {"demo": True}
    out_dir = Path(a.out or f"results/{a.project}")
    labels = [s for s in (a.labels.split(",") if a.labels else []) if s]
    if not labels and a.rounds:
        labels = [f"Round_{i + 1}" for i in range(a.rounds)]

    ctrl = Control()
    if not a.auto_confirm:
        start_stdin_listener(ctrl)  # 交互模式: C# 经 stdin 发 pause/resume/stop/confirm

    emit({"type": "phase", "phase": "start",
          "msg": f"开始 {p['name']}, 配置={cfg}, 输出={out_dir}"})
    try:
        mod = importlib.import_module(f"engine.projects.{a.project}.workflow")
        res = mod.run(cfg, binding, out_dir, ctrl,
                      open_instruments_factory(a.demo or binding.get("demo", False)),
                      auto_confirm=a.auto_confirm, labels=labels, demo=bool(a.demo))
        if ctrl.stopped:
            emit({"type": "phase", "phase": "stopped", "msg": "已终止, 仪器已安全关闭"})
        else:
            emit({"type": "done", "msg": f"完成, 共{len(res)}轮, 结果见 {out_dir}"})
    except Exception as e:
        import traceback
        emit({"type": "error", "msg": f"{e}\n{traceback.format_exc()}"})
        sys.exit(1)


def cmd_pack(a):
    from .packaging import pack
    from .registry import get_project
    p = get_project(a.project)
    out = pack(Path(p["_dir"]), Path(a.out or f"{a.project}.itcpkg"))
    print(json.dumps({"ok": True, "file": str(out)}, ensure_ascii=False))


def cmd_inspect_pkg(a):
    from .packaging import inspect
    print(json.dumps(inspect(Path(a.pkg)), ensure_ascii=False, indent=2))


def cmd_install_pkg(a):
    from .packaging import install
    from .registry import PROJECTS_DIR
    target = install(Path(a.pkg), PROJECTS_DIR, force=a.force)
    print(json.dumps({"ok": True, "dir": str(target)}, ensure_ascii=False))


def main(argv=None):
    ap = argparse.ArgumentParser(prog="engine")
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list_projects")
    g = sub.add_parser("get_schema"); g.add_argument("--project", required=True)
    g = sub.add_parser("get_diagram"); g.add_argument("--project", required=True)
    g = sub.add_parser("scan"); g.add_argument("--demo", action="store_true")
    g = sub.add_parser("run")
    g.add_argument("--project", required=True)
    g.add_argument("--config", default="")
    g.add_argument("--binding", default="")
    g.add_argument("--out", default="")
    g.add_argument("--demo", action="store_true")
    g.add_argument("--auto-confirm", action="store_true")
    g.add_argument("--labels", default="")
    g.add_argument("--rounds", type=int, default=0)
    g = sub.add_parser("pack"); g.add_argument("--project", required=True)
    g.add_argument("--out", default="")
    g = sub.add_parser("inspect_pkg"); g.add_argument("--pkg", required=True)
    g = sub.add_parser("install_pkg"); g.add_argument("--pkg", required=True)
    g.add_argument("--force", action="store_true")
    a = ap.parse_args(argv)
    {"list_projects": cmd_list_projects, "get_schema": cmd_get_schema,
     "get_diagram": cmd_get_diagram, "scan": cmd_scan,
     "run": cmd_run, "pack": cmd_pack,
     "inspect_pkg": cmd_inspect_pkg, "install_pkg": cmd_install_pkg}[a.cmd](a)


if __name__ == "__main__":
    main()

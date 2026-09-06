"""项目注册表: 扫描 engine/projects/*/project.json, C# list_projects 即调此."""
import json
from pathlib import Path

PROJECTS_DIR = Path(__file__).parent / "projects"


def list_projects() -> list:
    out = []
    if not PROJECTS_DIR.exists():
        return out
    for d in sorted(PROJECTS_DIR.iterdir()):
        pj = d / "project.json"
        if pj.exists():
            try:
                meta = json.loads(pj.read_text(encoding="utf-8"))
                meta["_dir"] = str(d)
                out.append(meta)
            except Exception:
                continue
    return out


def get_project(pid: str) -> dict:
    for p in list_projects():
        if p.get("id") == pid:
            return p
    raise KeyError(f"未知测试项目: {pid}")


def load_json(project_dir: str, name: str) -> dict:
    return json.loads((Path(project_dir) / name).read_text(encoding="utf-8"))

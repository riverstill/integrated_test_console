"""测试项目包 (.itcpkg, 实为 zip): 打包分发 + 安装.

包结构 (顶层即项目目录内容):
  project.json  diagram.json  config_schema.json  default_config.json
  workflow.py   __init__.py   (可选 README.md)

安装 = 校验后解压到 engine/projects/<id>/, 与内置项目同等对待,
C# 无需任何改动, 重启即出现在项目列表.
"""
import json
import shutil
import zipfile
from pathlib import Path

REQUIRED = ["project.json", "diagram.json", "config_schema.json",
            "default_config.json", "workflow.py", "__init__.py"]

PKG_SUFFIX = ".itcpkg"


def pack(project_dir: Path, out_file: Path) -> Path:
    project_dir = Path(project_dir)
    missing = [f for f in REQUIRED if not (project_dir / f).exists()]
    if missing:
        raise ValueError(f"项目目录缺少文件: {missing}")
    meta = json.loads((project_dir / "project.json").read_text(encoding="utf-8"))
    if "id" not in meta or "name" not in meta:
        raise ValueError("project.json 必须包含 id/name")
    out_file = Path(out_file)
    if out_file.suffix != PKG_SUFFIX:
        out_file = out_file.with_suffix(PKG_SUFFIX)
    with zipfile.ZipFile(out_file, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(project_dir.iterdir()):
            if f.is_file():
                z.write(f, f.name)
    return out_file


def inspect(pkg_file: Path) -> dict:
    """不安裝, 只读包内 project.json 摘要 (供 C# 安装前预览)."""
    with zipfile.ZipFile(pkg_file) as z:
        names = z.namelist()
        missing = [f for f in REQUIRED if f not in names]
        if missing:
            raise ValueError(f"项目包缺少文件: {missing}")
        return json.loads(z.read("project.json").decode("utf-8"))


def install(pkg_file: Path, projects_dir: Path, force: bool = False) -> Path:
    meta = inspect(Path(pkg_file))
    pid = meta["id"]
    if not pid.replace("_", "").replace("-", "").isalnum():
        raise ValueError(f"非法项目 id: {pid}")
    target = Path(projects_dir) / pid
    if target.exists() and not force:
        raise FileExistsError(f"项目已存在: {pid} (加 --force 覆盖)")
    tmp = target.parent / (pid + ".tmp_install")
    shutil.rmtree(tmp, ignore_errors=True)
    with zipfile.ZipFile(pkg_file) as z:
        z.extractall(tmp)
    if target.exists():
        shutil.rmtree(target)
    tmp.rename(target)
    return target

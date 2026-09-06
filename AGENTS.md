# AGENTS.md — integrated_test_console

Hybrid app: **C# WPF shell** (`Console.WPF/`, generic, never per-project code) +
**Python measurement engine** (`engine/`, owns all SCPI/hardware). Contract = JSON over
stdio; C# spawns `python -u -m engine <cmd>`. Do NOT mix: new test logic goes in Python only.

## Python engine

- No `pip` on this box — use `uv run --with pyvisa --with pyvisa-py --with numpy --with matplotlib --with openpyxl python ...`
- CLI: `list_projects | get_schema | get_diagram | scan [--demo] | run ... | pack | inspect_pkg | install_pkg`. Demo mode (`--demo`) uses `engine/drivers/demo.py`, no hardware needed.
- **CWD must be the repo root** (parent of `engine/`) for `-m engine` to resolve. The C# `EngineRoot` bug (WorkingDirectory one level too deep → `No module named engine`) already bit once — keep this invariant.
- **UTF-8 both ends**: `engine/__main__.py` reconfigures stdio to utf-8; `PyRunner` sets `Standard*Encoding = UTF8`. English-Windows consoles (cp1252) crash on raw Chinese `print` otherwise.
- Test a full run: `python -u -m engine run --project tel_300ohm --demo --auto-confirm --labels T --out /tmp/x` → expect `point` JSONL + `raw.csv`/`curve_*.png`; rf_50ohm also emits xlsx.
- `.itcpkg` = zip with exactly `project.json diagram.json config_schema.json default_config.json workflow.py __init__.py` at top level (`engine/packaging.py`). Prebuilt packs live in `packages/`.

## C# WPF (no dotnet on this Linux box — author only, build on Windows/CI)

- `gh run list/watch` in repo root; CI (`.github/workflows/build.yml`) builds on `windows-latest` + runs engine smoke. Always watch a push to green.
- Pinned `ScottPlot.WPF 4.1.68` (v5 API incompatible). v4 XAML namespace is `clr-namespace:ScottPlot;assembly=ScottPlot.WPF`. v4 has **no log axis** — `RunView` plots `log10(Hz)` manually. `Plot.Plot.Style(...)` for theme colors.
- `ImplicitUsings` is on. JSON strings: use `GetValue<string>()`, never `ToString()` (adds quotes).
- **Every XAML-referenced asset must be declared**: loose `app.ico` with only `<ApplicationIcon>` caused startup `XamlParseException: 找不到资源"app.ico"`. CI asserts via `msbuild -getItem:Resource/Page` — extend that step if you add assets (pwsh can't `LoadFrom` WPF assemblies, don't try).
- Themes (`Themes/Light|Dark.xaml`) are swapped wholesale; views must use `{DynamicResource …}` (never hardcoded `Gray`), code-behind via `FindResource`. `ThemeManager.Changed` event exists for code-drawn content.
- Startup crash safety: `App` global handlers write `%AppData%/IntegratedTestConsole/crash.log`. Don't add eager-loading controls to startup path — `HelpView` navigates lazily on tab select for this reason.
- Settings persist in `%AppData%/…/settings.json`; keep the record backward-compatible (missing fields → defaults).

## Release checklist

Bump all three or update-check lies: csproj `<Version>`, `engine/__init__.py::__version__`, update manifest (`update_manifest.example.json` shape). `HELP.html` ships beside the exe; keep it in sync with behavior changes.

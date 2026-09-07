# 集成测试控制台 — Windows 运行说明

目标机要求: Windows 10/11 + 系统 Python 3.9+ (已装, PATH 可用) + NI-VISA 或 Keysight VISA.

## 0. 安装（二选一）

- **安装程序（推荐）**：运行 `IntegratedTestConsole-Setup-<版本>.exe`，向导装到
  `Program Files\IntegratedTestConsole`，自动建开始菜单/桌面快捷方式。
  安装时若提示缺 Python，可继续装，跑测试前补上即可。
- **绿色包**：解压 `IntegratedTestConsole-win64` artifact，双击 `Console.WPF.exe`。

### “未知发布者”说明

首次运行若 SmartScreen 提示“未知发布者”，原因是程序**没有购买代码签名证书**
（个人/小团队常见情况，非病毒）。三种处理：

1. 单机放行：点“更多信息”→“仍要运行”。
2. 内部分发根治：用自签名证书签名 + 组策略把证书推到各机的“受信任的发布者”。
3. 彻底消除：购买 OV/EV 代码签名证书，把 `CODE_SIGN_PFX`（pfx 的 base64）和
   `CODE_SIGN_PASSWORD` 配进 GitHub Secrets，CI 会自动签名 exe 和安装包。

## 1. 安装 Python 依赖 (系统 Python)

```bat
cd IntegratedTestConsole
py -3 -m pip install -r engine\requirements.txt
```

## 2. 编译 C# 启动器 (开发机, 需 .NET 8 SDK)

```bat
cd Console.WPF
dotnet build IntegratedTestConsole.sln -c Release
```

发布单文件 (Framework-dependent, 体积最小 ~15MB):

```bat
dotnet publish Console.WPF\Console.WPF.csproj -c Release -o publish
```

## 3. 目录摆放

```
IntegratedTestConsole\
  Console.WPF\publish\Console.WPF.exe   <- 双击启动
  engine\                                <- 与 exe 同级(或源码相对路径, App.xaml.cs 自动探测)
  configs\                               <- 测试配置存档
  results\                               <- 运行结果
```

## 4. 使用流程

1. 启动 exe, 点“Python自检”.
2. 第1页「测试系统」：左栏选测试项目 → 中栏点框图仪器节点绑定 VISA（★为推荐；先点右栏“扫描仪器”）→ 右栏确认绑定状态。无硬件时勾顶部 Demo。
3. 第2页「测试运行」：左栏调测试配置（可存文件复用）→ 中栏填轮标签（`DUT_1,DUT_2`）和输出目录，点开始；支持暂停/继续/确认下一步/终止 → 右栏看轮次、导出曲线、打开报告/历史会话（`raw.csv / *.xlsx / *.png`）。

## 5. 新增测试项目 (无需改 C#，两种方式)

- **A. 项目包 (推荐)**：程序第1页“安装项目包(.itcpkg)”导入；`packages/` 目录自带两个内置项目的包；导出同样在第1页。
- **B. 源码目录**：复制 `engine\projects\rf_50ohm\` 为新文件夹, 改 `project.json / diagram.json /
config_schema.json / workflow.py (实现 run(cfg, binding, out_dir, ctrl, open_fn, auto_confirm, labels, demo))`,
用 `python -u -m engine pack --project <id>` 打包分发，重启 exe 即自动出现在项目列表.

## 5b. 设置 / 主题 / 帮助 / 更新

- “设置”页：主题（跟随系统/浅色/深色）、Python 命令、更新源 manifest 地址、检查更新。
- 主题免编译修改：改 exe 旁 `Themes/Light.xaml` / `Dark.xaml`（纯文本色值+样式），设置页点“重新加载主题”即生效；删掉文件则回退内置主题。
- “帮助”页：原生简要说明 + 按钮在外部浏览器打开 `HELP.html` 完整离线帮助（与 exe 同级）。
- 发版三处版本号同步：csproj `<Version>`、`engine/__init__.py`、`update_manifest.json`。

## 6. 故障排查

| 现象 | 原因 / 处理 |
|---|---|
| 顶部状态条红色 “找不到引擎 …” | `engine` 文件夹不在 exe 旁边。解压 artifact 时保持目录结构：`Console.WPF.exe` 与 `engine/` 同级 |
| “引擎调用失败 … exit=9009 / 'py' 不是内部命令” | 顶部 Python 输入框改成 `python`（或点“自动检测”），路径会自动记住 |
| “引擎调用失败 … No module named engine” | 工作目录错位（旧版本 bug）。更新到最新构建即可 |
| “引擎通但缺依赖 …” | 执行 `py -3 -m pip install -r engine\requirements.txt`，再点“Python自检” |
| 第1页空白、无弹框 | 看顶部状态条红色文字，按上面对照处理；点“刷新项目列表”重试 |

## 7. 真机注意

- 首次接仪器先装 Siglent / Tek USB 驱动 + VISA; 在第2页确认 `*IDN?` 能读回.
- 项目A (示波器法) 每频点有 AUTOSet 延时, 全程约 `点数×(0.6s)`; 项目B 约 `点数×0.06s`.
- 中文字体: 界面使用系统字体; Python 出图用 DejaVu 回退 (Windows 有 SimHei 会自动用).

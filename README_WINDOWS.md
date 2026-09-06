# 集成测试控制台 — Windows 运行说明

目标机要求: Windows 10/11 + 系统 Python 3.9+ (已装, PATH 可用) + NI-VISA 或 Keysight VISA.

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
2. 第1页选测试项目 (电话滤波器 300Ω / 射频滤波器 50Ω).
3. 第2页点“扫描仪器”, 点击仪器节点绑定 VISA (★为自动推荐; 无硬件时勾 Demo).
4. 第3页调测试配置, 可存文件复用.
5. 第4页填轮标签 (`DUT_1,DUT_2`) 和输出目录, 点开始; 支持暂停/继续/确认下一步/终止.
6. 第5页打开 `results/...` 看 `raw.csv / *.xlsx / *.png`.

## 5. 新增测试项目 (无需改 C#，两种方式)

- **A. 项目包 (推荐)**：程序第1页“安装项目包(.itcpkg)”导入；`packages/` 目录自带两个内置项目的包；导出同样在第1页。
- **B. 源码目录**：复制 `engine\projects\rf_50ohm\` 为新文件夹, 改 `project.json / diagram.json /
config_schema.json / workflow.py (实现 run(cfg, binding, out_dir, ctrl, open_fn, auto_confirm, labels, demo))`,
用 `python -u -m engine pack --project <id>` 打包分发，重启 exe 即自动出现在项目列表.

## 5b. 设置 / 主题 / 帮助 / 更新

- “设置”页：主题（跟随系统/浅色/深色）、Python 命令、更新源 manifest 地址、检查更新。
- “帮助”页：内置 `HELP.html` 离线帮助（与 exe 同级）。
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

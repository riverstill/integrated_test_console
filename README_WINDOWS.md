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

## 5. 新增测试项目 (无需改 C#)

复制 `engine\projects\rf_50ohm\` 为新文件夹, 改 `project.json / diagram.json /
config_schema.json / workflow.py (实现 run(cfg, binding, out_dir, ctrl, open_fn, auto_confirm, labels, demo))`,
重启 exe 即自动出现在项目列表.

## 6. 真机注意

- 首次接仪器先装 Siglent / Tek USB 驱动 + VISA; 在第2页确认 `*IDN?` 能读回.
- 项目A (示波器法) 每频点有 AUTOSet 延时, 全程约 `点数×(0.6s)`; 项目B 约 `点数×0.06s`.
- 中文字体: 界面使用系统字体; Python 出图用 DejaVu 回退 (Windows 有 SimHei 会自动用).

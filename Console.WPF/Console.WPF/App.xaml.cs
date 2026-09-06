using System.IO;
using System.Windows;
using Console.WPF.Models;

namespace Console.WPF;

public partial class App : Application
{
    public static AppState State { get; } = new();
    public static string EngineProbeLog { get; private set; } = "";

    protected override void OnStartup(StartupEventArgs e)
    {
        State.EngineRoot = LocateEngineRoot(out var log);
        EngineProbeLog = log;
        var saved = Services.Settings.Load();
        State.PythonPath = saved.PythonPath
            ?? Services.PythonEnv.DetectDefault();
        State.Theme = saved.Theme ?? "System";
        State.UpdateUrl = saved.UpdateUrl ?? "";
        Services.ThemeManager.Apply(State.Theme);
        base.OnStartup(e);
    }

    /// <summary>定位 EngineRoot: 包含 engine/__main__.py 的目录.
    /// exe 同级优先, 再逐级向上 (覆盖 publish / bin 调试布局).</summary>
    public static string LocateEngineRoot(out string log)
    {
        var tried = new List<string>();
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            tried.Add(dir.FullName);
            if (File.Exists(Path.Combine(dir.FullName, "engine", "__main__.py")))
            {
                log = "命中: " + dir.FullName;
                return dir.FullName;
            }
        }
        log = "未找到 engine/__main__.py, 已搜索:\n" + string.Join("\n", tried);
        // 回退 exe 同级, 后续调用会报更明确的错
        return AppDomain.CurrentDomain.BaseDirectory;
    }
}

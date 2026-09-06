using System.IO;
using System.Windows;
using Console.WPF.Models;

namespace Console.WPF;

public partial class App : Application
{
    public static AppState State { get; } = new();
    public static string EngineProbeLog { get; private set; } = "";

    public static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IntegratedTestConsole", "crash.log");

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            WriteCrashLog("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(
                "程序发生未处理异常，已记录到:\n" + CrashLogPath +
                "\n\n" + e.Exception.GetBaseException().Message,
                "集成测试控制台", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown(-1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WriteCrashLog("UnhandledException", e.ExceptionObject as Exception);
            MessageBox.Show(
                "程序启动失败，已记录到:\n" + CrashLogPath +
                "\n\n" + (e.ExceptionObject as Exception)?.GetBaseException().Message,
                "集成测试控制台", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }

    public static void WriteCrashLog(string kind, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}\n{ex}\n\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        State.EngineRoot = LocateEngineRoot(out var log);
        EngineProbeLog = log;
        var saved = Services.Settings.Load();
        State.PythonPath = saved.PythonPath
            ?? Services.PythonEnv.DetectDefault();
        State.Theme = saved.Theme ?? "System";
        State.UpdateUrl = saved.UpdateUrl ?? "";
        Services.ThemeManager.ExternalDir =
            Path.Combine(State.EngineRoot, "Themes");
        try
        {
            Services.ThemeManager.Apply(State.Theme);
        }
        catch (Exception ex)
        {
            // 主题加载失败不阻塞启动, 用默认样式跑
            WriteCrashLog("ThemeApply", ex);
        }
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

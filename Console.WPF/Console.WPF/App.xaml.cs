using System.IO;
using System.Windows;
using Console.WPF.Models;

namespace Console.WPF;

public partial class App : Application
{
    public static AppState State { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // engine/ 默认与 exe 同级, 调试时回退到源码相对路径
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var cand = Path.GetFullPath(Path.Combine(baseDir, "engine"));
        if (!Directory.Exists(cand))
            cand = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "engine"));
        State.EngineDir = cand;
        State.PythonPath = Services.PythonEnv.DetectDefault();
        base.OnStartup(e);
    }
}

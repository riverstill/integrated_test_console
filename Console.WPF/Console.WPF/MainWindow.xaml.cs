using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Console.WPF;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PythonBox.Text = App.State.PythonPath;
        DemoCheck.IsChecked = App.State.DemoMode;
        Loaded += async (_, _) => await RunStartupCheck();
        // HWND 就绪后按当前主题刷标题栏；主题切换时跟随
        SourceInitialized += (_, _) => ApplyChrome();
        Services.ThemeManager.Changed += _ => Dispatcher.Invoke(ApplyChrome);
    }

    private void ApplyChrome() => Services.WindowChrome.SetDarkTitle(
        this, Services.ThemeManager.IsDarkNow(App.State.Theme));

    private static Brush Br(string key) =>
        (Brush)Application.Current.FindResource(key);

    private void SetPill(bool? ok, string text)
    {
        EnvInfo.Text = text;
        var color = ok == null ? "SubtleFg" : ok.Value ? "Success" : "Danger";
        StatusDot.Fill = Br(color);
        StatusPill.BorderBrush = Br(color);
    }

    private async Task RunStartupCheck()
    {
        SetPill(null, "自检中...");
        var (ok, msg) = await Task.Run(SelfCheckCore);
        SetPill(ok, msg);
    }

    private static (bool, string) SelfCheckCore()
    {
        // 1) 引擎入口存在?
        var main = Path.Combine(App.State.EngineRoot, "engine", "__main__.py");
        if (!File.Exists(main))
            return (false, $"找不到引擎 {main}。" + App.EngineProbeLog);
        // 2) list_projects 能调通? (同时验证 python 可用 + 工作目录正确)
        int n;
        try
        {
            var json = Services.PyRunner.Query(
                App.State.PythonPath, App.State.EngineRoot, "list_projects", 60000);
            n = JsonNode.Parse(json)!.AsArray().Count;
        }
        catch (Exception e) { return (false, "自检失败: " + e.Message); }
        // 3) 第三方依赖
        var (dok, dmsg) = Services.PythonEnv.CheckDeps(
            App.State.PythonPath, App.State.EngineRoot);
        return dok
            ? (true, $"就绪: {n} 个测试项目 | Python={App.State.PythonPath}")
            : (false, $"引擎通但{dmsg}");
    }

    private void DemoToggled(object s, RoutedEventArgs e) =>
        App.State.DemoMode = DemoCheck.IsChecked == true;

    private void TabChanged(object s, SelectionChangedEventArgs e)
    {
        // 切到第2页时按当前项目/绑定刷新配置与历史
        if (ReferenceEquals(Tabs.SelectedContent, RunWorkspace))
            RunWorkspace.RefreshAll();
    }

    private async void SelfCheck(object s, RoutedEventArgs e) => await RunStartupCheck();

    private void DetectPython(object s, RoutedEventArgs e) =>
        PythonBox.Text = Services.PythonEnv.DetectDefault();

    private void PythonChanged(object s, TextChangedEventArgs e)
    {
        App.State.PythonPath = PythonBox.Text.Trim();
        Services.Settings.Save(App.State.PythonPath, App.State.Theme, App.State.UpdateUrl);
    }
}

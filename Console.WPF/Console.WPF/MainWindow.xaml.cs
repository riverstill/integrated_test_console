using System.Windows;

namespace Console.WPF;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        EnvInfo.Text = $"Python: {App.State.PythonPath} | Engine: {App.State.EngineDir}";
        DemoCheck.IsChecked = App.State.DemoMode;
    }

    private void DemoToggled(object s, RoutedEventArgs e) =>
        App.State.DemoMode = DemoCheck.IsChecked == true;

    private void SelfCheck(object s, RoutedEventArgs e)
    {
        var (ok, msg) = Services.PythonEnv.CheckDeps(App.State.PythonPath, App.State.EngineDir);
        MessageBox.Show(msg + "\n\n缺依赖时执行:\n" +
            Services.PythonEnv.PipInstallCmd(App.State.PythonPath),
            ok ? "自检通过" : "自检未通过");
    }
}

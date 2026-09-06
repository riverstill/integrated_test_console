using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Console.WPF.Views;

/// <summary>
/// 帮助页: 原生 WPF 简要说明（不再内嵌 IE 内核浏览器，避免 ActiveX 启动风险与样式问题）。
/// 详细帮助 HELP.html 用系统默认浏览器打开。
/// </summary>
public partial class HelpView : UserControl
{
    public HelpView()
    {
        InitializeComponent();
        Loaded += (_, _) => PathText.Text =
            "完整帮助: " + Path.Combine(App.State.EngineRoot, "HELP.html");
    }

    private void OpenFullHelp(object s, RoutedEventArgs e)
    {
        var help = Path.Combine(App.State.EngineRoot, "HELP.html");
        if (!File.Exists(help))
        {
            MessageBox.Show("找不到 HELP.html，请确认其与 exe 在同一目录。\n\n" + help,
                "帮助", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(help) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开浏览器:\n" + ex.Message,
                "帮助", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Console.WPF.Views;

/// <summary>帮助页: 显示与 exe 同级的 HELP.html (离线可用).</summary>
public partial class HelpView : UserControl
{
    public HelpView()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    private void Reload(object? s = null, RoutedEventArgs? e = null) => Reload();

    private void Reload()
    {
        var help = Path.Combine(App.State.EngineRoot, "HELP.html");
        PathText.Text = help;
        if (File.Exists(help))
            Browser.Navigate(new Uri(help));
        else
            Browser.NavigateToString(
                "<html><body><p>找不到 HELP.html，请确认其与 exe 在同一目录。</p></body></html>");
    }
}

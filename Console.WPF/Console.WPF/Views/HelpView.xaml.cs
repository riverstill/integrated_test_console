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
        // 延迟到 tab 被选中时才导航: WebBrowser(ActiveX) 初始化异常不再导致启动崩溃
        Loaded += (_, _) => PathText.Text =
            Path.Combine(App.State.EngineRoot, "HELP.html");
    }

    private void Reload(object? s = null, RoutedEventArgs? e = null) => Reload();

    public void Reload()
    {
        var help = Path.Combine(App.State.EngineRoot, "HELP.html");
        PathText.Text = help;
        try
        {
            if (File.Exists(help))
                Browser.Navigate(new Uri(help));
            else
                Browser.NavigateToString(
                    "<html><body><p>找不到 HELP.html，请确认其与 exe 在同一目录。</p></body></html>");
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("HelpNavigate", ex);
            Browser.NavigateToString(
                "<html><body><p>帮助页加载失败: " +
                System.Net.WebUtility.HtmlEncode(ex.GetBaseException().Message) +
                "</p></body></html>");
        }
    }
}

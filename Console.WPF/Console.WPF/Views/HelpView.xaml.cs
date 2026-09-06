using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Console.WPF.Views;

/// <summary>
/// 帮助页: 显示与 exe 同级的 HELP.html (离线可用).
/// WebBrowser 是 IE 内核, 不支持 prefers-color-scheme/CSS 变量,
/// 深色适配靠给 body 加 class="dark" (样式见 HELP.html).
/// </summary>
public partial class HelpView : UserControl
{
    private bool _autoLoaded;

    public HelpView()
    {
        InitializeComponent();
        // 延迟到 tab 被选中时才导航: WebBrowser(ActiveX) 初始化异常不再导致启动崩溃
        Loaded += (_, _) => PathText.Text =
            Path.Combine(App.State.EngineRoot, "HELP.html");
        // 主题切换时若已加载则重渲染深浅
        Services.ThemeManager.Changed += _ =>
        {
            if (_autoLoaded) Dispatcher.Invoke(() => Reload());
        };
    }

    private void Reload(object? s = null, RoutedEventArgs? e = null) => Reload();

    /// <summary>首次点开 tab 时自动调用一次, 之后靠“重新加载”按钮手动刷新.</summary>
    public void EnsureLoaded()
    {
        if (_autoLoaded) return;
        _autoLoaded = true;
        Reload();
    }

    public void Reload()
    {
        _autoLoaded = true;
        var help = Path.Combine(App.State.EngineRoot, "HELP.html");
        PathText.Text = help;
        try
        {
            if (!File.Exists(help))
            {
                Browser.NavigateToString(
                    "<html><body><p>找不到 HELP.html，请确认其与 exe 在同一目录。</p></body></html>");
                return;
            }
            var html = File.ReadAllText(help);
            if (Services.ThemeManager.IsDarkNow(App.State.Theme))
                html = html.Replace("<body>", "<body class=\"dark\">");
            Browser.NavigateToString(html);
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

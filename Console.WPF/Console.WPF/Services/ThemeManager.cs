using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace Console.WPF.Services;

/// <summary>
/// 深 / 浅主题切换. "System" 跟随 Windows 应用主题.
/// 样式与程序分离: 优先加载 exe 旁 Themes/ 下的 loose xaml
/// (改完点“重新加载主题”即生效, 无需重新编译);
/// 目录缺失或解析失败则回退到编译进程序集的内置主题.
/// </summary>
public static class ThemeManager
{
    public static readonly string[] Options = ["System", "Light", "Dark"];

    /// <summary>外部主题目录, 由 App 启动时设为 exe 旁 Themes/.</summary>
    public static string? ExternalDir { get; set; }

    public static string Resolve(string theme)
    {
        if (theme is "Light" or "Dark") return theme;
        try
        {
            var v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return v is int i && i == 0 ? "Dark" : "Light";
        }
        catch { return "Light"; }
    }

    /// <summary>主题切换完成事件 (参数为解析后的 Light/Dark). 代码绘制的内容(如曲线)可订阅跟随.</summary>
    public static event Action<string>? Changed;

    public static void Apply(string theme)
    {
        var name = Resolve(theme);
        var dict = LoadLoose(name) ?? new ResourceDictionary
        {
            Source = new Uri($"/Console.WPF;component/Themes/{name}.xaml", UriKind.Relative)
        };
        var res = Application.Current.Resources;
        res.MergedDictionaries.Clear();
        res.MergedDictionaries.Add(dict);
        Changed?.Invoke(name);
    }

    private static ResourceDictionary? LoadLoose(string name)
    {
        try
        {
            if (ExternalDir == null) return null;
            var file = Path.Combine(ExternalDir, name + ".xaml");
            if (!File.Exists(file)) return null;
            using var fs = File.OpenRead(file);
            return (ResourceDictionary)XamlReader.Load(fs);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("ThemeLoose", ex);
            return null;
        }
    }

    public static bool IsDarkNow(string theme) => Resolve(theme) == "Dark";
}

using System.Windows;

namespace Console.WPF.Services;

/// <summary>深 / 浅主题切换. "System" 跟随 Windows 应用主题.</summary>
public static class ThemeManager
{
    public static readonly string[] Options = ["System", "Light", "Dark"];

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
        var dict = new ResourceDictionary
        {
            Source = new Uri($"/Console.WPF;component/Themes/{name}.xaml", UriKind.Relative)
        };
        var res = Application.Current.Resources;
        res.MergedDictionaries.Clear();
        res.MergedDictionaries.Add(dict);
        Changed?.Invoke(name);
    }

    public static bool IsDarkNow(string theme) => Resolve(theme) == "Dark";
}

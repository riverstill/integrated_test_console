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
    }
}

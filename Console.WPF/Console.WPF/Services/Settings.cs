using System.IO;
using System.Text.Json;

namespace Console.WPF.Services;

public record UserSettings(string? PythonPath, string? Theme, string? UpdateUrl);

/// <summary>用户设置持久化: %AppData%/IntegratedTestConsole/settings.json.
/// 字段缺失时回退默认值, 兼容旧版 settings.json.</summary>
public static class Settings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IntegratedTestConsole", "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<UserSettings>(
                    File.ReadAllText(FilePath)) ?? new UserSettings(null, null, null);
        }
        catch { }
        return new UserSettings(null, null, null);
    }

    public static void Save(string pythonPath, string theme, string updateUrl)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new UserSettings(pythonPath, theme, updateUrl),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // 兼容旧二参调用
    public static void Save(string pythonPath) =>
        Save(pythonPath, "System", "");
}

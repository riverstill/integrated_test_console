using System.IO;
using System.Text.Json;

namespace Console.WPF.Services;

public record UserSettings(string? PythonPath);

/// <summary>用户设置持久化: %AppData%/IntegratedTestConsole/settings.json</summary>
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
                    File.ReadAllText(FilePath)) ?? new UserSettings(null);
        }
        catch { }
        return new UserSettings(null);
    }

    public static void Save(string pythonPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new UserSettings(pythonPath),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

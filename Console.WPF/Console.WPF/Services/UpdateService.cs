using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Console.WPF.Services;

/// <summary>
/// 程序更新预留: 通过 manifest URL 检查新版本.
/// manifest 格式见仓库根 update_manifest.example.json:
/// {"version":"0.2.1","downloadUrl":"...","notes":"..."}
/// UpdateUrl 为空 = 未配置更新源, 检查直接提示.
/// </summary>
public static class UpdateService
{
    public record Manifest(string Version, string? DownloadUrl, string? Notes);

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task<(bool hasUpdate, string msg)> CheckAsync(
        string url, string current)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "未配置更新源：在“设置”页填写 manifest 地址后重试。");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var json = await http.GetStringAsync(url.Trim());
            var m = JsonSerializer.Deserialize<Manifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (m == null || string.IsNullOrWhiteSpace(m.Version))
                return (false, "更新源返回内容无法解析。");
            var cmp = CompareVersions(m.Version.Trim(), current.Trim());
            if (cmp > 0)
                return (true,
                    $"发现新版本 {m.Version}（当前 {current}）。\n\n{m.Notes}\n\n下载：{m.DownloadUrl}");
            return (false, $"已是最新（当前 {current}，远端 {m.Version}）。");
        }
        catch (Exception e)
        {
            return (false, "检查更新失败：" + e.Message);
        }
    }

    public static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.'); var pb = b.Split('.');
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            int x = i < pa.Length && int.TryParse(pa[i], out var vx) ? vx : 0;
            int y = i < pb.Length && int.TryParse(pb[i], out var vy) ? vy : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }
}

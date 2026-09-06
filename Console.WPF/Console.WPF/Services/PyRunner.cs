using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Console.WPF.Services;

/// <summary>
/// Python 引擎进程管理: stdout JSONL 事件 -> C# 事件; stdin 控制行.
/// 事件: OnEvent(JsonNode) 由 UI 线程封送后处理.
/// </summary>
public class PyRunner : IDisposable
{
    private Process? _p;
    public event Action<JsonNode>? OnEvent;
    public bool Running => _p != null && !_p.HasExited;

    /// 同步一问一答命令(list_projects/get_schema/scan).
    /// 失败时抛异常 (内含 exit code + stderr), 不再返回空字符串误导上层 JSON 解析.
    public static string Query(string pythonCmd, string engineRoot, string args, int timeoutMs = 30000)
    {
        ProcessStartInfo psi;
        try
        {
            psi = new ProcessStartInfo("cmd", $"/c {pythonCmd} -u -m engine {args}")
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = engineRoot,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"无法启动 Python 进程 (命令={pythonCmd}, 目录={engineRoot}): {e.Message}", e);
        }
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(timeoutMs);
        if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            var tail = stderr.Length > 800 ? "..." + stderr[^800..] : stderr;
            throw new InvalidOperationException(
                $"引擎调用失败 (命令={pythonCmd}, 目录={engineRoot}, 参数={args}, exit={p.ExitCode})。" +
                $"请先点顶部“Python自检”。stderr:\n{tail}");
        }
        return stdout;
    }

    /// 长任务 run: 启动进程并异步解析 stdout 每一行 JSON
    public void StartRun(string pythonCmd, string engineRoot, string runArgs)
    {
        Stop();
        if (!File.Exists(Path.Combine(engineRoot, "engine", "__main__.py")))
            throw new InvalidOperationException(
                $"找不到引擎: {Path.Combine(engineRoot, "engine", "__main__.py")} 不存在。" +
                "请确认 engine 文件夹与 exe 在同一目录。");
        var psi = new ProcessStartInfo("cmd", $"/c {pythonCmd} -u -m engine run {runArgs}")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false,
            CreateNoWindow = true, WorkingDirectory = engineRoot,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            StandardInputEncoding = System.Text.Encoding.UTF8,
        };
        _p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _p.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            try { OnEvent?.Invoke(JsonNode.Parse(e.Data)!); } catch { }
        };
        _p.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            OnEvent?.Invoke(new JsonObject
            {
                ["type"] = "log", ["level"] = "warn", ["msg"] = e.Data
            });
        };
        _p.Start();
        _p.BeginOutputReadLine();
        _p.BeginErrorReadLine();
    }

    private void Send(string json)
    {
        if (!Running) return;
        try { _p!.StandardInput.WriteLine(json); _p.StandardInput.Flush(); } catch { }
    }

    public void Pause() => Send("""{"cmd":"pause"}""");
    public void Resume() => Send("""{"cmd":"resume"}""");
    public void Confirm(string label = "") =>
        Send(JsonObject.Parse(JsonSerializer.Serialize(new { cmd = "confirm", label }))!.ToJsonString());

    public void Stop()
    {
        if (_p == null) return;
        try
        {
            if (!_p.HasExited) { Send("""{"cmd":"stop"}"""); _p.WaitForExit(5000); }
        }
        catch { }
        try { if (!_p.HasExited) _p.Kill(); } catch { }
        _p.Dispose(); _p = null;
    }

    public void Dispose() => Stop();
}

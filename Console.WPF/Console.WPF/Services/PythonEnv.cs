using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Console.WPF.Services;

/// <summary>系统 Python 定位 + 依赖自检. 用户机已装 Python, 此处只检测不捆绑.</summary>
public static class PythonEnv
{
    public static string DetectDefault()
    {
        // 优先 py 启动器(Windows), 否则 PATH 中的 python
        foreach (var c in new[] { "py -3", "python", "python3" })
        {
            try
            {
                var psi = new ProcessStartInfo("cmd", $"/c {c} --version")
                {
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(5000);
                if (p.ExitCode == 0) return c;
            }
            catch { }
        }
        return "python";
    }

    public static (bool ok, string msg) CheckDeps(string pythonCmd, string engineRoot)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd",
                $"/c {pythonCmd} -c \"import pyvisa,numpy,matplotlib,openpyxl; print('ok')\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = engineRoot
            };
            using var p = Process.Start(psi)!;
            var out_ = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30000);
            return p.ExitCode == 0 && out_.Contains("ok")
                ? (true, "依赖齐全")
                : (false, "缺依赖, 请执行: " + pythonCmd + " -m pip install -r engine/requirements.txt");
        }
        catch (Exception e) { return (false, e.Message); }
    }

    public static string PipInstallCmd(string pythonCmd) =>
        $"{pythonCmd} -m pip install -r engine{Path.DirectorySeparatorChar}requirements.txt";
}

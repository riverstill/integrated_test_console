using System.Text.Json;
using System.Text.Json.Nodes;

namespace Console.WPF.Models;

public record InstrumentNeed(string Key, string Label, string Itype);
public record ProjectMeta(string Id, string Name, string Description,
    List<InstrumentNeed> Instruments, string Dir);

public record ScanItem(string Visa, string Idn, string Itype);

public record DiagramNode(string Id, string Kind, string Label, string? Role);
public record DiagramEdge(string Src, string Dst, string? Label);

public class AppState
{
    public string PythonPath { get; set; } = "py -3";
    /// <summary>主题: System(跟随系统，默认) / Light / Dark.</summary>
    public string Theme { get; set; } = "System";
    /// <summary>更新 manifest 地址，为空表示未配置更新源.</summary>
    public string UpdateUrl { get; set; } = "";
    /// <summary>引擎根目录: 包含 engine/ 包的目录 (exe 同级或仓库根).
    /// Python 以此为 WorkingDirectory 执行 -m engine, 差一级就会报 No module named engine.</summary>
    public string EngineRoot { get; set; } = "";
    public ProjectMeta? Project { get; set; }
    public Dictionary<string, string> Binding { get; } = new();  // role -> visa
    public Dictionary<string, string> BindingIdn { get; } = new();
    public JsonObject? ConfigSchema { get; set; }
    public Dictionary<string, string> ConfigValues { get; } = new();
    public bool DemoMode { get; set; } = true;
}

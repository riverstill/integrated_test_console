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
    public string EngineDir { get; set; } = "";   // engine/ 目录(与 exe 同级或源码相对路径)
    public ProjectMeta? Project { get; set; }
    public Dictionary<string, string> Binding { get; } = new();  // role -> visa
    public Dictionary<string, string> BindingIdn { get; } = new();
    public JsonObject? ConfigSchema { get; set; }
    public Dictionary<string, string> ConfigValues { get; } = new();
    public bool DemoMode { get; set; } = true;
}

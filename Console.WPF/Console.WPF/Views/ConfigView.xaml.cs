using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Console.WPF.Services;
using Microsoft.Win32;

namespace Console.WPF.Views;

/// <summary>按 config_schema.json 自动生成表单, 值存 App.State.ConfigValues.</summary>
public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            try { LoadDefault(); }
            catch (Exception ex)
            {
                MessageBox.Show("加载配置失败 (请先看顶部自检状态):\n" + ex.Message,
                    "集成测试控制台", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
    }

    public void LoadDefault(object? s = null, RoutedEventArgs? e = null)
    {
        var p = App.State.Project;
        if (p == null) return;  // 引擎未就绪时静默, 由顶部自检条提示
        var schema = JsonNode.Parse(PyRunner.Query(App.State.PythonPath,
            App.State.EngineRoot, $"get_schema --project {p.Id}"))!.AsObject();
        App.State.ConfigSchema = schema;
        BuildForm(schema);
    }

    private void BuildForm(JsonObject schema)
    {
        Form.Children.Clear();
        App.State.ConfigValues.Clear();
        foreach (var (key, def) in schema["properties"]!.AsObject())
        {
            var title = def!["title"]?.ToString() ?? key;
            var dflt = def!["default"]?.ToString() ?? "";
            App.State.ConfigValues[key] = dflt;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(new TextBlock
            {
                Text = title, Width = 220, VerticalAlignment = VerticalAlignment.Center
            });
            var tb = new TextBox { Text = dflt, Width = 160, Tag = key };
            tb.TextChanged += (s, _) =>
                App.State.ConfigValues[((TextBox)s).Tag!.ToString()!] = ((TextBox)s).Text;
            row.Children.Add(tb);
            Form.Children.Add(row);
        }
    }

    private void LoadFile(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
        if (dlg.ShowDialog() != true) return;
        var obj = JsonNode.Parse(File.ReadAllText(dlg.FileName))!.AsObject();
        foreach (var (k, v) in obj)
            if (App.State.ConfigValues.ContainsKey(k))
                App.State.ConfigValues[k] = v!.ToString();
        if (App.State.ConfigSchema != null) BuildFormWithValues(App.State.ConfigSchema);
    }

    private void BuildFormWithValues(JsonObject schema)
    {
        var keep = new Dictionary<string, string>(App.State.ConfigValues);
        BuildForm(schema);
        foreach (var (k, v) in keep)
            if (App.State.ConfigValues.ContainsKey(k)) App.State.ConfigValues[k] = v;
        // 回填显示
        foreach (StackPanel row in Form.Children)
            foreach (var c in row.Children)
                if (c is TextBox tb && keep.TryGetValue(tb.Tag!.ToString()!, out var val))
                    tb.Text = val;
    }

    private void SaveFile(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "config.json" };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName,
            JsonSerializer.Serialize(App.State.ConfigValues,
                new JsonSerializerOptions { WriteIndented = true }));
    }
}

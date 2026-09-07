using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Console.WPF.Models;
using Console.WPF.Services;

namespace Console.WPF.Views;

/// <summary>右栏: 扫描 + 绑定状态. 扫描结果经静态 ScanResults 与框图共享.</summary>
public partial class InstrumentBindView : UserControl
{
    public static List<ScanItem> ScanResults { get; private set; } = new();

    public InstrumentBindView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshStatus();
    }

    private void Scan(object s, RoutedEventArgs e)
    {
        try
        {
            var arg = App.State.DemoMode ? "scan --demo" : "scan";
            var json = PyRunner.Query(App.State.PythonPath, App.State.EngineRoot, arg, 60000);
            ScanResults = JsonNode.Parse(json)!.AsArray().Select(n =>
                new ScanItem(n!["visa"]!.GetValue<string>(), n!["idn"]!.GetValue<string>(),
                    n!["itype"]!.GetValue<string>())).ToList();
            ScanList.ItemsSource = ScanResults.Select(x => new
            {
                x.Visa,
                Idn = $"[{x.Itype}] {x.Idn}"
            });
            RefreshStatus();
        }
        catch (Exception ex) { MessageBox.Show("扫描失败:\n" + ex.Message); }
    }

    /// <summary>按当前项目所需角色逐行显示绑定情况.</summary>
    public void RefreshStatus()
    {
        StatusPanel.Children.Clear();
        var p = App.State.Project;
        if (p == null)
        {
            StatusPanel.Children.Add(new TextBlock
            {
                Text = "请先在左栏选择测试项目。",
                Foreground = (Brush)Application.Current.FindResource("SubtleFg")
            });
            return;
        }
        foreach (var need in p.Instruments)
        {
            var bound = App.State.Binding.TryGetValue(need.Key, out var visa);
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center,
                Fill = (Brush)Application.Current.FindResource(bound ? "Success" : "Warn"),
                Margin = new Thickness(0, 0, 6, 0)
            };
            var txt = new TextBlock
            {
                Text = bound ? $"{need.Label}\n{visa}" : $"{need.Label}\n未绑定（点框图节点选择）",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.FindResource(
                    bound ? "TextFg" : "SubtleFg")
            };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 4)
            };
            row.Children.Add(dot); row.Children.Add(txt);
            StatusPanel.Children.Add(row);
        }
    }
}

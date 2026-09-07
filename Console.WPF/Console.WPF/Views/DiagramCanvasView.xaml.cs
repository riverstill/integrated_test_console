using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Console.WPF.Services;

namespace Console.WPF.Views;

/// <summary>中栏: 框图预览. 扫描结果经 InstrumentBindView.ScanResults 共享.
/// 绑定成功后经 BindingChanged 通知 SystemView 刷新右侧状态区.</summary>
public partial class DiagramCanvasView : UserControl
{
    public event Action? BindingChanged;

    public DiagramCanvasView() { InitializeComponent(); Loaded += (_, _) => Draw(); }

    public void Draw()
    {
        Canvas.Children.Clear();
        var p = App.State.Project;
        if (p == null) return;
        try
        {
            var json = PyRunner.Query(App.State.PythonPath, App.State.EngineRoot,
                $"get_diagram --project {p.Id}");
            var root = JsonNode.Parse(json)!;
            var nodes = root["nodes"]!.AsArray().ToList();
            var edges = root["edges"]!.AsArray().ToList();
            double x = 30;
            var pos = new Dictionary<string, Point>();
            foreach (var n in nodes)
            {
                var id = n!["id"]!.GetValue<string>();
                pos[id] = new Point(x, 150); x += 200;
            }
            foreach (var e in edges)
            {
                var a = pos[e!["src"]!.GetValue<string>()];
                var b = pos[e!["dst"]!.GetValue<string>()];
                Canvas.Children.Add(new Line
                {
                    X1 = a.X + 70, Y1 = a.Y, X2 = b.X - 70, Y2 = b.Y,
                    Stroke = Brushes.SlateGray, StrokeThickness = 2
                });
            }
            foreach (var n in nodes)
            {
                var id = n!["id"]!.GetValue<string>();
                var kind = n!["kind"]!.GetValue<string>();
                var label = n!["label"]!.GetValue<string>().Replace("\\n", "\n");
                var role = n!["role"]?.GetValue<string>();
                var btn = new Button
                {
                    Content = label, Width = 140, Height = 90, Tag = (id, role, kind)
                };
                static Brush Br(string key) =>
                    (Brush)Application.Current.FindResource(key);
                bool bound = role != null && App.State.Binding.ContainsKey(role);
                if (kind == "instrument")
                {
                    // 已绑定=强调色, 未绑定=中性底+琥珀边框提示选择
                    btn.Background = bound ? Br("Accent") : Br("PanelBg");
                    btn.Foreground = bound ? Br("AccentFg") : Br("TextFg");
                    btn.BorderBrush = bound ? Br("Accent") : Br("Warn");
                    btn.BorderThickness = new Thickness(bound ? 1 : 2);
                    btn.Click += NodeClick;
                    btn.ToolTip = bound ? App.State.Binding[role!] : "点击选择仪器";
                }
                else if (kind == "dut")
                {
                    btn.Background = Br("SelectedBg");
                    btn.Foreground = Br("TextFg");
                    btn.BorderBrush = Br("Warn");
                    btn.BorderThickness = new Thickness(2);
                    btn.FontWeight = FontWeights.SemiBold;
                    btn.IsEnabled = false;
                }
                else
                {
                    btn.IsEnabled = false;
                }
                Canvas.SetLeft(btn, pos[id].X - 70); Canvas.SetTop(btn, pos[id].Y - 45);
                Canvas.Children.Add(btn);
            }
        }
        catch (Exception ex) { MessageBox.Show("框图加载失败:\n" + ex.Message); }
    }

    private void NodeClick(object s, RoutedEventArgs e)
    {
        var btn = (Button)s;
        var (id, role, _) = ((string, string?, string))btn.Tag!;
        var scan = InstrumentBindView.ScanResults;
        if (role == null || scan.Count == 0)
        { MessageBox.Show("请先在右侧点“扫描仪器”"); return; }
        var need = App.State.Project!.Instruments.First(i => i.Key == role);
        var dlg = new Window
        {
            Title = $"为 {need.Label} 选择仪器", Width = 560, Height = 380,
            Content = new ListBox { Name = "L" }
        };
        var lb = (ListBox)dlg.Content;
        lb.ItemsSource = scan.Select(x =>
            $"{x.Visa}\n[{x.Itype}] {x.Idn}" +
            (x.Itype == need.Itype ? "  ★推荐" : ""));
        lb.MouseDoubleClick += (_, _) =>
        {
            if (lb.SelectedIndex < 0) return;
            var sel = scan[lb.SelectedIndex];
            App.State.Binding[role] = sel.Visa;
            App.State.BindingIdn[role] = sel.Idn;
            dlg.Close(); Draw();
            BindingChanged?.Invoke();
        };
        dlg.ShowDialog();
    }
}

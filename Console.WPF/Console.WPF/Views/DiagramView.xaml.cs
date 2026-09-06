using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Console.WPF.Models;
using Console.WPF.Services;

namespace Console.WPF.Views;

public partial class DiagramView : UserControl
{
    private List<ScanItem> _scan = new();

    public DiagramView() { InitializeComponent(); Loaded += (_, _) => Draw(); }

    private void Scan(object s, RoutedEventArgs e)
    {
        try
        {
            var arg = App.State.DemoMode ? "scan --demo" : "scan";
            var json = PyRunner.Query(App.State.PythonPath, App.State.EngineDir, arg, 60000);
            _scan = JsonNode.Parse(json)!.AsArray().Select(n =>
                new ScanItem(n!["visa"]!.GetValue<string>(), n!["idn"]!.GetValue<string>(),
                    n!["itype"]!.GetValue<string>())).ToList();
            ScanList.ItemsSource = _scan.Select(x => new
            {
                x.Visa,
                Idn = $"[{x.Itype}] {x.Idn}"
            });
            Draw();
        }
        catch (Exception ex) { MessageBox.Show("扫描失败:\n" + ex.Message); }
    }

    private void Draw()
    {
        Canvas.Children.Clear();
        var p = App.State.Project;
        if (p == null) return;
        try
        {
            var json = PyRunner.Query(App.State.PythonPath, App.State.EngineDir,
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
                bool bound = role != null && App.State.Binding.ContainsKey(role);
                if (kind == "instrument")
                {
                    btn.Background = bound ? Brushes.DarkGreen : Brushes.DimGray;
                    btn.Foreground = Brushes.White;
                    btn.Click += NodeClick;
                    btn.ToolTip = bound ? App.State.Binding[role!] : "点击选择仪器";
                }
                else
                {
                    btn.Background = kind == "dut" ? Brushes.DarkOrange : Brushes.Teal;
                    btn.Foreground = Brushes.White;
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
        if (role == null || _scan.Count == 0)
        { MessageBox.Show("请先点“扫描仪器”"); return; }
        var need = App.State.Project!.Instruments.First(i => i.Key == role);
        var dlg = new Window
        {
            Title = $"为 {need.Label} 选择仪器", Width = 560, Height = 380,
            Content = new ListBox { Name = "L" }
        };
        var lb = (ListBox)dlg.Content;
        lb.ItemsSource = _scan.Select(x =>
            $"{x.Visa}\n[{x.Itype}] {x.Idn}" +
            (x.Itype == need.Itype ? "  ★推荐" : ""));
        lb.MouseDoubleClick += (_, _) =>
        {
            if (lb.SelectedIndex < 0) return;
            var sel = _scan[lb.SelectedIndex];
            App.State.Binding[role] = sel.Visa;
            App.State.BindingIdn[role] = sel.Idn;
            dlg.Close(); Draw();
        };
        dlg.ShowDialog();
    }
}

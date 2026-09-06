using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Console.WPF.Services;

namespace Console.WPF.Views;

public partial class RunView : UserControl
{
    private readonly PyRunner _runner = new();
    private readonly ObservableCollection<Dictionary<string, string>> _rows = new();
    private readonly Dictionary<string, List<(double x, double y)>> _series = new();

    public RunView()
    {
        InitializeComponent();
        Grid.ItemsSource = _rows;
        _runner.OnEvent += ev => Dispatcher.Invoke(() => Handle(ev));
        Loaded += (_, _) =>
        {
            // v4 无原生对数轴: X 用 log10(Hz), 刻度即指数 (3=1k, 6=1M, 8=100M)
            Plot.Plot.YLabel("dB"); Plot.Plot.XLabel("频率 log10(Hz)");
            Plot.Refresh();
        };
    }

    private void AppendLog(string s)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}\n");
        LogBox.ScrollToEnd();
    }

    private void Start(object s, RoutedEventArgs e)
    {
        var p = App.State.Project;
        if (p == null) { MessageBox.Show("请先选择测试项目"); return; }
        // 落盘本次配置+绑定, 传绝对路径给 Python
        var sessDir = Path.GetFullPath(OutBox.Text);
        Directory.CreateDirectory(sessDir);
        File.WriteAllText(Path.Combine(sessDir, "config.json"),
            JsonSerializer.Serialize(App.State.ConfigValues,
                new JsonSerializerOptions { WriteIndented = true }));
        var binding = new Dictionary<string, string>(App.State.Binding);
        if (App.State.DemoMode) binding["demo"] = "true";
        File.WriteAllText(Path.Combine(sessDir, "binding.json"),
            JsonSerializer.Serialize(binding,
                new JsonSerializerOptions { WriteIndented = true }));
        _rows.Clear(); _series.Clear(); Plot.Plot.Clear();
        var args = $"--project {p.Id} --config \"{Path.Combine(sessDir, "config.json")}\" " +
                   $"--binding \"{Path.Combine(sessDir, "binding.json")}\" " +
                   $"--out \"{sessDir}\" --labels \"{LabelsBox.Text}\"";
        if (App.State.DemoMode) args += " --demo";
        Status.Text = "运行中...";
        AppendLog("启动: " + args);
        _runner.StartRun(App.State.PythonPath, App.State.EngineDir, args);
    }

    private void Handle(JsonNode ev)
    {
        var t = ev["type"]?.ToString();
        switch (t)
        {
            case "log": AppendLog(ev["msg"]?.ToString() ?? ""); break;
            case "phase":
                Status.Text = ev["msg"]?.ToString();
                AppendLog("[阶段] " + ev["msg"]);
                break;
            case "need_confirm":
                Status.Text = "等待确认: " + ev["prompt"];
                AppendLog("[等待确认] " + ev["prompt"] + " -> 点“确认下一步”");
                break;
            case "point":
            {
                var d = new Dictionary<string, string>();
                foreach (var (k, v) in ev.AsObject())
                    d[k] = v is JsonValue jv && jv.TryGetValue<string>(out var s)
                        ? s : v!.ToString();
                _rows.Add(d);
                if (_rows.Count > 2000) _rows.RemoveAt(0);
                // 实时曲线: y 取 loss_db / att_db / thru_loss_db
                double x = ev["freq"]?.GetValue<double>() ?? 0;
                double y = ev["loss_db"]?.GetValue<double>()
                    ?? ev["att_db"]?.GetValue<double>()
                    ?? ev["thru_loss_db"]?.GetValue<double>() ?? double.NaN;
                if (!double.IsNaN(y) && y > -900 && x > 0)
                {
                    var key = $"R{ev["round"]}_{ev["kind"]}";
                    if (!_series.TryGetValue(key, out var pts))
                        _series[key] = pts = new List<(double, double)>();
                    pts.Add((Math.Log10(x), y));
                    Plot.Plot.Clear();
                    foreach (var (k, v) in _series)
                        Plot.Plot.AddScatter(v.Select(p => p.x).ToArray(),
                            v.Select(p => p.y).ToArray(), label: k);
                    Plot.Plot.Legend();
                    Plot.Refresh();
                }
                break;
            }
            case "round_done": AppendLog($"第{ev["round"]}轮完成 ({ev["label"]})"); break;
            case "result": AppendLog("报告: " + ev["out_dir"]); Status.Text = "完成"; break;
            case "error": AppendLog("[错误] " + ev["msg"]); Status.Text = "出错"; break;
            case "done": AppendLog(ev["msg"]?.ToString() ?? ""); Status.Text = "完成"; break;
        }
    }

    private void Pause(object s, RoutedEventArgs e) { _runner.Pause(); AppendLog("已发送暂停"); }
    private void Resume(object s, RoutedEventArgs e) { _runner.Resume(); AppendLog("已发送继续"); }
    private void Confirm(object s, RoutedEventArgs e)
    {
        var tb = new TextBox { Width = 260, Margin = new Thickness(10) };
        var dlg = new Window
        {
            Title = "确认下一步 (可修改本轮标签, 留空用默认)",
            Width = 340, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Content = new StackPanel
            {
                Children =
                {
                    tb,
                    new Button
                    {
                        Content = "确认", Margin = new Thickness(10), IsDefault = true,
                        CommandParameter = true
                    }
                }
            }
        };
        ((StackPanel)dlg.Content).Children.OfType<Button>().First().Click += (_, _) =>
        { dlg.DialogResult = true; dlg.Close(); };
        var label = dlg.ShowDialog() == true ? tb.Text.Trim() : "";
        _runner.Confirm(label);
        AppendLog("已确认下一步, 标签=" + (string.IsNullOrEmpty(label) ? "(默认)" : label));
    }
    private void Stop(object s, RoutedEventArgs e)
    {
        _runner.Stop(); Status.Text = "已终止"; AppendLog("已终止, 仪器已安全关闭");
    }
}

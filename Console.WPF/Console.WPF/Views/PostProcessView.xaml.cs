using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace Console.WPF.Views;

/// <summary>右栏: 本轮轮次 + 导出/打开报告 + 历史会话 (原 HistoryView 并入).</summary>
public partial class PostProcessView : UserControl
{
    private readonly ObservableCollection<string> _rounds = new();
    private string _reportDir = "";
    private RunCenterView? _center;

    public PostProcessView()
    {
        InitializeComponent();
        RoundList.ItemsSource = _rounds;
        Loaded += (_, _) => RefreshHistory();
    }

    /// <summary>由 RunWorkspaceView 在组装时调用, 订阅运行事件.</summary>
    public void Attach(RunCenterView center)
    {
        _center = center;
        center.RunEvent += ev => Dispatcher.Invoke(() => OnRunEvent(ev));
    }

    private void OnRunEvent(JsonNode ev)
    {
        switch (ev["type"]?.ToString())
        {
            case "round_done":
                _rounds.Add($"第{ev["round"]}轮 {ev["label"]}");
                break;
            case "result":
            case "done":
                if (_center != null) _reportDir = _center.LastOutDir;
                RefreshHistory();
                break;
        }
    }

    private void ExportPng(object s, RoutedEventArgs e)
    {
        if (_center == null) return;
        var dir = string.IsNullOrEmpty(_reportDir) ? _center.LastOutDir : _reportDir;
        if (string.IsNullOrEmpty(dir))
        { MessageBox.Show("还没有运行结果可导出。"); return; }
        try
        {
            var file = _center.ExportPlot(dir);
            MessageBox.Show("已导出:\n" + file, "导出曲线");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败:\n" + ex.Message,
                "导出曲线", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenReport(object s, RoutedEventArgs e)
    {
        var dir = string.IsNullOrEmpty(_reportDir)
            ? _center?.LastOutDir ?? "" : _reportDir;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        { MessageBox.Show("报告目录还不存在，先运行一次。"); return; }
        Process.Start("explorer", $"\"{Path.GetFullPath(dir)}\"");
    }

    public void RefreshHistory(object? s = null, RoutedEventArgs? e = null)
    {
        try
        {
            var root = Path.GetFullPath("results");
            HistoryList.ItemsSource = Directory.Exists(root)
                ? Directory.GetDirectories(root).OrderDescending().ToList()
                : new List<string> { "(results 目录不存在, 运行一次后自动创建)" };
        }
        catch { }
    }

    private void OpenHistory(object? s, RoutedEventArgs? e)
    {
        if (HistoryList.SelectedItem is string dir && Directory.Exists(dir))
            Process.Start("explorer", $"\"{dir}\"");
    }
}

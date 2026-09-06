using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Console.WPF.Models;
using Console.WPF.Services;

namespace Console.WPF.Views;

public partial class ProjectsView : UserControl
{
    private bool _quiet = true;  // 首次自动加载失败不弹框, 只靠顶部自检条提示

    public ProjectsView() { InitializeComponent(); Loaded += (_, _) => Refresh(); }

    private void Refresh(object? s = null, RoutedEventArgs? e = null)
    {
        _quiet = false;
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            var json = PyRunner.Query(App.State.PythonPath, App.State.EngineRoot, "list_projects");
            var items = new List<ProjectMeta>();
            foreach (var n in JsonNode.Parse(json)!.AsArray())
            {
                static string S(JsonNode? x, string k) =>
                    x![k]?.GetValue<string>() ?? "";
                var needs = n!["instruments"]!.AsArray().Select(x =>
                    new InstrumentNeed(S(x, "key"), S(x, "label"), S(x, "itype"))).ToList();
                items.Add(new ProjectMeta(S(n, "id"), S(n, "name"),
                    S(n, "description"), needs, n!["_dir"]?.GetValue<string>() ?? ""));
            }
            List.ItemsSource = items;
        }
        catch (Exception ex)
        {
            if (!_quiet)
                MessageBox.Show("读取项目失败 (请先看顶部自检状态):\n" + ex.Message,
                    "集成测试控制台", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _quiet = false; }
    }

    private void Selected(object s, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is ProjectMeta p)
        {
            App.State.Project = p;
            App.State.Binding.Clear(); App.State.BindingIdn.Clear();
        }
    }
}

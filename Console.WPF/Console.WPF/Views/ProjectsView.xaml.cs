using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Console.WPF.Models;
using Console.WPF.Services;

namespace Console.WPF.Views;

public partial class ProjectsView : UserControl
{
    public ProjectsView() { InitializeComponent(); Loaded += (_, _) => Refresh(); }

    private void Refresh(object? s = null, RoutedEventArgs? e = null) => Refresh();

    private void Refresh()
    {
        try
        {
            var json = PyRunner.Query(App.State.PythonPath, App.State.EngineDir, "list_projects");
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
        catch (Exception ex) { MessageBox.Show("读取项目失败:\n" + ex.Message); }
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

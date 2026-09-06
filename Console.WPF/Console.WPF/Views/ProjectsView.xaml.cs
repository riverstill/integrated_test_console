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

    private void InstallPkg(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
            { Filter = "测试项目包|*.itcpkg;*.zip" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            // 先预览包信息, 确认后再安装
            var info = PyRunner.Query(App.State.PythonPath, App.State.EngineRoot,
                $"inspect_pkg --pkg \"{dlg.FileName}\"");
            var meta = System.Text.Json.Nodes.JsonNode.Parse(info)!;
            var msg = $"安装测试项目?\n\n名称: {meta["name"]?.GetValue<string>()}\n" +
                      $"id: {meta["id"]?.GetValue<string>()}\n说明: {meta["description"]?.GetValue<string>()}";
            if (MessageBox.Show(msg, "安装项目包",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            PyRunner.Query(App.State.PythonPath, App.State.EngineRoot,
                $"install_pkg --pkg \"{dlg.FileName}\"");
            MessageBox.Show("安装成功, 列表已刷新。", "安装项目包");
            _quiet = false;
            Refresh();
        }
        catch (Exception ex)
        {
            var hint = ex.Message.Contains("已存在")
                ? "\n\n如需覆盖, 请先删除 engine/projects 下同名目录后重试。"
                : "";
            MessageBox.Show("安装失败:\n" + ex.Message + hint,
                "安装项目包", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExportPkg(object s, RoutedEventArgs e)
    {
        if (List.SelectedItem is not ProjectMeta p)
        { MessageBox.Show("请先在列表中选中一个测试项目。"); return; }
        var dlg = new Microsoft.Win32.SaveFileDialog
            { Filter = "测试项目包|*.itcpkg", FileName = p.Id + ".itcpkg" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = PyRunner.Query(App.State.PythonPath, App.State.EngineRoot,
                $"pack --project {p.Id} --out \"{dlg.FileName}\"");
            MessageBox.Show("已导出:\n" + json, "导出项目包");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败:\n" + ex.Message,
                "导出项目包", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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

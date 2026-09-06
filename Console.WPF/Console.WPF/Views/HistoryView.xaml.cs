using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Console.WPF.Views;

public partial class HistoryView : UserControl
{
    public HistoryView() { InitializeComponent(); Loaded += (_, _) => Refresh(); }

    private void Refresh(object? s = null, RoutedEventArgs? e = null) => Refresh();

    private void Refresh()
    {
        try
        {
            var root = Path.GetFullPath(RootBox.Text);
            List.ItemsSource = Directory.Exists(root)
                ? Directory.GetDirectories(root).OrderDescending().ToList()
                : new List<string> { "(results 目录不存在, 运行一次后自动创建)" };
        }
        catch { }
    }

    private void Open(object? s = null, RoutedEventArgs? e = null)
    {
        if (List.SelectedItem is string dir && Directory.Exists(dir))
            Process.Start("explorer", $"\"{dir}\"");
    }
}

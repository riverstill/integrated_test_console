using System.Windows;
using System.Windows.Controls;
using Console.WPF.Services;

namespace Console.WPF.Views;

public partial class SettingsView : UserControl
{
    private static readonly (string Value, string Label)[] Themes =
    [
        ("System", "跟随系统（默认）"),
        ("Light", "浅色"),
        ("Dark", "深色"),
    ];

    public SettingsView()
    {
        InitializeComponent();
        ThemeBox.ItemsSource = Themes.Select(t => t.Label).ToList();
        Loaded += (_, _) => SyncFromState();
    }

    private void SyncFromState()
    {
        ThemeBox.SelectedIndex = Math.Max(0,
            Array.FindIndex(Themes, t => t.Value == App.State.Theme));
        if (PythonBox.Text != App.State.PythonPath)
            PythonBox.Text = App.State.PythonPath;
        if (UpdateBox.Text != App.State.UpdateUrl)
            UpdateBox.Text = App.State.UpdateUrl;
        VersionText.Text = "当前版本 " + UpdateService.CurrentVersion;
    }

    private void Persist() => Settings.Save(
        App.State.PythonPath, App.State.Theme, App.State.UpdateUrl);

    private void ThemeChanged(object s, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedIndex < 0) return;
        App.State.Theme = Themes[ThemeBox.SelectedIndex].Value;
        ThemeManager.Apply(App.State.Theme);
        Persist();
    }

    private void PythonChanged(object s, TextChangedEventArgs e)
    {
        App.State.PythonPath = PythonBox.Text.Trim();
        Persist();
    }

    private void UpdateChanged(object s, TextChangedEventArgs e)
    {
        App.State.UpdateUrl = UpdateBox.Text.Trim();
        Persist();
    }

    private async void CheckUpdate(object s, RoutedEventArgs e)
    {
        UpdateMsg.Text = "检查中...";
        var (has, msg) = await UpdateService.CheckAsync(
            App.State.UpdateUrl, UpdateService.CurrentVersion);
        UpdateMsg.Text = msg;
        if (has)
            MessageBox.Show(msg, "发现新版本",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

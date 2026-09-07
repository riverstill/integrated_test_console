using System.Windows.Controls;

namespace Console.WPF.Views;

/// <summary>第1页: 项目选择 | 框图预览 | 连接仪器. 经 App.State + 事件串联三栏.</summary>
public partial class SystemView : UserControl
{
    public SystemView()
    {
        InitializeComponent();
        ProjectPart.ProjectSelected += () =>
        {
            DiagramPart.Draw();
            BindPart.RefreshStatus();
        };
        DiagramPart.BindingChanged += () => BindPart.RefreshStatus();
    }
}

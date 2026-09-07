using System.Windows.Controls;

namespace Console.WPF.Views;

/// <summary>第2页: 测试配置 | 绘图区 | 后处理. 经 App.State 串联.</summary>
public partial class RunWorkspaceView : UserControl
{
    public RunWorkspaceView()
    {
        InitializeComponent();
        Loaded += (_, _) => PostPart.Attach(RunCenter);
    }

    /// <summary>切到本页时刷新: 配置表单按当前项目重载 + 历史会话刷新.</summary>
    public void RefreshAll()
    {
        try { ConfigPart.LoadDefault(); } catch { }
        PostPart.RefreshHistory();
    }
}

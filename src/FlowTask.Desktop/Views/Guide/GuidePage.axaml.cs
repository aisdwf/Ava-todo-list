using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlowTask.Desktop.Views.Guide;

/// <summary>设置 → 操作指南页（spec-onboarding-guide）。拆出 MainWindow，避免主窗 XAML 继续膨胀。</summary>
public partial class GuidePage : UserControl
{
    public GuidePage()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

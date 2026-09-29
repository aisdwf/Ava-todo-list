using Avalonia;
using Avalonia.Controls;

namespace FlowTask.Desktop.Views.Onboarding;

/// <summary>
/// 给主窗控件标注聚光灯锚点（spec-onboarding-guide）。
/// </summary>
/// <remarks>
/// 以附加属性标注而非 <c>x:Name</c> 查找：任务行在 DataTemplate 里，名字不进窗口名称域；
/// 锚点名引用 <see cref="ViewModels.OnboardingTargets"/> 常量，与引导目录同源。
/// </remarks>
public static class OnboardingAnchor
{
    /// <summary>锚点名。</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Key", typeof(OnboardingAnchor));

    public static string? GetKey(Control control) => control.GetValue(KeyProperty);

    public static void SetKey(Control control, string? value) => control.SetValue(KeyProperty, value);
}

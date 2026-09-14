using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Views;

/// <summary>
/// 随手记浮窗：Raycast 风格的极速捕捉胶囊窗 (design-visual-language §3)。
/// </summary>
public partial class QuickCaptureWindow : Window
{
    /// <summary>
    /// 设计器与 XAML 预览专用构造函数。
    /// </summary>
    public QuickCaptureWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 构造随手记浮窗。
    /// </summary>
    /// <param name="vm">随手记视图模型。</param>
    public QuickCaptureWindow(QuickCaptureViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose += Hide;

        // 拖拽整窗：无系统装饰条时，用户只能靠窗体本身移动浮窗
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };

        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Escape:
                    vm.CancelCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    vm.SaveCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        };
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FocusInput();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 浮窗实例被复用（关闭走 Hide 而非 Close），<c>OnOpened</c> 只在首次显示时触发。
    /// 因此再次唤起时必须在可见性变更处重新聚焦，否则用户需先点一下才能输入。
    /// </remarks>
    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && change.NewValue is true)
        {
            FocusInput();
        }
    }

    /// <summary>
    /// 将焦点交给输入框。
    /// </summary>
    /// <remarks>
    /// 延后到 Loaded 优先级执行：窗口刚显示时视觉树尚未完成首次布局，
    /// 立即调用 Focus 会因控件未附加到渲染树而静默失败，用户需要多按一次键才能输入。
    /// </remarks>
    private void FocusInput()
    {
        Dispatcher.UIThread.Post(
            () => this.FindControl<TextBox>("InputBox")?.Focus(),
            DispatcherPriority.Loaded);
    }
}

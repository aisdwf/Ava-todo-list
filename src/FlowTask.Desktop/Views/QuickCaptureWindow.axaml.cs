using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlowTask.Desktop.Controls;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Views;

/// <summary>
/// 快捷小窗：Raycast 风格的极速捕捉胶囊窗 (design-visual-language §3)。
/// </summary>
public partial class QuickCaptureWindow : Window
{
    /// <summary>
    /// 请求宿主统一隐藏小窗，确保后台唤起时不会把主窗口带到前台。
    /// </summary>
    public event Action? RequestHide;

    /// <summary>
    /// 小窗前台热键请求统一走主窗 Toggle，避免本窗 Hide 后同一次按键再被主窗打开。
    /// </summary>
    public event Action? RequestToggleHotkey;

    /// <summary>
    /// 查询进程级全局热键是否已生效；生效时本窗不再重复响应 Alt+Space，
    /// 避免同一次按键被系统级热键与本窗 KeyDown 两条路径各触发一次 Toggle。
    /// </summary>
    private Func<bool>? _isSystemHotkeyActive;

    /// <summary>
    /// 设计器与 XAML 预览专用构造函数。
    /// </summary>
    public QuickCaptureWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 构造快捷小窗。
    /// </summary>
    /// <param name="vm">快捷小窗视图模型。</param>
    /// <param name="isSystemHotkeyActive">
    /// 查询进程级全局热键当前是否已生效，生效时本窗跳过窗内 Alt+Space 处理。
    /// 未提供时默认视为未生效（沿用窗内监听作为唯一路径）。
    /// </param>
    public QuickCaptureWindow(QuickCaptureViewModel vm, Func<bool>? isSystemHotkeyActive = null) : this()
    {
        _isSystemHotkeyActive = isSystemHotkeyActive;
        DataContext = vm;
        vm.RequestClose += () => RequestHide?.Invoke();

        // 拖拽整窗：无系统装饰条时，用户只能靠窗体本身移动浮窗。
        // 单项目列表 (spec-quick-window-single-project-list) 加入项目下拉与任务勾选后，
        // 排除范围须覆盖 ComboBox、CheckBox 与任务列表的 ScrollViewer，
        // 否则点击这些控件会先触发整窗拖拽，吞掉点击/勾选事件。
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
                && e.Source is not ComboBox
                && e.Source is not ComboBoxItem
                && e.Source is not CheckBox
                && !IsDescendantOfInteractiveRegion(e.Source))
            {
                BeginMoveDrag(e);
            }
        };

        // Tunnel：Tab 会先被焦点导航消费，Ctrl+Tab 切换项目须在隧道阶段先拦下
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // 底栏选到日期后焦点回输入框，便于直接回车保存（全键盘路径，R-1.4）
        if (this.FindControl<DueDatePicker>("NewDuePicker") is { } picker)
        {
            picker.PickerClosed += (_, _) => FocusInput();
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickCaptureViewModel vm)
        {
            return;
        }

        // 到期日浮层内的按键（Enter 提交日期、Esc 关浮层）交给浮层自己处理：
        // PopupRoot 的事件路由经由所属 Popup 回到本窗口，本隧道处理器会先于浮层收到，
        // 若不放行，浮层里按 Enter 会保存任务、按 Esc 会隐藏整个小窗。
        if (e.Source is Visual source && source.GetVisualRoot() is PopupRoot)
        {
            return;
        }

        // 系统级热键已生效时窗内不再重复响应，否则同一次按键会触发两次 Toggle
        // 小窗前台：热键只通知主窗统一 Toggle，不在此 Hide（否则焦点回主窗会再开一次）
        // 修饰键判断按平台分流，与 MainWindow 保持一致，避免 Windows 上 Win 键误触
        if (_isSystemHotkeyActive?.Invoke() != true
            && e.Key == Key.Space && MainWindow.IsQuickCaptureModifier(e.KeyModifiers))
        {
            RequestToggleHotkey?.Invoke();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                vm.CancelCommand.Execute(null);
                e.Handled = true;
                break;

            // D6：Ctrl+Tab 下一个、Ctrl+Shift+Tab 上一个；焦点留在输入框，便于切完直接录入
            case Key.Tab when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    vm.SelectPreviousProjectCommand.Execute(null);
                }
                else
                {
                    vm.SelectNextProjectCommand.Execute(null);
                }

                e.Handled = true;
                break;

            case Key.Enter:
                vm.SaveCommand.Execute(null);
                e.Handled = true;
                break;

            // R-1.10：键盘补充入口；点击入口是底栏常驻按钮（用户裁决不得只依赖快捷键）
            case Key.D when e.KeyModifiers == KeyModifiers.Control:
                this.FindControl<DueDatePicker>("NewDuePicker")?.Open();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// 判断指针事件来源是否落在需要保留自身点击行为的交互控件内
    /// （项目下拉、任务列表滚动区、勾选框），
    /// 这些区域内的点击不应被整窗拖拽抢走。
    /// </summary>
    private static bool IsDescendantOfInteractiveRegion(object? source)
    {
        if (source is not Control control)
        {
            return false;
        }

        for (var current = control; current is not null; current = current.Parent as Control)
        {
            // Button 覆盖到期日入口与 P1/P2/P3（RadioButton 派生自 Button）：
            // BeginMoveDrag 会进入系统拖拽循环并吞掉抬起事件，按钮的 Click 永远不触发。
            if (current is ComboBox or ComboBoxItem or CheckBox or ScrollViewer or Button)
            {
                return true;
            }
        }

        return false;
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

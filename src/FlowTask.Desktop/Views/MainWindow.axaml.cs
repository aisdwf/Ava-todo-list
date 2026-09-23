using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Threading;
using FlowTask.Desktop.Appearance;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Views;

/// <summary>
/// 主工作台视窗：承载 Editorial 任务流与昼夜切换水波纹转场。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>水波纹扩散时长，兼顾"丝滑"观感与不拖慢操作节奏。</summary>
    private static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(520);

    /// <summary>主题切换后遮罩淡出时长。</summary>
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(260);

    private readonly QuickCaptureViewModel? _quickCaptureVm;
    private QuickCaptureWindow? _quickCaptureWindow;

    /// <summary>
    /// 热键关闭小窗后的短暂抑制：焦点回主窗时同一次 Option+Space 勿再打开。
    /// </summary>
    private DateTime _suppressQuickCaptureOpenUntil = DateTime.MinValue;

    /// <summary>
    /// 转场进行中标志，防止连续点击导致多个动画叠加、遮罩残留。
    /// </summary>
    private bool _isRevealRunning;

    /// <summary>
    /// 设计器与 XAML 预览专用构造函数。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 构造主视窗。
    /// </summary>
    /// <param name="vm">主视图模型。</param>
    /// <param name="quickCaptureVm">快捷小窗视图模型。</param>
    public MainWindow(MainViewModel vm, QuickCaptureViewModel quickCaptureVm)
    {
        InitializeComponent();

        DataContext = vm;
        _quickCaptureVm = quickCaptureVm;

        vm.RequestOpenQuickCapture += ToggleQuickCaptureWindow;
        vm.MaterialPresetChanged += preset => AppearanceCoordinator.ApplyMaterial(this, preset.Id);

        // 窗口背景由代码构建的具体笔刷承载，不随主题字典重新求值，
        // 因此昼夜切换后必须按当前材质档位重建底色。
        vm.ThemeApplied += () => AppearanceCoordinator.RefreshMaterialBackground(this, vm.SelectedMaterial.Id);

        // 主题按钮不绑定命令：主题必须在水波纹覆盖全屏后才切换，
        // 否则用户会先看到底层界面突变、再看到遮罩扩散，动效失去意义。
        if (this.FindControl<Button>("ThemeToggleButton") is { } themeButton)
        {
            themeButton.Click += async (_, _) => await RunThemeRevealAsync(themeButton, vm);
            // 焦点留在按钮时，macOS 会把 Space 当「激活按钮」；命中小窗热键修饰键时必须让给快捷小窗热键
            themeButton.AddHandler(
                KeyDownEvent,
                (_, e) =>
                {
                    if (e.Key == Key.Space && IsQuickCaptureModifier(e.KeyModifiers))
                    {
                        ToggleQuickCaptureWindow();
                        e.Handled = true;
                    }
                },
                RoutingStrategies.Tunnel);
        }

        // Tunnel：先于子控件（含聚焦的主题钮）处理热键，避免 Space 被当成按钮激活
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        Opened += async (_, _) =>
        {
            AppearanceCoordinator.ApplyTheme(vm.IsDarkTheme);
            AppearanceCoordinator.ApplyMaterial(this, vm.SelectedMaterial.Id);
            await vm.InitializeAsync();
        };
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Alt+Space (Windows) / Option(Meta)+Space (macOS) 唤起快捷小窗；按平台分流，不跨平台混判修饰键
        if (e.Key == Key.Space && IsQuickCaptureModifier(e.KeyModifiers))
        {
            ToggleQuickCaptureWindow();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 判断按键修饰符是否命中当前平台的快捷小窗唤起手势。
    /// </summary>
    /// <remarks>
    /// Windows 用 <c>Alt+Space</c>；macOS 用 <c>Option(Meta)+Space</c>。
    /// 此前两平台的修饰键判断混在一起（<c>Alt || Meta</c>），
    /// 导致 Windows 上 Win 键（映射为 <see cref="KeyModifiers.Meta"/>）也能误触唤起。
    /// </remarks>
    internal static bool IsQuickCaptureModifier(KeyModifiers modifiers)
        => OperatingSystem.IsMacOS()
            ? modifiers.HasFlag(KeyModifiers.Meta)
            : modifiers.HasFlag(KeyModifiers.Alt);

    /// <summary>
    /// 以按钮圆心为原点执行全屏径向水波纹扩散，并在遮罩完全覆盖后切换主题 (spec-editorial-and-ripple-theme §2)。
    /// </summary>
    /// <remarks>
    /// 为什么用 <see cref="Transitions"/> 而非 <see cref="Animation"/>：
    /// Avalonia 11 的 <c>Animation.RunAsync</c> 对 <c>RenderTransform</c> 没有内建 animator，
    /// 直接以关键帧插值该属性会抛 <c>InvalidOperationException</c>
    /// （"No animator registered for the property RenderTransform"），
    /// 该异常从 async 事件处理器逃逸到进程顶层，表现为点击主题按钮即闪退。
    /// 声明式过渡由合成器驱动，既避免了缺失 animator 的问题，
    /// 也不需要业务代码中的 sleep 式时序控制（Article 9）。
    /// </remarks>
    private async Task RunThemeRevealAsync(Control origin, MainViewModel vm)
    {
        var canvas = this.FindControl<Canvas>("RevealCanvas");
        var circle = this.FindControl<Ellipse>("RevealCircle");

        // 动画控件缺失或重入时退化为直接切换，保证换肤功能不依赖动效可用性
        if (canvas is null || circle is null || _isRevealRunning)
        {
            vm.ApplyTheme(!vm.IsDarkTheme);
            Focus();
            return;
        }

        _isRevealRunning = true;
        try
        {
            var center = origin.TranslatePoint(
                             new Point(origin.Bounds.Width / 2, origin.Bounds.Height / 2), this)
                         ?? new Point(Bounds.Width - 60, 60);

            // 覆盖全窗所需半径：圆心到最远角的距离
            var radius = Math.Sqrt(
                Math.Pow(Math.Max(center.X, Bounds.Width - center.X), 2) +
                Math.Pow(Math.Max(center.Y, Bounds.Height - center.Y), 2));

            var targetIsDark = !vm.IsDarkTheme;
            circle.Fill = new SolidColorBrush(AppearanceCoordinator.ResolveWindowSurfaceColor(targetIsDark));
            circle.Width = radius * 2;
            circle.Height = radius * 2;
            Canvas.SetLeft(circle, center.X - radius);
            Canvas.SetTop(circle, center.Y - radius);
            circle.RenderTransformOrigin = RelativePoint.Center;

            // 先无过渡地归零，避免复用时从上一次的终态开始扩散
            circle.Transitions = null;
            circle.RenderTransform = TransformOperations.Parse("scale(0)");
            circle.Opacity = 1;
            canvas.IsVisible = true;

            circle.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = RevealDuration,
                    Easing = new CubicEaseOut()
                },
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = FadeOutDuration,
                    Easing = new CubicEaseOut()
                }
            };

            // 让归零状态先提交一帧，否则设置终值时过渡没有起点可插值
            await Task.Yield();

            circle.RenderTransform = TransformOperations.Parse("scale(1)");
            await Task.Delay(RevealDuration);

            // 遮罩此刻已铺满窗口，换肤对用户不可见
            vm.ApplyTheme(targetIsDark);

            // 遮罩色与新主题底色一致，淡出过程中不会露出旧配色
            circle.Opacity = 0;
            await Task.Delay(FadeOutDuration);
        }
        finally
        {
            // 无论动画是否正常结束都必须清理，否则残留遮罩会吞掉整个界面的交互
            circle.Transitions = null;
            circle.Opacity = 0;
            circle.Width = 0;
            circle.Height = 0;
            canvas.IsVisible = false;
            _isRevealRunning = false;
            // 把焦点从主题钮挪走，避免后续 Option+Space 被当成「再点一次按钮」
            Focus();
        }
    }

    /// <summary>
    /// 供进程级热键回调：切到 UI 线程后显隐小窗（spec-quick-window-hotkey-capture）。
    /// </summary>
    public void ToggleQuickCaptureFromHotkey() => _ = ToggleQuickCaptureWindowAsync();

    /// <summary>
    /// 唤起或隐藏快捷小窗。窗口实例复用以保证亚秒级唤起 (design-visual-language §3)。
    /// </summary>
    private void ToggleQuickCaptureWindow() => _ = ToggleQuickCaptureWindowAsync();

    private async Task ToggleQuickCaptureWindowAsync()
    {
        if (_quickCaptureVm is null)
        {
            return;
        }

        if (_quickCaptureWindow is null)
        {
            _quickCaptureWindow = new QuickCaptureWindow(_quickCaptureVm);
            // 小窗前台热键统一走本方法，避免小窗自 Hide 后同一次按键再被主窗打开
            _quickCaptureWindow.RequestToggleHotkey += ToggleQuickCaptureWindow;

            // 拦截关闭改为隐藏：重建窗口会丢失焦点预热，导致再次唤起有可感知延迟
            _quickCaptureWindow.Closing += (_, e) =>
            {
                e.Cancel = true;
                _quickCaptureWindow?.Hide();
            };
        }

        if (_quickCaptureWindow.IsVisible)
        {
            // 不 Activate 主窗：会抢前台造成「跳动」；抑制窗避免焦点回流后同键再开
            _suppressQuickCaptureOpenUntil = DateTime.UtcNow.AddMilliseconds(350);
            _quickCaptureWindow.Hide();
            return;
        }

        if (DateTime.UtcNow < _suppressQuickCaptureOpenUntil)
        {
            return;
        }

        await _quickCaptureVm.PrepareAsync();
        _quickCaptureWindow.Show();
        _quickCaptureWindow.Activate();
    }

    /// <summary>
    /// 单击项目行：切换到该项目的任务列表。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 以事件处理而非 <c>Button</c> 承载：项目行内已含改色与删除两个按钮，
    /// 嵌套按钮会产生点击热区竞争 —— 点删除会同时触发选中。
    /// </para>
    /// <para>
    /// <b>关键</b>：Avalonia 的 <c>Tapped</c> 沿可视树冒泡，
    /// 行内按钮被点击时其事件已标记 <c>Handled</c>，因此不会到达此处；
    /// 但重命名输入框的点击必须显式排除，否则点击输入框会切换筛选、
    /// 使编辑态被 <c>LoadProjectsAsync</c> 重建而中断。
    /// </para>
    /// </remarks>
    private void OnProjectRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { DataContext: ProjectItemViewModel row })
        {
            return;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.BeginRenameProjectCommand.Execute(row);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 单击项目行切换筛选。重命名进行中时忽略，避免打断编辑。
    /// </summary>
    private void OnProjectRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { DataContext: ProjectItemViewModel row })
        {
            return;
        }

        // 重命名进行中：点击应落在输入框上，不触发筛选切换
        if (row.IsRenaming)
        {
            return;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.SelectProjectCommand.Execute(row);
        }
    }
}

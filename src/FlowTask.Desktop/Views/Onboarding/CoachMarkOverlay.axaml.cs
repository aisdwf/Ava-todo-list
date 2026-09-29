using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Views.Onboarding;

/// <summary>
/// 聚光灯引导遮罩：挖空当前步骤的真实控件，气泡贴在旁边（spec-onboarding-guide，R-6.1）。
/// </summary>
/// <remarks>
/// <para>
/// 定位完全在视图层：<see cref="OnboardingViewModel"/> 只给出锚点名，
/// 这里在主窗可视树中找带 <see cref="OnboardingAnchor.KeyProperty"/> 的可见控件，
/// 每次布局更新重新量一次，窗口缩放、列表载入后挖空仍对得上。
/// </para>
/// <para>
/// 键盘在窗口级 Tunnel 拦截：遮罩期间焦点可能仍停在新任务输入框，
/// 若只在本控件上监听，回车会先落到输入框里建出一条任务。
/// </para>
/// </remarks>
public partial class CoachMarkOverlay : UserControl
{
    /// <summary>挖空框比控件外扩的留白。</summary>
    private const double SpotPadding = 8;

    /// <summary>气泡与挖空框、窗口边缘的间距。</summary>
    private const double Gap = 16;

    /// <summary>窗口低于此高度时气泡改用紧凑舞台。</summary>
    private const double CompactStageThreshold = 720;

    private const double RegularSceneHeight = 168;
    private const double CompactSceneHeight = 120;

    private static readonly TimeSpan MoveDuration = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(260);

    private readonly Canvas _stage;
    private readonly Border _hitShield;
    private readonly Border _spotlight;
    private readonly Border _bubble;
    private readonly Control _stepScene;
    private readonly DispatcherTimer _hideTimer = new() { Interval = FadeDuration };

    private OnboardingViewModel? _vm;
    private TopLevel? _topLevel;
    private Rect? _lastSpot;

    /// <summary>
    /// 最近一次算出的挖空目标（本控件坐标系）。挖空框的 Left/Width 在过渡中是插值中间值，
    /// 测试与诊断读这里的终值。
    /// </summary>
    public Rect? SpotTarget => _lastSpot;

    /// <summary>最近一次算出的气泡左上角终值。</summary>
    public Point? BubbleTarget { get; private set; }
    private Size _lastStage;
    private bool _animateMoves;

    public CoachMarkOverlay()
    {
        AvaloniaXamlLoader.Load(this);
        _stage = this.FindControl<Canvas>("Stage")!;
        _hitShield = this.FindControl<Border>("HitShield")!;
        _spotlight = this.FindControl<Border>("Spotlight")!;
        _bubble = this.FindControl<Border>("Bubble")!;
        _stepScene = this.FindControl<Control>("StepScene")!;
        Focusable = true;

        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (_vm?.IsActive != true)
            {
                IsVisible = false;
            }
        };
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as OnboardingViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            SyncActive();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
        _topLevel.AddHandler(TextInputEvent, OnTopLevelTextInput, RoutingStrategies.Tunnel);
        _topLevel.LayoutUpdated += OnTopLevelLayoutUpdated;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _topLevel.RemoveHandler(TextInputEvent, OnTopLevelTextInput);
            _topLevel.LayoutUpdated -= OnTopLevelLayoutUpdated;
            _topLevel = null;
        }

        _hideTimer.Stop();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OnboardingViewModel.IsActive):
                SyncActive();
                break;
            case nameof(OnboardingViewModel.StepIndex):
                _lastSpot = null;
                Reposition();
                break;
        }
    }

    private void SyncActive()
    {
        if (_vm?.IsActive == true)
        {
            _hideTimer.Stop();
            _lastSpot = null;
            // 首帧直接落位：从窗口角落滑进来反而像闪了一下
            _animateMoves = false;
            IsVisible = true;
            Opacity = 1;
            Focus();
            Dispatcher.UIThread.Post(Reposition, DispatcherPriority.Loaded);
            return;
        }

        Opacity = 0;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void OnTopLevelLayoutUpdated(object? sender, EventArgs e)
    {
        if (_vm?.IsActive == true)
        {
            Reposition();
        }
    }

    /// <summary>量出当前锚点、挪挖空与气泡。位置没变时直接返回，避免 LayoutUpdated 自激。</summary>
    private void Reposition()
    {
        if (_vm is not { IsActive: true } vm || _topLevel is null)
        {
            return;
        }

        var stageSize = _stage.Bounds.Size;
        if (stageSize.Width <= 0 || stageSize.Height <= 0)
        {
            return;
        }

        var spot = FindSpot(vm.CurrentStep.TargetKeys) ?? CenterFallback(stageSize);
        if (_lastSpot == spot && _lastStage == stageSize)
        {
            return;
        }

        _lastSpot = spot;
        _lastStage = stageSize;

        _hitShield.Width = stageSize.Width;
        _hitShield.Height = stageSize.Height;

        EnsureTransitions();
        Place(_spotlight, spot.X, spot.Y);
        _spotlight.Width = spot.Width;
        _spotlight.Height = spot.Height;

        _stepScene.Height = stageSize.Height < CompactStageThreshold ? CompactSceneHeight : RegularSceneHeight;
        _bubble.Measure(Size.Infinity);
        var bubbleSize = _bubble.DesiredSize;
        var origin = PlaceBubble(spot, bubbleSize, stageSize);
        BubbleTarget = origin;
        Place(_bubble, origin.X, origin.Y);

        _animateMoves = true;
    }

    /// <summary>按目录顺序取第一个可见锚点：如列表为空时「任务行」退回「空状态」。</summary>
    private Rect? FindSpot(IReadOnlyList<string> keys)
    {
        if (_topLevel is null)
        {
            return null;
        }

        var anchors = _topLevel.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => OnboardingAnchor.GetKey(control) is not null
                              && control.IsEffectivelyVisible
                              && control.Bounds.Width > 0
                              && control.Bounds.Height > 0)
            .ToList();

        foreach (var key in keys)
        {
            var anchor = anchors.FirstOrDefault(control => OnboardingAnchor.GetKey(control) == key);
            if (anchor?.TranslatePoint(default, _stage) is not { } topLeft)
            {
                continue;
            }

            var rect = new Rect(topLeft, anchor.Bounds.Size).Inflate(SpotPadding);
            // 贴边控件外扩后会越出窗口，收回到可见区域内
            return rect.Intersect(new Rect(_stage.Bounds.Size).Deflate(2));
        }

        return null;
    }

    /// <summary>找不到锚点时退为窗口中央的零尺寸挖空，气泡居中，引导不中断。</summary>
    private static Rect CenterFallback(Size stage) => new(stage.Width / 2, stage.Height / 2, 0, 0);

    /// <summary>
    /// 依次尝试挖空右、下、上、左四个方向，选第一个放得下且不压住挖空的。
    /// 都放不下（最小窗口下挖空横贯内容区）时，放进挖空下方或上方剩余空间较大的一侧，
    /// 必要时压住挖空边缘，但始终完整留在窗口内：气泡越界等于按钮点不到。
    /// </summary>
    internal static Point PlaceBubble(Rect spot, Size bubble, Size stage)
    {
        var alignedY = Math.Clamp(spot.Y, Gap, Math.Max(Gap, stage.Height - bubble.Height - Gap));
        var alignedX = Math.Clamp(spot.X, Gap, Math.Max(Gap, stage.Width - bubble.Width - Gap));

        var candidates = new[]
        {
            new Point(spot.Right + Gap, alignedY),
            new Point(alignedX, spot.Bottom + Gap),
            new Point(alignedX, spot.Y - Gap - bubble.Height),
            new Point(spot.X - Gap - bubble.Width, alignedY)
        };

        foreach (var candidate in candidates)
        {
            var rect = new Rect(candidate, bubble);
            if (rect.X >= Gap
                && rect.Y >= Gap
                && rect.Right <= stage.Width - Gap
                && rect.Bottom <= stage.Height - Gap
                && !rect.Intersects(spot))
            {
                return candidate;
            }
        }

        var spaceBelow = stage.Height - spot.Bottom;
        var spaceAbove = spot.Y;
        var preferredY = spaceBelow >= spaceAbove
            ? spot.Bottom + Gap
            : spot.Y - Gap - bubble.Height;
        var rightAlignedX = stage.Width - bubble.Width - Gap;

        return new Point(
            Math.Max(Gap, rightAlignedX),
            Math.Clamp(preferredY, Gap, Math.Max(Gap, stage.Height - bubble.Height - Gap)));
    }

    private void EnsureTransitions()
    {
        if (!_animateMoves)
        {
            _spotlight.Transitions = null;
            _bubble.Transitions = null;
            return;
        }

        _spotlight.Transitions ??= MoveTransitions(includeSize: true);
        _bubble.Transitions ??= MoveTransitions(includeSize: false);
    }

    private static Transitions MoveTransitions(bool includeSize)
    {
        var transitions = new Transitions
        {
            new DoubleTransition { Property = Canvas.LeftProperty, Duration = MoveDuration, Easing = new CubicEaseOut() },
            new DoubleTransition { Property = Canvas.TopProperty, Duration = MoveDuration, Easing = new CubicEaseOut() }
        };

        if (includeSize)
        {
            transitions.Add(new DoubleTransition { Property = WidthProperty, Duration = MoveDuration, Easing = new CubicEaseOut() });
            transitions.Add(new DoubleTransition { Property = HeightProperty, Duration = MoveDuration, Easing = new CubicEaseOut() });
        }

        return transitions;
    }

    private static void Place(Control control, double x, double y)
    {
        Canvas.SetLeft(control, x);
        Canvas.SetTop(control, y);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is not { IsActive: true } vm)
        {
            return;
        }

        // Tab 到气泡里的「上一步 / 跳过」后按回车或空格，应激活该按钮本身，而不是一律前进
        if (e.Key is Key.Enter or Key.Space
            && e.Source is Visual source
            && source.FindAncestorOfType<Button>(includeSelf: true) is { } button
            && _bubble.IsVisualAncestorOf(button))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                vm.SkipCommand.Execute(null);
                break;
            case Key.Right:
            case Key.Enter:
            case Key.Space when e.KeyModifiers == KeyModifiers.None:
                vm.NextCommand.Execute(null);
                break;
            case Key.Left:
                vm.PreviousCommand.Execute(null);
                break;
            case Key.Tab:
                // Tab 在遮罩内的按钮间切换，不拦
                return;
        }

        // 其余按键一律吞掉：遮罩下的输入框、快捷键不应在引导期间被触发
        e.Handled = true;
    }

    private void OnTopLevelTextInput(object? sender, TextInputEventArgs e)
    {
        if (_vm?.IsActive == true)
        {
            e.Handled = true;
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Views.Guide;

/// <summary>
/// 循环播放一个矢量演示场景的舞台（spec-onboarding-guide，R-6.2）。
/// </summary>
/// <remarks>
/// <para>
/// 驱动方式：<see cref="DispatcherTimer"/> 逐步改属性，Transitions 负责补间。
/// 计时器只推进用户可见的演示，不承载任何业务判断（Article 9 动画豁免）。
/// </para>
/// <para>
/// 只在挂到可视树上时运行：设置页切走、引导关闭后自动停，避免后台空转计时器。
/// 换场景时整块重建画布，旧绑定一并释放，不在旧元素上叠加新状态。
/// </para>
/// </remarks>
public sealed class GuideSceneHost : Border
{
    /// <summary>要演示的场景。</summary>
    public static readonly StyledProperty<GuideSceneKind?> SceneProperty =
        AvaloniaProperty.Register<GuideSceneHost, GuideSceneKind?>(nameof(Scene));

    private readonly DispatcherTimer _timer = new();
    private SceneBrushes? _brushes;
    private SceneScript? _script;
    private int _stepIndex;

    public GuideSceneHost()
    {
        ClipToBounds = true;
        CornerRadius = new CornerRadius(8);
        _timer.Tick += (_, _) => Advance();
    }

    /// <summary>要演示的场景。</summary>
    public GuideSceneKind? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Teardown();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty && VisualRoot is not null)
        {
            Rebuild();
        }
        else if (change.Property == IsVisibleProperty)
        {
            // 设置页各分区以 IsVisible 切换，不会脱离可视树；隐藏时同样停表
            if (IsEffectivelyVisible && VisualRoot is not null)
            {
                Restart();
            }
            else
            {
                _timer.Stop();
            }
        }
    }

    private void Rebuild()
    {
        Teardown();
        if (Scene is not { } kind)
        {
            return;
        }

        _brushes = new SceneBrushes();
        var canvas = new Canvas
        {
            Width = GuideScenes.StageWidth,
            Height = GuideScenes.StageHeight,
            ClipToBounds = true
        };
        _script = GuideScenes.Build(kind, new SceneLayer(canvas, _brushes));
        Child = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };
        Restart();
    }

    private void Restart()
    {
        if (_script is null)
        {
            return;
        }

        _timer.Stop();
        _stepIndex = 0;
        _script.Reset();
        // 首步前留一拍，让复位状态先上屏，过渡才有起点
        _timer.Interval = TimeSpan.FromMilliseconds(600);
        _timer.Start();
    }

    /// <summary>当前场景的脚本步数；未构建时为 0。</summary>
    public int StepCount => _script?.Steps.Count ?? 0;

    /// <summary>
    /// 不等计时器、立即推进一步。供 headless 测试逐步驱动整轮脚本，不依赖真实时间（Article 9）。
    /// </summary>
    public void StepOnce() => Advance();

    private void Advance()
    {
        if (_script is null || _script.Steps.Count == 0)
        {
            _timer.Stop();
            return;
        }

        if (_stepIndex >= _script.Steps.Count)
        {
            // 一轮结束：先淡出复位再开始下一轮，循环接缝不跳变
            _stepIndex = 0;
            _script.Reset();
            _timer.Interval = TimeSpan.FromMilliseconds(700);
            return;
        }

        var step = _script.Steps[_stepIndex++];
        step.Apply();
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, step.HoldMs));
    }

    private void Teardown()
    {
        _timer.Stop();
        _script = null;
        Child = null;
        _brushes?.Dispose();
        _brushes = null;
    }
}

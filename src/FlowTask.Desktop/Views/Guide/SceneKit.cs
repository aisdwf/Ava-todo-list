using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Path = Avalonia.Controls.Shapes.Path;

namespace FlowTask.Desktop.Views.Guide;

/// <summary>演示脚本的一步：先执行 <see cref="Apply"/>，再停留 <see cref="HoldMs"/> 毫秒。</summary>
internal sealed record SceneStep(int HoldMs, Action Apply);

/// <summary>
/// 一个场景的循环脚本。<see cref="Reset"/> 在画面隐藏时把所有元素拨回起点。
/// </summary>
internal sealed class SceneScript
{
    public List<SceneStep> Steps { get; } = new();

    public Action Reset { get; set; } = () => { };

    public SceneScript Then(int holdMs, Action apply)
    {
        Steps.Add(new SceneStep(holdMs, apply));
        return this;
    }

    public SceneScript Wait(int holdMs) => Then(holdMs, static () => { });

    /// <summary>逐字打出 <paramref name="text"/>；<paramref name="prefix"/> 为已存在的前缀。</summary>
    public SceneScript Type(TextBlock target, string text, int perCharMs = 130, string prefix = "")
    {
        for (var i = prefix.Length + 1; i <= text.Length; i++)
        {
            var partial = text[..i];
            Then(perCharMs, () => target.Text = partial);
        }

        return this;
    }

    /// <summary>
    /// 在 (x, y) 模拟一次点击：指针尖端放出一圈扩散的强调色环。
    /// 不缩放指针本身：平移与缩放同时插值时，缩放会连带放大平移量，指针会跳位。
    /// </summary>
    public SceneScript Click(SceneCursor cursor, double x, double y)
        => Then(30, () =>
            {
                cursor.Ring.Transitions = null;
                Canvas.SetLeft(cursor.Ring, x - (SceneCursor.RingSize / 2));
                Canvas.SetTop(cursor.Ring, y - (SceneCursor.RingSize / 2));
                Motion.Place(cursor.Ring, 0, 0, 0.3);
                cursor.Ring.Opacity = 0.9;
            })
            .Then(300, () =>
            {
                Motion.Animate(cursor.Ring);
                Motion.Place(cursor.Ring, 0, 0, 1.6);
                cursor.Ring.Opacity = 0;
            });

    /// <summary>指针移动到 (x, y)（指针尖端坐标）。</summary>
    public SceneScript MoveTo(SceneCursor cursor, double x, double y, int holdMs = 560)
        => Then(holdMs, () => Motion.Place(cursor.Pointer, x, y));
}

/// <summary>场景中的鼠标指针与点击环。</summary>
internal sealed record SceneCursor(Path Pointer, Ellipse Ring)
{
    public const double RingSize = 16;

    /// <summary>把指针停到右下角待命，点击环隐藏。</summary>
    public void Park()
    {
        Motion.Place(Pointer, GuideScenes.StageWidth - 26, GuideScenes.StageHeight - 30);
        Ring.Opacity = 0;
    }
}

/// <summary>
/// 场景动效的统一参数。只用 Transitions（design-visual-language §5.1：Avalonia 11.2 的
/// <c>Animation</c> 不支持 <c>RenderTransform</c>），时长落在既有 160–520ms 区间，CubicEaseOut。
/// </summary>
internal static class Motion
{
    public static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(260);
    public static readonly TimeSpan Move = TimeSpan.FromMilliseconds(420);
    public static readonly TimeSpan Grow = TimeSpan.FromMilliseconds(360);
    public static readonly TimeSpan Tint = TimeSpan.FromMilliseconds(180);
    public static readonly TimeSpan Ripple = TimeSpan.FromMilliseconds(520);

    /// <summary>给元素挂上场景通用过渡。</summary>
    public static void Animate(Control control, TimeSpan? move = null)
    {
        var transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = Fade, Easing = new CubicEaseOut() },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = move ?? Move,
                Easing = new CubicEaseOut()
            },
            new DoubleTransition { Property = Layoutable.WidthProperty, Duration = Grow, Easing = new CubicEaseOut() },
            new DoubleTransition { Property = Layoutable.HeightProperty, Duration = Grow, Easing = new CubicEaseOut() }
        };

        switch (control)
        {
            case Border:
                transitions.Add(new BrushTransition { Property = Border.BackgroundProperty, Duration = Tint });
                transitions.Add(new BrushTransition { Property = Border.BorderBrushProperty, Duration = Tint });
                break;
            case Shape:
                transitions.Add(new BrushTransition { Property = Shape.FillProperty, Duration = Tint });
                transitions.Add(new BrushTransition { Property = Shape.StrokeProperty, Duration = Tint });
                break;
            case TextBlock:
                transitions.Add(new BrushTransition { Property = TextBlock.ForegroundProperty, Duration = Tint });
                break;
        }

        control.Transitions = transitions;
    }

    /// <summary>
    /// 平移 + 缩放。始终写成同一结构的两段变换，过渡才能逐项插值；结构不同时 Avalonia 会直接跳变。
    /// </summary>
    public static void Place(Control control, double dx, double dy, double scale = 1)
        => control.RenderTransform = TransformOperations.Parse(string.Create(
            CultureInfo.InvariantCulture,
            $"translate({dx}px,{dy}px) scale({scale})"));

    public static void Show(Control control, bool visible = true) => control.Opacity = visible ? 1 : 0;

    public static void Hide(Control control) => control.Opacity = 0;
}

/// <summary>
/// 场景元素的主题笔刷绑定。换色时先释放旧绑定再绑新键，笔刷过渡负责渐变；
/// 走 DynamicResource 同源令牌，昼夜与主题预设切换后场景自动跟随（design-visual-language §2.4）。
/// </summary>
internal sealed class SceneBrushes : IDisposable
{
    private readonly Dictionary<(AvaloniaObject Target, AvaloniaProperty Property), IDisposable> _bindings = new();

    public void Set(StyledElement target, AvaloniaProperty property, string? resourceKey)
    {
        if (_bindings.Remove((target, property), out var previous))
        {
            previous.Dispose();
        }

        if (resourceKey is null)
        {
            target.ClearValue(property);
            return;
        }

        _bindings[(target, property)] = target.Bind(property, target.GetResourceObservable(resourceKey));
    }

    public void Dispose()
    {
        foreach (var binding in _bindings.Values)
        {
            binding.Dispose();
        }

        _bindings.Clear();
    }
}

/// <summary>
/// 一层画布及其绘制原语。坐标以场景设计尺寸（<see cref="GuideScenes.StageWidth"/>）为准，
/// 最终由 <see cref="Viewbox"/> 统一缩放。
/// </summary>
internal sealed class SceneLayer
{
    public SceneLayer(Canvas canvas, SceneBrushes brushes)
    {
        Canvas = canvas;
        Brushes = brushes;
    }

    public Canvas Canvas { get; }

    public SceneBrushes Brushes { get; }

    /// <summary>矩形块：底色、描边、圆角。</summary>
    public Border Box(double x, double y, double w, double h, string? fill, double radius = 4, string? stroke = null)
    {
        var box = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(radius),
            BorderThickness = new Thickness(stroke is null ? 0 : 1),
            ClipToBounds = true
        };
        Brushes.Set(box, Border.BackgroundProperty, fill);
        Brushes.Set(box, Border.BorderBrushProperty, stroke);
        return Add(box, x, y);
    }

    /// <summary>文字。</summary>
    public TextBlock Text(
        double x,
        double y,
        string text,
        double size,
        string foreground = "TextPrimaryBrush",
        FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight
        };
        Brushes.Set(block, TextBlock.ForegroundProperty, foreground);
        return Add(block, x, y);
    }

    /// <summary>微标：小字号、加粗、加字距。</summary>
    public TextBlock Micro(double x, double y, string text, string foreground = "TextTertiaryBrush")
    {
        var block = Text(x, y, text, 7, foreground, FontWeight.Bold);
        block.LetterSpacing = 1.2;
        return block;
    }

    /// <summary>圆。</summary>
    public Ellipse Dot(double x, double y, double diameter, string? fill, string? stroke = null, double thickness = 1.4)
    {
        var dot = new Ellipse { Width = diameter, Height = diameter, StrokeThickness = thickness };
        Brushes.Set(dot, Shape.FillProperty, fill);
        Brushes.Set(dot, Shape.StrokeProperty, stroke);
        return Add(dot, x, y);
    }

    /// <summary>场景内描边图标的默认线宽（像素）。</summary>
    public const double IconStroke = 1.6;

    /// <summary>
    /// 场景里模拟主窗顶栏三钮时用的加粗线宽（像素）。
    /// </summary>
    /// <remarks>
    /// 主窗顶栏为 <c>Path.StrokeIcon.Bold</c>（2.25）× 20/24 ≈ 1.88px；场景图标画 18px，
    /// 按同一比例取 1.88 × 18/20 ≈ 1.7，使指南里的顶栏与真实顶栏观感一致（spec-icon-refresh）。
    /// 场景 Path 直接按像素绘制、没有 Viewbox，故不能复用样式类里的 24 基准值。
    /// </remarks>
    public const double HeaderIconStroke = 1.7;

    /// <summary>描边图标，几何取自 Icons.axaml。</summary>
    public Path Icon(double x, double y, double size, string geometryKey, string stroke = "TextSecondaryBrush",
        double thickness = IconStroke)
    {
        var path = new Path
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Data = ResolveGeometry(geometryKey)
        };
        Brushes.Set(path, Shape.StrokeProperty, stroke);
        return Add(path, x, y);
    }

    /// <summary>勾。</summary>
    public Path Tick(double x, double y, double size, string stroke = "OnAccentBrush")
    {
        var tick = Icon(x, y, size, "IconCheck", stroke);
        tick.StrokeThickness = 2;
        return tick;
    }

    /// <summary>键帽：与侧栏 KeyCap 同一令牌。</summary>
    public Border Key(double x, double y, string label)
    {
        var width = Math.Max(20, 8 + (label.Length * 6));
        var key = Box(x, y, width, 18, "KeyCapSurfaceBrush", 4, "KeyCapBorderBrush");
        var text = new TextBlock
        {
            Text = label,
            FontSize = 8.5,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Brushes.Set(text, TextBlock.ForegroundProperty, "TextSecondaryBrush");
        key.Child = text;
        return key;
    }

    /// <summary>按下或松开键帽：强调色底 + 下沉 1.5px。</summary>
    public void Press(Border key, bool pressed)
    {
        Brushes.Set(key, Border.BackgroundProperty, pressed ? "AccentSubtleBrush" : "KeyCapSurfaceBrush");
        Brushes.Set(key, Border.BorderBrushProperty, pressed ? "AccentBrush" : "KeyCapBorderBrush");
        if (key.Child is TextBlock text)
        {
            Brushes.Set(text, TextBlock.ForegroundProperty, pressed ? "AccentBrush" : "TextSecondaryBrush");
        }

        Motion.Place(key, 0, pressed ? 1.5 : 0);
    }

    /// <summary>药丸按钮，可切换选中态。</summary>
    public Border Pill(double x, double y, double w, string label, string? fill, string foreground)
        => Cell(x, y, w, 16, label, fill, foreground, 8, 7.5, FontWeight.Bold);

    /// <summary>带居中文字的块，文字随 <see cref="Tint"/> 换色。</summary>
    public Border Cell(
        double x,
        double y,
        double w,
        double h,
        string label,
        string? fill,
        string foreground,
        double radius = 4,
        double size = 8.5,
        FontWeight weight = FontWeight.SemiBold)
    {
        var cell = Box(x, y, w, h, fill, radius);
        var text = new TextBlock
        {
            Text = label,
            FontSize = size,
            FontWeight = weight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Brushes.Set(text, TextBlock.ForegroundProperty, foreground);
        cell.Child = text;
        return cell;
    }

    /// <summary>改药丸底色与字色。</summary>
    public void Tint(Border pill, string? fill, string foreground)
    {
        Brushes.Set(pill, Border.BackgroundProperty, fill);
        if (pill.Child is TextBlock text)
        {
            Brushes.Set(text, TextBlock.ForegroundProperty, foreground);
        }
    }

    /// <summary>嵌套分组：自带裁剪的子画布，整组可一起平移、缩放、淡出。</summary>
    public (Border Frame, SceneLayer Layer) Group(
        double x,
        double y,
        double w,
        double h,
        string? fill = null,
        double radius = 6,
        string? stroke = null)
    {
        var frame = Box(x, y, w, h, fill, radius, stroke);
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        frame.Child = canvas;
        return (frame, new SceneLayer(canvas, Brushes));
    }

    /// <summary>
    /// 鼠标指针与点击环。指针放在画布原点，靠平移定位，尖端即平移后的坐标；须最后创建以压在最上层。
    /// </summary>
    public SceneCursor Cursor()
    {
        var ring = new Ellipse
        {
            Width = SceneCursor.RingSize,
            Height = SceneCursor.RingSize,
            StrokeThickness = 1.6,
            Opacity = 0,
            IsHitTestVisible = false
        };
        Brushes.Set(ring, Shape.StrokeProperty, "AccentBrush");
        Add(ring, 0, 0);

        var pointer = new Path
        {
            Width = 11,
            Height = 17,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1,
            StrokeJoin = PenLineJoin.Round,
            Data = ResolveGeometry("GuidePointer")
        };
        Brushes.Set(pointer, Shape.FillProperty, "TextPrimaryBrush");
        Brushes.Set(pointer, Shape.StrokeProperty, "CardSurfaceBrush");
        Add(pointer, 0, 0);
        Motion.Animate(pointer, TimeSpan.FromMilliseconds(480));

        var cursor = new SceneCursor(pointer, ring);
        cursor.Park();
        return cursor;
    }

    /// <summary>改药丸或键帽上的文字。</summary>
    public static void Relabel(Border pill, string label)
    {
        if (pill.Child is TextBlock text)
        {
            text.Text = label;
        }
    }

    /// <summary>
    /// 一条任务行：圆环、完成填充、勾、标题、删除线与发丝线。
    /// 删除线宽度按中文 1 字 ≈ 1 个字号估算；场景文案全为中文，无需实测。
    /// </summary>
    public TaskRowParts TaskRow(double y, string title, double width = 296, double x = 12)
    {
        var titleWidth = title.Length * 11.2;
        var (frame, layer) = Group(x, y, width, 28);
        var hover = layer.Box(0, 0, width, 27, "HairlineBrush", 4);
        Motion.Hide(hover);
        var ring = layer.Dot(4, 8, 12, null, "TextTertiaryBrush");
        var fill = layer.Dot(4, 8, 12, "AccentBrush");
        var tick = layer.Tick(6.5, 10.5, 7);
        var text = layer.Text(26, 6, title, 11, "TextPrimaryBrush", FontWeight.SemiBold);
        var strike = layer.Box(26, 13.5, 0, 1.2, "TextSecondaryBrush", 0.6);
        layer.Box(0, 27, width, 1, "HairlineBrush", 0);
        var remove = layer.Icon(width - 18, 8, 11, "IconClose", "TextTertiaryBrush");
        return new TaskRowParts(frame, layer, hover, ring, fill, tick, text, strike, remove, titleWidth);
    }

    private T Add<T>(T control, double x, double y) where T : Control
    {
        Canvas.SetLeft(control, x);
        Canvas.SetTop(control, y);
        Motion.Animate(control);
        Motion.Place(control, 0, 0);
        Canvas.Children.Add(control);
        return control;
    }

    private static Geometry? ResolveGeometry(string key)
        => Application.Current?.TryGetResource(key, null, out var value) == true ? value as Geometry : null;
}

/// <summary>任务行的可动部件。</summary>
internal sealed record TaskRowParts(
    Border Frame,
    SceneLayer Layer,
    Border Hover,
    Ellipse Ring,
    Ellipse Fill,
    Path Tick,
    TextBlock Title,
    Border Strike,
    Path Remove,
    double TitleWidth)
{
    /// <summary>回到未完成、未悬停、原位。</summary>
    public void Reset()
    {
        Motion.Hide(Fill);
        Motion.Hide(Tick);
        Motion.Hide(Hover);
        Motion.Hide(Remove);
        Motion.Show(Frame);
        Motion.Place(Frame, 0, 0);
        Title.Opacity = 1;
        Strike.Width = 0;
        Layer.Brushes.Set(Ring, Shape.StrokeProperty, "TextTertiaryBrush");
    }

    /// <summary>勾选完成：填充 + 勾 + 标题沉降 + 删除线展开（design-visual-language §5.2）。</summary>
    public void Complete()
    {
        Motion.Show(Fill);
        Motion.Show(Tick);
        Layer.Brushes.Set(Ring, Shape.StrokeProperty, "AccentBrush");
        Title.Opacity = 0.4;
        Strike.Width = TitleWidth;
    }
}

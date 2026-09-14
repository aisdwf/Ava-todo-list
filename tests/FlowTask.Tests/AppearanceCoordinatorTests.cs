using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using FlowTask.Desktop.Appearance;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖外观切换与主题令牌解析。
/// </summary>
/// <remarks>
/// 这些断言针对两个实际故障：
/// 1. 调整外观配置（强调色/材质）后画面毫无变化 —— 令牌曾嵌在 MergedDictionaries 内，
///    顶层 ThemeDictionaries 为空集合，运行时写入被静默丢弃；
/// 2. 切换昼夜模式直接闪退 —— <c>Animation</c> 对 <c>RenderTransform</c> 无内建 animator，
///    抛出的 <c>InvalidOperationException</c> 从 async 事件处理器逃逸到进程顶层。
///
/// 两者都能通过编译与当时的全部测试，仅在真实交互时暴露，故必须固化为回归防护。
/// </remarks>
public class AppearanceCoordinatorTests
{
    /// <summary>
    /// 强调色预设必须覆盖 spec-editorial-and-ripple-theme §2 要求的四种配色，且深浅色值各自独立。
    /// </summary>
    [AvaloniaFact]
    public void AccentPresets_ProvideFourDistinctOptions()
    {
        Assert.Equal(4, AppearanceCoordinator.AccentPresets.Count);
        Assert.Equal(
            AppearanceCoordinator.AccentPresets.Count,
            AppearanceCoordinator.AccentPresets.Select(p => p.Id).Distinct().Count());

        foreach (var preset in AppearanceCoordinator.AccentPresets)
        {
            Assert.NotEqual(preset.DarkHex, preset.LightHex);
            Assert.Equal(Color.Parse(preset.DarkHex), ((SolidColorBrush)preset.Swatch).Color);
        }
    }

    /// <summary>
    /// 材质预设必须携带不透明度语义，否则不透明的窗口背景会遮盖平台材质。
    /// </summary>
    [AvaloniaFact]
    public void MaterialPresets_CarryOpacitySemantics()
    {
        Assert.Equal(4, AppearanceCoordinator.MaterialPresets.Count);

        var solid = AppearanceCoordinator.FindMaterial("Solid");
        Assert.True(solid.IsOpaque);
        Assert.Equal(1.0, solid.SurfaceOpacity);

        // 透明档位必须真的半透明，否则材质切换在视觉上无差别
        foreach (var id in new[] { "Mica", "Acrylic", "Blur" })
        {
            var preset = AppearanceCoordinator.FindMaterial(id);
            Assert.False(preset.IsOpaque);
            Assert.InRange(preset.SurfaceOpacity, 0.2, 0.95);
        }

        // 候选链末位必须是 None，保证平台不支持首选材质时仍有可用底色
        foreach (var preset in AppearanceCoordinator.MaterialPresets)
        {
            Assert.NotEmpty(preset.Hint);
            Assert.Equal(WindowTransparencyLevel.None, preset.Hint[^1]);
        }
    }

    /// <summary>
    /// 透明档位之间的不透明度需有区分度，否则四个选项观感相同。
    /// </summary>
    [AvaloniaFact]
    public void TranslucentMaterials_HaveDistinctOpacity()
    {
        var values = new[] { "Mica", "Acrylic", "Blur" }
            .Select(id => AppearanceCoordinator.FindMaterial(id).SurfaceOpacity)
            .ToList();

        Assert.Equal(values.Count, values.Distinct().Count());
    }

    /// <summary>
    /// 未知预设标识必须回退而非抛出，避免持久化数据损坏导致启动失败。
    /// </summary>
    [AvaloniaFact]
    public void UnknownPresetId_FallsBackToFirstOption()
    {
        Assert.Equal(AppearanceCoordinator.AccentPresets[0].Id, AppearanceCoordinator.FindAccent("nope").Id);
        Assert.Equal(AppearanceCoordinator.MaterialPresets[0].Id, AppearanceCoordinator.FindMaterial("nope").Id);
    }

    /// <summary>
    /// 令牌必须挂载在顶层 ThemeDictionaries 上，这是运行时覆写能生效的前提。
    /// </summary>
    [AvaloniaFact]
    public void ThemeDictionaries_AreMountedAtTopLevel()
    {
        var resources = Application.Current!.Resources;

        Assert.True(resources.ThemeDictionaries.ContainsKey(ThemeVariant.Dark));
        Assert.True(resources.ThemeDictionaries.ContainsKey(ThemeVariant.Light));
    }

    /// <summary>
    /// 核心令牌在深浅主题下必须解析出不同值，证明变体隔离正确。
    /// </summary>
    [AvaloniaTheory]
    [InlineData("AccentBrush")]
    [InlineData("WindowSurfaceBrush")]
    [InlineData("CardSurfaceBrush")]
    [InlineData("TextPrimaryBrush")]
    [InlineData("HairlineBrush")]
    [InlineData("PriorityHighBrush")]
    public void CoreTokens_ResolveDifferentlyPerVariant(string key)
    {
        var resources = Application.Current!.Resources;

        Assert.True(resources.TryGetResource(key, ThemeVariant.Dark, out var dark));
        Assert.True(resources.TryGetResource(key, ThemeVariant.Light, out var light));

        var darkColor = Assert.IsType<SolidColorBrush>(dark).Color;
        var lightColor = Assert.IsType<SolidColorBrush>(light).Color;
        Assert.NotEqual(darkColor, lightColor);
    }

    /// <summary>
    /// 与主题无关的尺度令牌必须可解析，否则排版尺度全部退化为控件默认值。
    /// </summary>
    [AvaloniaTheory]
    [InlineData("FontSizeHeroTitle")]
    [InlineData("FontSizeTaskTitle")]
    [InlineData("FontSizeMicro")]
    [InlineData("TaskRowPadding")]
    [InlineData("ContentAreaMargin")]
    [InlineData("PillCornerRadius")]
    public void SharedScaleTokens_AreResolvable(string key)
    {
        Assert.True(Application.Current!.Resources.TryGetResource(key, ThemeVariant.Dark, out var value));
        Assert.NotNull(value);
    }

    /// <summary>
    /// 换强调色必须真正改写主题字典中的全部派生笔刷。
    /// </summary>
    [AvaloniaFact]
    public void ApplyAccent_MutatesAllDerivedBrushes()
    {
        var resources = Application.Current!.Resources;

        try
        {
            foreach (var preset in AppearanceCoordinator.AccentPresets)
            {
                AppearanceCoordinator.ApplyAccent(preset.Id);

                AssertBrushColor(resources, "AccentBrush", ThemeVariant.Dark, Color.Parse(preset.DarkHex));
                AssertBrushColor(resources, "AccentBrush", ThemeVariant.Light, Color.Parse(preset.LightHex));

                // 弱化笔刷需同色但保持半透明，否则选中态会变成实心色块
                var subtle = GetBrush(resources, "AccentSubtleBrush", ThemeVariant.Dark);
                Assert.Equal(Color.Parse(preset.DarkHex), subtle.Color);
                Assert.True(subtle.Opacity < 1.0);
            }
        }
        finally
        {
            AppearanceCoordinator.ApplyAccent(AppearanceCoordinator.AccentPresets[0].Id);
        }
    }

    /// <summary>
    /// 换强调色不得破坏未被覆写的其他令牌。
    /// </summary>
    /// <remarks>
    /// 覆写层若直接替换整个主题字典而未保留原内容，文本色、卡片底色等
    /// 令牌会一并丢失，界面将退化为不可读状态。
    /// </remarks>
    [AvaloniaFact]
    public void ApplyAccent_PreservesUnrelatedTokens()
    {
        var resources = Application.Current!.Resources;

        try
        {
            AppearanceCoordinator.ApplyAccent("Purple");

            foreach (var key in new[] { "TextPrimaryBrush", "CardSurfaceBrush", "HairlineBrush", "PriorityLowBrush" })
            {
                Assert.True(resources.TryGetResource(key, ThemeVariant.Dark, out var dark), $"{key} lost in Dark");
                Assert.True(resources.TryGetResource(key, ThemeVariant.Light, out var light), $"{key} lost in Light");
                Assert.NotNull(dark);
                Assert.NotNull(light);
            }
        }
        finally
        {
            AppearanceCoordinator.ApplyAccent(AppearanceCoordinator.AccentPresets[0].Id);
        }
    }

    /// <summary>
    /// 窗体底色必须按变体解析，水波纹遮罩与窗口背景都依赖它。
    /// </summary>
    [AvaloniaFact]
    public void ResolveWindowSurfaceColor_DiffersPerVariant()
    {
        var dark = AppearanceCoordinator.ResolveWindowSurfaceColor(true);
        var light = AppearanceCoordinator.ResolveWindowSurfaceColor(false);

        Assert.NotEqual(dark, light);
        // 深色底必须显著暗于浅色底，防止两套令牌被写反
        Assert.True(dark.R < light.R);
    }

    /// <summary>
    /// 应用材质必须同时设置透明度等级与半透明背景。
    /// </summary>
    [AvaloniaFact]
    public void ApplyMaterial_SetsHintAndTranslucentBackground()
    {
        var window = new Window { Width = 300, Height = 200 };

        try
        {
            AppearanceCoordinator.ApplyMaterial(window, "Acrylic");

            var acrylic = AppearanceCoordinator.FindMaterial("Acrylic");
            Assert.Equal(acrylic.Hint, window.TransparencyLevelHint);

            // 背景必须半透明，否则平台材质被完全遮盖
            var brush = Assert.IsType<SolidColorBrush>(window.Background);
            Assert.Equal(acrylic.SurfaceOpacity, brush.Opacity);

            AppearanceCoordinator.ApplyMaterial(window, "Solid");
            var solidBrush = Assert.IsType<SolidColorBrush>(window.Background);
            Assert.Equal(1.0, solidBrush.Opacity);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// 回归防护：水波纹所用的过渡配置不得抛异常。
    /// </summary>
    /// <remarks>
    /// 闪退根因是 <c>Animation.RunAsync</c> 对 <c>RenderTransform</c> 缺少 animator。
    /// 本测试断言改用 <see cref="TransformOperationsTransition"/> 后该属性可安全动画化。
    /// </remarks>
    [AvaloniaFact]
    public void RevealTransition_DoesNotThrowOnRenderTransform()
    {
        var circle = new Ellipse { Width = 100, Height = 100, Fill = Brushes.Red };
        var canvas = new Canvas { Children = { circle } };
        var window = new Window { Content = canvas, Width = 300, Height = 300 };
        window.Show();

        try
        {
            circle.RenderTransformOrigin = RelativePoint.Center;
            circle.RenderTransform = TransformOperations.Parse("scale(0)");
            circle.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(20),
                    Easing = new CubicEaseOut()
                },
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(20),
                    Easing = new CubicEaseOut()
                }
            };

            // 设置终值即触发过渡；旧实现在等价位置会抛 InvalidOperationException
            circle.RenderTransform = TransformOperations.Parse("scale(1)");
            circle.Opacity = 0;

            Assert.NotNull(circle.RenderTransform);
        }
        finally
        {
            circle.Transitions = null;
            window.Close();
        }
    }

    /// <summary>
    /// 锁定根因：以 Animation 关键帧插值 RenderTransform 必定失败。
    /// </summary>
    /// <remarks>
    /// 若后续重构改回 Animation 写法，此断言会失败并直接指出闪退风险。
    /// </remarks>
    [AvaloniaFact]
    public void AnimationOnRenderTransform_IsUnsupported()
    {
        var circle = new Ellipse { Width = 100, Height = 100 };
        var canvas = new Canvas { Children = { circle } };
        var window = new Window { Content = canvas, Width = 300, Height = 300 };
        window.Show();

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(20),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(Visual.RenderTransformProperty, TransformOperations.Parse("scale(0)")) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(Visual.RenderTransformProperty, TransformOperations.Parse("scale(1)")) }
                }
            }
        };

        try
        {
            // RunAsync 在建立 animator 阶段同步抛出，尚未进入异步流程
            Exception? error = null;
            try
            {
                _ = animation.RunAsync(circle);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Assert.IsType<InvalidOperationException>(error);
            Assert.Contains("RenderTransform", error!.Message, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    private static SolidColorBrush GetBrush(IResourceDictionary resources, string key, ThemeVariant variant)
    {
        Assert.True(resources.TryGetResource(key, variant, out var value), $"{key} not found for {variant}");
        return Assert.IsType<SolidColorBrush>(value);
    }

    private static void AssertBrushColor(IResourceDictionary resources, string key, ThemeVariant variant, Color expected)
        => Assert.Equal(expected, GetBrush(resources, key, variant).Color);
}

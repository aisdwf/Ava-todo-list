using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace FlowTask.Desktop.Appearance;

/// <summary>
/// 外观协调器：集中承载主题变体、强调色与窗口材质的运行时切换 (spec-editorial-and-ripple-theme Phase 4)。
/// </summary>
/// <remarks>
/// 为什么需要这一层：原实现把强调色写入 <c>Application.Current.Resources["AccentBrush"]</c>，
/// 但 AccentBrush 定义在 <c>ResourceDictionary.ThemeDictionaries</c> 内部。Avalonia 解析
/// DynamicResource 时主题字典优先级高于宿主顶层字典，顶层写入被主题值直接遮蔽，
/// 因此四个强调色按钮点击后毫无视觉变化。此处改为向两个主题字典同时写入
/// <c>AccentColor</c> 种子色，令牌文件中所有强调派生笔刷都链式引用该色，
/// 一次写入即完成级联（AI_CONSTITUTION Article 6 / Article 10）。
/// </remarks>
public static class AppearanceCoordinator
{
    /// <summary>强调色种子令牌键，与 Tokens.Dark/Light.axaml 中的声明保持一致。</summary>
    private const string AccentColorKey = "AccentColor";

    /// <summary>窗体底色令牌键。</summary>
    private const string WindowSurfaceColorKey = "WindowSurfaceColor";

    // 强调色派生笔刷的不透明度，与 Tokens.Dark/Light.axaml 中的初始声明保持一致
    private const double SubtleOpacityDark = 0.18;
    private const double GlowOpacityDark = 0.34;
    private const double SubtleOpacityLight = 0.14;
    private const double GlowOpacityLight = 0.28;

    /// <summary>
    /// 可选强调色预设。这是强调色的权威定义，UI 与命令层均从此处派生，
    /// 禁止在视图或 ViewModel 中另建色值查找表。
    /// </summary>
    public static readonly IReadOnlyList<AppearanceOption> AccentPresets = new[]
    {
        new AppearanceOption("Blue", "科技蓝", "#4CA0FF", "#0067C0"),
        new AppearanceOption("Purple", "极光紫", "#A78BFA", "#6D28D9"),
        new AppearanceOption("Green", "翡翠绿", "#34D399", "#0E7C5A"),
        new AppearanceOption("Orange", "日落橙", "#FB923C", "#C2410C")
    };

    /// <summary>
    /// 可选窗口材质预设。<c>Hint</c> 为 Avalonia 透明度等级候选链，
    /// 首选项不被平台支持时按序回退，末位 None 保证始终有可用的不透明底。
    /// </summary>
    public static readonly IReadOnlyList<MaterialOption> MaterialPresets = new[]
    {
        new MaterialOption("Mica", "Mica 云母", "Windows 11 经典",
            new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None },
            SurfaceOpacity: 0.80),
        new MaterialOption("Acrylic", "Acrylic 亚克力", "半透明磨砂质感",
            new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None },
            SurfaceOpacity: 0.62),
        new MaterialOption("Blur", "Blur 极光模糊", "柔和漫反射环境",
            new[] { WindowTransparencyLevel.Blur, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None },
            SurfaceOpacity: 0.46),
        new MaterialOption("Solid", "Solid 纯色", "杂志感无杂质纯色",
            new[] { WindowTransparencyLevel.None },
            SurfaceOpacity: 1.0)
    };

    /// <summary>
    /// 应用主题变体（深色 / 浅色）。
    /// </summary>
    public static void ApplyTheme(bool isDark)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    /// <summary>
    /// 应用强调色预设，深浅两套主题字典同时更新，避免切换主题后强调色回退。
    /// </summary>
    /// <param name="presetId">预设标识，未知值回退至首个预设。</param>
    /// <remarks>
    /// 派生笔刷需逐个覆写而非依赖 <c>DynamicResource</c> 链式引用种子色：
    /// 主题字典内的 <c>DynamicResource</c> 引用不会限定在本变体内解析，
    /// 实测浅色主题会取到深色字典的值（AccentBrush 在 Light 下返回 #4CA0FF）。
    /// 因此令牌文件使用字面色值，运行时换色在此处集中覆写全部派生笔刷。
    /// </remarks>
    public static void ApplyAccent(string presetId)
    {
        var preset = FindAccent(presetId);

        ApplyAccentToVariant(ThemeVariant.Dark, Color.Parse(preset.DarkHex), SubtleOpacityDark, GlowOpacityDark);
        ApplyAccentToVariant(ThemeVariant.Light, Color.Parse(preset.LightHex), SubtleOpacityLight, GlowOpacityLight);
    }

    private static void ApplyAccentToVariant(ThemeVariant variant, Color accent, double subtle, double glow)
    {
        WriteThemeResource(variant, AccentColorKey, accent);
        WriteThemeResource(variant, "AccentBrush", new SolidColorBrush(accent));
        WriteThemeResource(variant, "AccentSubtleBrush", new SolidColorBrush(accent, subtle));
        WriteThemeResource(variant, "AccentGlowBrush", new SolidColorBrush(accent, glow));
        WriteThemeResource(variant, "TextControlSelectionHighlightColor", new SolidColorBrush(accent));
    }

    /// <summary>
    /// 应用窗口材质预设。
    /// </summary>
    /// <param name="window">目标窗口，通常为主窗口。</param>
    /// <param name="presetId">预设标识，未知值回退至首个预设。</param>
    /// <remarks>
    /// 必须同时调整窗口背景：<see cref="Window.TransparencyLevelHint"/> 只声明期望的
    /// 透明材质等级，若窗口 Background 仍是不透明笔刷，材质会被完全遮盖，
    /// 表现为切换材质后画面毫无变化。透明材质档位下改用半透明底，
    /// 让平台合成的模糊效果透出；纯色档位维持不透明底。
    /// </remarks>
    public static void ApplyMaterial(Window window, string presetId)
    {
        ArgumentNullException.ThrowIfNull(window);

        var preset = FindMaterial(presetId);
        window.TransparencyLevelHint = preset.Hint;
        window.Background = BuildWindowBackground(window, preset);
    }

    /// <summary>
    /// 依据当前主题与材质档位构建窗口背景笔刷。
    /// </summary>
    private static IBrush BuildWindowBackground(Window window, MaterialOption preset)
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var color = ResolveWindowSurfaceColor(isDark);

        if (preset.IsOpaque)
        {
            return new SolidColorBrush(color);
        }

        // 透明档位：保留足够不透明度维持文本对比度，同时让底层材质可见
        return new SolidColorBrush(color, preset.SurfaceOpacity);
    }

    /// <summary>
    /// 重新应用当前材质的背景笔刷，供主题切换后刷新底色使用。
    /// </summary>
    /// <remarks>
    /// 背景是代码设置的具体笔刷实例，不参与主题字典的重新求值，
    /// 因此昼夜切换后必须显式重建，否则浅色主题会保留深色底。
    /// </remarks>
    public static void RefreshMaterialBackground(Window window, string presetId)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Background = BuildWindowBackground(window, FindMaterial(presetId));
    }

    /// <summary>
    /// 查询指定材质预设，未知标识回退至首个预设。
    /// </summary>
    public static MaterialOption FindMaterial(string presetId)
        => MaterialPresets.FirstOrDefault(p => p.Id == presetId) ?? MaterialPresets[0];

    /// <summary>
    /// 查询指定强调色预设，供动画等需要提前获知目标色的场景使用。
    /// </summary>
    public static AppearanceOption FindAccent(string presetId)
        => AccentPresets.FirstOrDefault(p => p.Id == presetId) ?? AccentPresets[0];

    /// <summary>
    /// 读取当前生效主题下的窗体底色，供水波纹转场取得准确的目标色。
    /// </summary>
    /// <remarks>
    /// 为什么从资源读取而非硬编码：原动画实现把 <c>#202020</c> / <c>#F9F9FB</c> 直接写在
    /// 代码里，与令牌文件形成第二份色值副本，改令牌时动画色会静默失配（Article 6）。
    /// </remarks>
    public static Color ResolveWindowSurfaceColor(bool isDark)
    {
        var variant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (Application.Current?.Resources.TryGetResource(WindowSurfaceColorKey, variant, out var value) == true
            && value is Color color)
        {
            return color;
        }

        // 令牌缺失时的保守回退，仅用于避免动画与背景构建抛异常
        return isDark ? Color.Parse("#202022") : Color.Parse("#F7F7F5");
    }

    /// <summary>
    /// 向指定主题字典写入令牌值。
    /// </summary>
    /// <remarks>
    /// 此前的实现要求主题字典必须已是 <see cref="ResourceDictionary"/> 实例，否则静默跳过。
    /// 而令牌一度嵌在 MergedDictionaries 内，顶层 ThemeDictionaries 为空集合，
    /// 换色请求全部无声丢弃 —— 这是"调整外观配置无任何变化"的直接原因。
    /// 此处对非 ResourceDictionary 的挂载形式（如 ResourceInclude）建立覆写层，
    /// 保证写入始终落地。
    /// </remarks>
    private static void WriteThemeResource(ThemeVariant variant, string key, object value)
    {
        var appResources = Application.Current?.Resources;
        if (appResources is null)
        {
            return;
        }

        if (!appResources.ThemeDictionaries.TryGetValue(variant, out var dictionary)
            || dictionary is not ResourceDictionary resources)
        {
            // ResourceInclude 等形式无法直接写入，改为在其上叠加一层可写字典。
            // 覆写层需保留原始内容，否则会丢失未被覆写的令牌。
            var overlay = new ResourceDictionary();
            if (dictionary is not null)
            {
                overlay.MergedDictionaries.Add(dictionary);
            }

            appResources.ThemeDictionaries[variant] = overlay;
            resources = overlay;
        }

        resources[key] = value;
    }
}

/// <summary>
/// 强调色预设定义，深浅主题各持一份色值以保证两种底色下的对比度。
/// </summary>
/// <param name="Id">稳定标识，用于持久化与命令参数。</param>
/// <param name="DisplayName">界面展示名称。</param>
/// <param name="DarkHex">深色主题下的色值。</param>
/// <param name="LightHex">浅色主题下的色值。</param>
public sealed record AppearanceOption(string Id, string DisplayName, string DarkHex, string LightHex)
{
    private IBrush? _swatch;

    /// <summary>
    /// 设置面板中的色样笔刷，由深色值派生，避免视图再次解析十六进制字符串。
    /// </summary>
    /// <remarks>
    /// 必须惰性创建：<see cref="SolidColorBrush"/> 继承 <c>Animatable</c>，
    /// 其构造函数会校验调用线程。若在静态字段初始化时创建，
    /// 任何从非 UI 线程首次触达 <see cref="AppearanceCoordinator"/> 的代码路径
    /// 都会因类型初始化器抛 "Call from invalid thread" 而失败。
    /// </remarks>
    public IBrush Swatch => _swatch ??= new SolidColorBrush(Color.Parse(DarkHex));
}

/// <summary>
/// 窗口材质预设定义。
/// </summary>
/// <param name="Id">稳定标识，用于持久化与命令参数。</param>
/// <param name="DisplayName">界面展示名称。</param>
/// <param name="Description">辅助说明文案。</param>
/// <param name="Hint">按优先级排序的透明度等级候选链。</param>
/// <param name="SurfaceOpacity">
/// 窗体底色不透明度。低于 1 时平台材质可透出；取值需保证文本对比度仍然充足。
/// </param>
public sealed record MaterialOption(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<WindowTransparencyLevel> Hint,
    double SurfaceOpacity)
{
    /// <summary>该档位是否为完全不透明的纯色底。</summary>
    public bool IsOpaque => SurfaceOpacity >= 1.0;
}

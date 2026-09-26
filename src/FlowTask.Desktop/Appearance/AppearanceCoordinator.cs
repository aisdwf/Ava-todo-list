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

    /// <summary>应用字体令牌键，定义在 Tokens.Shared.axaml。</summary>
    private const string AppFontFamilyKey = "AppFontFamily";

    /// <summary>站点默认无衬线栈，与 Tokens.Shared 的初始值一致。</summary>
    private const string SansFontFamily =
        "Inter, Segoe UI Variable, Segoe UI, PingFang SC, Microsoft YaHei, sans-serif";

    /// <summary>
    /// Anthropic 的代表字体。拉丁文用 Lora，中文落到思源宋体 / 宋体。
    /// 与 dogapi / linkapi 的 <c>--font-serif</c> 同一条回退链。
    /// </summary>
    private const string AnthropicFontFamily =
        "Lora, Source Serif 4, Noto Serif SC, Source Han Serif SC, Songti SC, SimSun, Georgia, Times New Roman, serif";

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
    /// 命名主题预设。色值来自 dogapi.cc 与 linkapi.ai 同一套 New API 主题样式表
    /// （构建标识 2k6e8r7p），不是由强调色列表派生。
    /// </summary>
    /// <remarks>
    /// 除 Anthropic 外，各预设只覆写强调色，表面、文本、边框与状态色继承站点默认浅色/深色。
    /// 浅色样式表没有单独的 <c>--sidebar</c> 十六进制值，侧栏底用已采集的 <c>--muted</c>（#F5F5F5）。
    /// 「超大字体简易」未收入：该预设的差异是字号，而本应用不改排版尺度。
    /// </remarks>
    public static readonly IReadOnlyList<ThemePreset> ThemePresets = BuildReferenceThemePresets();

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
    /// 查询指定命名主题预设，未知标识回退至首个预设。
    /// </summary>
    public static ThemePreset FindThemePreset(string presetId)
        => ThemePresets.FirstOrDefault(p => p.Id == presetId) ?? ThemePresets[0];

    /// <summary>
    /// 应用命名主题预设：把该预设的表面、文本、边框、状态色和强调色写入两套主题字典。
    /// </summary>
    /// <remarks>
    /// 不能转调 <see cref="ApplyAccent"/>。主题强调色不在 <see cref="AccentPresets"/> 里，
    /// 按 Id 查找会回退成科技蓝，表面色也不会被写上。
    /// </remarks>
    public static void ApplyThemePreset(string presetId)
    {
        var preset = FindThemePreset(presetId);
        ApplyPalette(ThemeVariant.Dark, preset.Dark, SubtleOpacityDark, GlowOpacityDark);
        ApplyPalette(ThemeVariant.Light, preset.Light, SubtleOpacityLight, GlowOpacityLight);
        ApplySidebarWash(preset);
        ApplyFont(preset.Id);
    }

    /// <summary>
    /// 默认主题保持原来的淡侧栏。其余主题让侧栏底色朝强调色偏一点，
    /// Anthropic 直接用站点采集的侧栏色。
    /// </summary>
    private static void ApplySidebarWash(ThemePreset preset)
    {
        if (preset.Id == "default")
        {
            WriteThemeResource(ThemeVariant.Light, "SidebarWashBrush", new SolidColorBrush(Color.Parse(preset.Light.HairlineHex)));
            WriteThemeResource(ThemeVariant.Dark, "SidebarWashBrush", new SolidColorBrush(Color.Parse(preset.Dark.HairlineHex)));
            return;
        }

        if (preset.Id == "anthropic")
        {
            WriteThemeResource(ThemeVariant.Light, "SidebarWashBrush", new SolidColorBrush(Color.Parse(preset.Light.SidebarHex)));
            WriteThemeResource(ThemeVariant.Dark, "SidebarWashBrush", new SolidColorBrush(Color.Parse(preset.Dark.SidebarHex)));
            return;
        }

        // 浅色多掺一点才看得出，深色底本身很暗，掺多了会变成一块纯强调色。
        WriteThemeResource(ThemeVariant.Light, "SidebarWashBrush", new SolidColorBrush(MixToward(Color.Parse("#F5F5F5"), Color.Parse(preset.Light.AccentHex), 0.22)));
        WriteThemeResource(ThemeVariant.Dark, "SidebarWashBrush", new SolidColorBrush(MixToward(Color.Parse("#1C1C1C"), Color.Parse(preset.Dark.AccentHex), 0.34)));
    }

    private static Color MixToward(Color basis, Color accent, double amount)
    {
        byte Channel(byte from, byte to) => (byte)Math.Round(from + (to - from) * amount);
        return Color.FromRgb(Channel(basis.R, accent.R), Channel(basis.G, accent.G), Channel(basis.B, accent.B));
    }

    /// <summary>
    /// 只有 Anthropic 在参考站点里绑定衬线字体，其余预设保持无衬线。
    /// </summary>
    private static void ApplyFont(string presetId)
    {
        if (Application.Current is null)
        {
            return;
        }

        var family = presetId == "anthropic" ? AnthropicFontFamily : SansFontFamily;
        Application.Current.Resources[AppFontFamilyKey] = new FontFamily(family);
    }

    private static void ApplyPalette(ThemeVariant variant, ThemeVariantPalette palette, double subtle, double glow)
    {
        var accent = Color.Parse(palette.AccentHex);
        var window = Color.Parse(palette.WindowHex);
        var text = Color.Parse(palette.TextPrimaryHex);
        var mutedText = Color.Parse(palette.TextSecondaryHex);
        var high = Color.Parse(palette.PriorityHighHex);
        var medium = Color.Parse(palette.PriorityMediumHex);
        var low = Color.Parse(palette.PriorityLowHex);

        WriteThemeResource(variant, WindowSurfaceColorKey, window);
        WriteThemeResource(variant, "WindowSurfaceBrush", new SolidColorBrush(window));
        WriteThemeResource(variant, "SidebarSurfaceBrush", new SolidColorBrush(Color.Parse(palette.SidebarHex)));
        WriteThemeResource(variant, "CardSurfaceBrush", new SolidColorBrush(Color.Parse(palette.CardHex)));
        WriteThemeResource(variant, "CardSurfaceHoverBrush", new SolidColorBrush(Color.Parse(palette.CardHoverHex)));
        WriteThemeResource(variant, "HairlineBrush", new SolidColorBrush(Color.Parse(palette.HairlineHex)));
        WriteThemeResource(variant, "HairlineStrongBrush", new SolidColorBrush(Color.Parse(palette.HairlineHex)));
        WriteThemeResource(variant, "TextPrimaryBrush", new SolidColorBrush(text));
        WriteThemeResource(variant, "TextSecondaryBrush", new SolidColorBrush(mutedText));
        WriteThemeResource(variant, "TextTertiaryBrush", new SolidColorBrush(mutedText));
        WriteThemeResource(variant, "TextDisabledBrush", new SolidColorBrush(mutedText));
        WriteThemeResource(variant, "OnAccentBrush", new SolidColorBrush(Color.Parse(palette.OnAccentHex)));
        WriteThemeResource(variant, "PriorityHighBrush", new SolidColorBrush(high));
        WriteThemeResource(variant, "PriorityMediumBrush", new SolidColorBrush(medium));
        WriteThemeResource(variant, "PriorityLowBrush", new SolidColorBrush(low));
        WriteThemeResource(variant, "PriorityHighSurfaceBrush", new SolidColorBrush(high, subtle));
        WriteThemeResource(variant, "PriorityMediumSurfaceBrush", new SolidColorBrush(medium, subtle));
        WriteThemeResource(variant, "PriorityLowSurfaceBrush", new SolidColorBrush(low, subtle));
        WriteThemeResource(variant, "TextControlForeground", new SolidColorBrush(text));
        WriteThemeResource(variant, "TextControlForegroundPointerOver", new SolidColorBrush(text));
        WriteThemeResource(variant, "TextControlForegroundFocused", new SolidColorBrush(text));
        WriteThemeResource(variant, "TextControlPlaceholderForeground", new SolidColorBrush(mutedText));
        WriteThemeResource(variant, "TextControlPlaceholderForegroundPointerOver", new SolidColorBrush(mutedText));
        WriteThemeResource(variant, "TextControlPlaceholderForegroundFocused", new SolidColorBrush(mutedText));

        WriteThemeResource(variant, AccentColorKey, accent);
        WriteThemeResource(variant, "AccentBrush", new SolidColorBrush(accent));
        WriteThemeResource(variant, "AccentSubtleBrush", new SolidColorBrush(accent, subtle));
        WriteThemeResource(variant, "AccentGlowBrush", new SolidColorBrush(accent, glow));
        WriteThemeResource(variant, "TextControlSelectionHighlightColor", new SolidColorBrush(accent));
    }

    /// <summary>
    /// 站点默认浅色/深色是其余预设的表面底。只改强调色的预设在此底上替换主色。
    /// </summary>
    private static IReadOnlyList<ThemePreset> BuildReferenceThemePresets()
    {
        var light = new ThemeVariantPalette(
            "#FFFFFF", "#F5F5F5", "#FFFFFF", "#F5F5F5", "#E8E8E8",
            "#0A0A0A", "#606060", "#3EA4EC", "#FFFFFF",
            "#E40014", "#CD8900", "#009767");
        var dark = new ThemeVariantPalette(
            "#1E1E1E", "#1C1C1C", "#2A2A2A", "#2F2F2F", "#1AFFFFFF",
            "#F3F3F3", "#B7B7B7", "#0E72BC", "#FFFFFF",
            "#FF6568", "#F99C00", "#00BB7F");

        return new[]
        {
            Preset("default", "默认", light, dark),
            Preset("anthropic", "Anthropic",
                new ThemeVariantPalette(
                    "#FAFAF7", "#F2F0EA", "#EFEDE7", "#EDEBE6", "#DEDCD6",
                    "#191715", "#686662", "#E37756", "#FDFCF8",
                    "#C53732", "#ECA851", "#6D8752"),
                new ThemeVariantPalette(
                    "#1B1918", "#191715", "#242221", "#2C2A28", "#1AFFFFFF",
                    "#F4F3F0", "#B3B1AD", "#EB8561", "#13110F",
                    "#FF6367", "#ECA851", "#82AD6A")),
            AccentPreset("underground", "暗夜", light, dark, "#49785B", "#FFFFFF", "#58946D", "#030713"),
            AccentPreset("rose-garden", "玫瑰花园", light, dark, "#E30054", "#FFFFFF", "#FB2F6C", "#FFFFFF"),
            AccentPreset("lake-view", "湖光", light, dark, "#00D294", "#000000", "#00D294", "#000000"),
            AccentPreset("sunset-glow", "日落霞光", light, dark, "#CB3435", "#FFFFFF", "#E55354", "#FFFFFF"),
            AccentPreset("forest-whisper", "森林低语", light, dark, "#007D70", "#FFFFFF", "#009683", "#FFFFFF"),
            AccentPreset("ocean-breeze", "海风", light, dark, "#2563EB", "#FFFFFF", "#3B82F6", "#FFFFFF"),
            AccentPreset("lavender-dream", "薰衣草梦", light, dark, "#9453C9", "#FFFFFF", "#A76AD9", "#FFFFFF")
        };
    }

    private static ThemePreset Preset(string id, string name, ThemeVariantPalette light, ThemeVariantPalette dark)
    {
        var accent = new AppearanceOption(id, name, dark.AccentHex, light.AccentHex);
        return new ThemePreset(id, name, accent, new ThemeSurfaceOverride(dark.WindowHex, light.WindowHex), dark, light);
    }

    private static ThemePreset AccentPreset(
        string id,
        string name,
        ThemeVariantPalette lightBasis,
        ThemeVariantPalette darkBasis,
        string lightAccent,
        string lightOnAccent,
        string darkAccent,
        string darkOnAccent)
        => Preset(
            id,
            name,
            lightBasis with { AccentHex = lightAccent, OnAccentHex = lightOnAccent },
            darkBasis with { AccentHex = darkAccent, OnAccentHex = darkOnAccent });

    /// <summary>
    /// 按已有条目数循环取调色板中的下一默认色（DarkHex）。
    /// </summary>
    /// <remarks>
    /// 权威定义集中于此，避免项目/标签创建路径各自取色而漂移（TR-1 / Article 6）。
    /// </remarks>
    public static string PickPaletteColor(int existingCount)
    {
        var palette = AccentPresets;
        var index = existingCount % palette.Count;
        if (index < 0)
        {
            index += palette.Count;
        }

        return palette[index].DarkHex;
    }

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
/// 命名主题预设定义（spec-settings-master-detail-and-theme-presets）。
/// </summary>
/// <param name="Id">稳定标识，用于持久化与命令参数；与内嵌 <see cref="Accent"/> 的 Id 一致。</param>
/// <param name="DisplayName">界面展示名称，例如 "Anthropic"、"暗夜"、"海风"。</param>
/// <param name="Accent">该主题的强调色。不加入 <see cref="AppearanceCoordinator.AccentPresets"/>，避免和「强调色」里的四色混成一份列表。</param>
/// <param name="SurfaceOverride">窗体底色。与 <see cref="Dark"/> / <see cref="Light"/> 的窗体色相同，供水波纹与材质读取。</param>
/// <param name="Dark">深色变体的完整色板。</param>
/// <param name="Light">浅色变体的完整色板。</param>
public sealed record ThemePreset(
    string Id,
    string DisplayName,
    AppearanceOption Accent,
    ThemeSurfaceOverride? SurfaceOverride,
    ThemeVariantPalette Dark,
    ThemeVariantPalette Light)
{
    /// <summary>设置面板中的色样笔刷，直接复用强调色的色样。</summary>
    public IBrush Swatch => Accent.Swatch;

    private IBrush? _preview;

    /// <summary>
    /// 色条：卡片底过渡到强调色，对应设置页扁圆角色块，而不是三段预览卡。
    /// </summary>
    public IBrush Preview => _preview ??= new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse(Light.CardHex), 0),
            new GradientStop(Color.Parse(Light.AccentHex), 1)
        }
    };
}

/// <summary>
/// 主题预设可选的表面基调覆写。
/// </summary>
/// <param name="DarkSurfaceHex">深色主题下的窗体底色覆写。</param>
/// <param name="LightSurfaceHex">浅色主题下的窗体底色覆写。</param>
public sealed record ThemeSurfaceOverride(string DarkSurfaceHex, string LightSurfaceHex);

/// <summary>
/// 一套主题变体里会写进令牌字典的颜色。十六进制使用 Avalonia 顺序（含透明通道时为 #AARRGGBB）。
/// </summary>
/// <param name="WindowHex">窗体底，对应站点 <c>--background</c>。</param>
/// <param name="SidebarHex">侧栏底，对应 <c>--sidebar</c>；浅色默认用 <c>--muted</c>。</param>
/// <param name="CardHex">卡片底，对应 <c>--card</c>。</param>
/// <param name="CardHoverHex">卡片悬停底，对应 <c>--muted</c>。</param>
/// <param name="HairlineHex">分隔线，对应 <c>--border</c>。</param>
/// <param name="TextPrimaryHex">主文本，对应 <c>--foreground</c>。</param>
/// <param name="TextSecondaryHex">次级文本，对应 <c>--muted-foreground</c>。</param>
/// <param name="AccentHex">强调色，对应 <c>--primary</c>。</param>
/// <param name="OnAccentHex">强调色上的文字，对应 <c>--primary-foreground</c>。</param>
/// <param name="PriorityHighHex">高优先级，对应 <c>--destructive</c>。</param>
/// <param name="PriorityMediumHex">中优先级，对应 <c>--warning</c>。</param>
/// <param name="PriorityLowHex">低优先级，对应 <c>--success</c>。</param>
public sealed record ThemeVariantPalette(
    string WindowHex,
    string SidebarHex,
    string CardHex,
    string CardHoverHex,
    string HairlineHex,
    string TextPrimaryHex,
    string TextSecondaryHex,
    string AccentHex,
    string OnAccentHex,
    string PriorityHighHex,
    string PriorityMediumHex,
    string PriorityLowHex);

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
    private IBrush? _swatch;

    /// <summary>该档位是否为完全不透明的纯色底。</summary>
    public bool IsOpaque => SurfaceOpacity >= 1.0;

    /// <summary>
    /// 设置页色条：用渐变模拟雾面 / 更透 / 更糊 / 实地，不另做窗口缩略图。
    /// </summary>
    public IBrush Swatch => _swatch ??= CreateSwatch();

    private IBrush CreateSwatch() => Id switch
    {
        "Mica" => Gradient("#EDEBE6", "#C8C4BB"),
        "Acrylic" => Gradient("#F3F6F8", "#9BB4C4"),
        "Blur" => Gradient("#DCE8F2", "#7A90A4"),
        _ => new SolidColorBrush(Color.Parse("#2A2A2C"))
    };

    private static IBrush Gradient(string fromHex, string toHex) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse(fromHex), 0),
            new GradientStop(Color.Parse(toHex), 1)
        }
    };
}

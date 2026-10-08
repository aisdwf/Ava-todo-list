using Avalonia.Input;

namespace FlowTask.Desktop.Services;

/// <summary>快捷小窗快捷键的修饰键。与 Win32 <c>MOD_*</c> 位不同值，换算只在 <see cref="QuickWindowHotkey"/> 内做。</summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>无修饰键。</summary>
    None = 0,

    /// <summary>Ctrl。</summary>
    Ctrl = 1,

    /// <summary>Alt（macOS 为 Option）。</summary>
    Alt = 2,

    /// <summary>Shift。只能与 Ctrl / Alt / Win 搭配，单独搭配会吞掉正常输入。</summary>
    Shift = 4,

    /// <summary>Windows 徽标键（Avalonia 的 <see cref="KeyModifiers.Meta"/>）。</summary>
    Win = 8
}

/// <summary>录制或读库得到的组合能否作为快捷小窗快捷键。</summary>
public enum HotkeyValidation
{
    /// <summary>可用。</summary>
    Valid,

    /// <summary>没有 Ctrl / Alt / Win。</summary>
    NeedsModifier,

    /// <summary>主键不在字母、数字、F1–F12、空格之内。</summary>
    UnsupportedKey,

    /// <summary>与系统常用键或小窗自身键位冲突（owner 裁决 Q3「拒绝」）。</summary>
    Reserved
}

/// <summary>
/// 快捷小窗快捷键（spec-quick-window-custom-hotkey）。
/// </summary>
/// <remarks>
/// 解析、存值、显示文案、Win32 修饰位与虚拟键都只在这里定义（Article 6）：
/// 此前 <c>GlobalHotkeyService</c> 写死 Alt+Space 与 Win+Alt+Space 两个常量，界面文案另写一份，
/// 静默回退时键帽和指南就会教错键（a5bf5d5 修过一次文案，根因是组合没有唯一来源）。
/// 主键直接使用 Avalonia 的 <see cref="Key"/>：录制来自窗口 KeyDown，不再造第二张键表。
/// </remarks>
/// <param name="Modifiers">修饰键。</param>
/// <param name="Key">主键。</param>
public readonly record struct QuickWindowHotkey(HotkeyModifiers Modifiers, Key Key)
{
    /// <summary>AppSettings 键。缺键或坏值时用 <see cref="Default"/>。</summary>
    public const string SettingsKey = "QuickWindow.Hotkey";

    /// <summary>macOS 固定注册 Option+Space，暂不支持自定义（TODO(macos-custom-hotkey)）。</summary>
    public const string MacLabel = "⌥ Space";

    private const HotkeyModifiers RequiredModifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win;

    /// <summary>默认组合 Alt+Space，「恢复默认」的目标（owner 裁决 Q2）。</summary>
    public static QuickWindowHotkey Default { get; } = new(HotkeyModifiers.Alt, Key.Space);

    /// <summary>
    /// 录制时拒绝的组合（owner 裁决 Q3）。
    /// </summary>
    /// <remarks>
    /// <c>RegisterHotKey</c> 不拦这些组合：注册 Ctrl+C 之后，所有程序的复制都会被 FlowTask 吃掉；
    /// Ctrl+D、Ctrl+Tab、Ctrl+Shift+Tab 是小窗自己的键位，注册后小窗内按它们只会收起小窗。
    /// </remarks>
    public static IReadOnlyList<QuickWindowHotkey> Reserved { get; } = new QuickWindowHotkey[]
    {
        new(HotkeyModifiers.Ctrl, Key.C),
        new(HotkeyModifiers.Ctrl, Key.V),
        new(HotkeyModifiers.Ctrl, Key.X),
        new(HotkeyModifiers.Ctrl, Key.Z),
        new(HotkeyModifiers.Ctrl, Key.Y),
        new(HotkeyModifiers.Ctrl, Key.A),
        new(HotkeyModifiers.Ctrl, Key.S),
        new(HotkeyModifiers.Ctrl, Key.D),
        new(HotkeyModifiers.Ctrl, Key.Tab),
        new(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, Key.Tab),
        new(HotkeyModifiers.Alt, Key.Tab),
        new(HotkeyModifiers.Alt, Key.F4)
    };

    /// <summary>校验组合。保留组合先于主键范围判断，Ctrl+Tab 这类才会给出「冲突」而不是「不支持」。</summary>
    public HotkeyValidation Validate()
    {
        if ((Modifiers & RequiredModifiers) == HotkeyModifiers.None)
        {
            return HotkeyValidation.NeedsModifier;
        }

        if (Reserved.Contains(this))
        {
            return HotkeyValidation.Reserved;
        }

        return IsSupportedKey(Key) ? HotkeyValidation.Valid : HotkeyValidation.UnsupportedKey;
    }

    /// <summary>校验不通过时给用户看的原因；可用时返回 null。</summary>
    public string? DescribeRejection()
        => Validate() switch
        {
            HotkeyValidation.NeedsModifier => "至少要带 Ctrl、Alt 或 Win 中的一个。",
            HotkeyValidation.UnsupportedKey => "主键只支持字母、数字、F1–F12 和空格。",
            HotkeyValidation.Reserved => $"{this} 是常用的系统或小窗快捷键，请换一个组合。",
            _ => null
        };

    /// <summary>
    /// 把窗口 KeyDown 变成候选组合。只按下修饰键时返回 null，录制继续等主键。
    /// </summary>
    public static QuickWindowHotkey? FromKeyEvent(KeyModifiers modifiers, Key key)
    {
        if (IsModifierKey(key))
        {
            return null;
        }

        var mapped = HotkeyModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            mapped |= HotkeyModifiers.Ctrl;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            mapped |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            mapped |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            mapped |= HotkeyModifiers.Win;
        }

        return new QuickWindowHotkey(mapped, key);
    }

    /// <summary>
    /// 解析存值。只接受能通过 <see cref="Validate"/> 的组合：库里的坏值或被手改成保留组合时，
    /// 宁可退回默认，也不注册一个会抢走复制粘贴的键。
    /// </summary>
    public static bool TryParse(string? raw, out QuickWindowHotkey hotkey)
    {
        hotkey = Default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var tokens = raw.Split('+', StringSplitOptions.TrimEntries);
        if (tokens.Length < 2 || tokens.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var token in tokens[..^1])
        {
            var modifier = ParseModifier(token);
            if (modifier == HotkeyModifiers.None)
            {
                return false;
            }

            modifiers |= modifier;
        }

        if (!TryParseKey(tokens[^1], out var key))
        {
            return false;
        }

        var parsed = new QuickWindowHotkey(modifiers, key);
        if (parsed.Validate() != HotkeyValidation.Valid)
        {
            return false;
        }

        hotkey = parsed;
        return true;
    }

    /// <summary>规范文案与存值同形：<c>Ctrl+Alt+Shift+Win+K</c>，修饰键顺序固定。</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(Key));
        return string.Join("+", parts);
    }

    /// <summary>写入 AppSettings 的值。</summary>
    public string ToStorage() => ToString();

    /// <summary>Win32 <c>RegisterHotKey</c> 的 <c>fsModifiers</c>，不含 <c>MOD_NOREPEAT</c>。</summary>
    public uint Win32Modifiers
    {
        get
        {
            uint value = 0;
            if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            {
                value |= 0x0001; // MOD_ALT
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Ctrl))
            {
                value |= 0x0002; // MOD_CONTROL
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            {
                value |= 0x0004; // MOD_SHIFT
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Win))
            {
                value |= 0x0008; // MOD_WIN
            }

            return value;
        }
    }

    /// <summary>Win32 虚拟键码。只对 <see cref="IsSupportedKey"/> 范围内的主键有意义。</summary>
    public uint Win32VirtualKey => Key switch
    {
        Key.Space => 0x20,
        >= Key.D0 and <= Key.D9 => 0x30u + (uint)(Key - Key.D0),
        >= Key.A and <= Key.Z => 0x41u + (uint)(Key - Key.A),
        >= Key.F1 and <= Key.F12 => 0x70u + (uint)(Key - Key.F1),
        _ => throw new InvalidOperationException($"Key {Key} has no supported virtual-key mapping.")
    };

    /// <summary>
    /// 窗内 KeyDown 是否恰好是本组合。修饰键要求完全一致：
    /// macOS 的 Command 映射为 <see cref="KeyModifiers.Meta"/>，不能被当成 Option(Alt)。
    /// </summary>
    public bool Matches(KeyModifiers modifiers, Key key) => FromKeyEvent(modifiers, key) == this;

    /// <summary>主键是否在支持范围内。</summary>
    public static bool IsSupportedKey(Key key)
        => key is Key.Space
            or >= Key.D0 and <= Key.D9
            or >= Key.A and <= Key.Z
            or >= Key.F1 and <= Key.F12;

    /// <summary>是否为修饰键本身。</summary>
    public static bool IsModifierKey(Key key)
        => key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;

    private static string KeyName(Key key) => key switch
    {
        Key.Space => "Space",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        _ => key.ToString()
    };

    private static HotkeyModifiers ParseModifier(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" => HotkeyModifiers.Ctrl,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None
    };

    private static bool TryParseKey(string token, out Key key)
    {
        key = default;
        var upper = token.ToUpperInvariant();
        if (upper == "SPACE")
        {
            key = Key.Space;
            return true;
        }

        if (upper.Length == 1 && upper[0] is >= 'A' and <= 'Z')
        {
            key = Key.A + (upper[0] - 'A');
            return true;
        }

        if (upper.Length == 1 && upper[0] is >= '0' and <= '9')
        {
            key = Key.D0 + (upper[0] - '0');
            return true;
        }

        if (upper.Length is 2 or 3 && upper[0] == 'F'
            && int.TryParse(upper.AsSpan(1), out var number) && number is >= 1 and <= 12)
        {
            key = Key.F1 + (number - 1);
            return true;
        }

        return false;
    }
}

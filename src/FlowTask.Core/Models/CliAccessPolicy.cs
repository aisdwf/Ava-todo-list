namespace FlowTask.Core.Models;

/// <summary>
/// 命令行权限的唯一解析（Article 6）。设置页与命令行都读这些键，不各写一套默认值。
/// </summary>
/// <remarks>
/// 总开关关闭时，读和写的存储值仍然保留，但 <see cref="ReadPermitted"/> /
/// <see cref="WritePermitted"/> 为 false。读和写只在总开关打开后才允许被改、才生效
/// （所有者原话「总开关打开以后可配置」）。
/// </remarks>
public sealed record CliAccessPolicy(bool Enabled, bool AllowRead, bool AllowWrite, bool MarkWrites)
{
    /// <summary>总开关。缺省关。</summary>
    public const string EnabledKey = "Cli.Enabled";

    /// <summary>允许列出任务。缺省关。</summary>
    public const string AllowReadKey = "Cli.AllowRead";

    /// <summary>允许加入和删除。缺省关。</summary>
    public const string AllowWriteKey = "Cli.AllowWrite";

    /// <summary>之后的命令行写入是否记下标记。缺省开。</summary>
    public const string MarkWritesKey = "Cli.MarkWrites";

    /// <summary>未写过设置时的策略：总开关、读、写都关，标记开。</summary>
    public static CliAccessPolicy Default { get; } = new(
        Enabled: false,
        AllowRead: false,
        AllowWrite: false,
        MarkWrites: true);

    /// <summary>总开关与读取都打开。</summary>
    public bool ReadPermitted => Enabled && AllowRead;

    /// <summary>总开关与写入都打开。</summary>
    public bool WritePermitted => Enabled && AllowWrite;

    /// <summary>把设置里的字符串读成布尔。空白或无法识别时用缺省，避免一条坏数据把权限打翻。</summary>
    public static bool ReadBool(string? raw, bool fallback)
        => bool.TryParse(raw, out var value) ? value : fallback;

    /// <summary>写入设置的规范形式。</summary>
    public static string StoreBool(bool value) => value ? "true" : "false";
}

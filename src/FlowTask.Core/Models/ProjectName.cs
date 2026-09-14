namespace FlowTask.Core.Models;

/// <summary>
/// 项目名称的校验规则真源。
/// </summary>
/// <remarks>
/// 与 <see cref="TaskTitle"/> 分立而非共用一套规则：二者上限不同，
/// 且错误文案需分别面向「任务」与「项目」两个概念。
/// 若强行合并为一个通用 <c>TextField.Validate(max)</c>，
/// 调用方就得在每处自行记住该传哪个上限 —— 那正是本类要消除的漂移来源。
/// </remarks>
public static class ProjectName
{
    /// <summary>
    /// 项目名长度上限。
    /// </summary>
    /// <remarks>
    /// 取 50：项目名需完整显示在 272px 宽的侧边栏内，
    /// 过长会被截断而失去辨识作用。这是由布局约束反推出的业务上限。
    /// </remarks>
    public const int MaxLength = 50;

    /// <summary>
    /// 校验项目名。
    /// </summary>
    /// <returns>合法返回 <c>null</c>；非法返回可直接展示给用户的错误消息。</returns>
    public static string? Validate(string? rawName)
    {
        var trimmed = rawName?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return "项目名称不能为空。";
        }

        if (trimmed.Length > MaxLength)
        {
            return $"项目名称不能超过 {MaxLength} 个字符（当前 {trimmed.Length}）。";
        }

        return null;
    }

    /// <summary>
    /// 判断项目名是否合法。
    /// </summary>
    public static bool IsValid(string? rawName) => Validate(rawName) is null;

    /// <summary>
    /// 规范化项目名：修剪首尾空白并折叠内部连续空白。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="TaskTitle.Normalize"/> 不同，此处**折叠内部空白**。
    /// 原因：项目名是被反复引用的标识符，
    /// 「我的 项目」与「我的  项目」在视觉上无法区分却是两个不同名称，
    /// 会让用户误以为建了重复项目。标题是一次性文本，无此问题。
    /// </remarks>
    public static string Normalize(string? rawName)
    {
        var trimmed = rawName?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

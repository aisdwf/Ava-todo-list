namespace FlowTask.Core.Models;

/// <summary>
/// 标签名称的校验规则真源。
/// </summary>
public static class TagName
{
    /// <summary>标签名称长度上限。</summary>
    public const int MaxLength = 30;

    /// <summary>
    /// 校验标签名称。
    /// </summary>
    /// <param name="rawName">原始名称。</param>
    /// <returns>合法返回 <c>null</c>；非法返回错误消息。</returns>
    public static string? Validate(string? rawName)
    {
        var normalized = Normalize(rawName);
        if (normalized.Length == 0)
        {
            return "标签名称不能为空。";
        }

        return normalized.Length > MaxLength
            ? $"标签名称不能超过 {MaxLength} 个字符（当前 {normalized.Length}）。"
            : null;
    }

    /// <summary>判断标签名称是否合法。</summary>
    public static bool IsValid(string? rawName) => Validate(rawName) is null;

    /// <summary>
    /// 修剪首尾空白并折叠内部连续空白。
    /// </summary>
    public static string Normalize(string? rawName)
    {
        var trimmed = rawName?.Trim() ?? string.Empty;
        return trimmed.Length == 0
            ? string.Empty
            : string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

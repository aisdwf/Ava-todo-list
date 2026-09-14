namespace FlowTask.Core.Models;

/// <summary>
/// 标签字符串的规范化与解析。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么集中在此</b>：标签以分隔符拼接存于 <see cref="TaskItem.Tags"/>，
/// 而主窗口与随手记小窗都会写入标签。若两处各自实现拆分与去重，
/// 必然出现规则漂移（一处过滤空白、另一处不过滤），
/// 使同一份数据在不同入口产生不同形态。此处为唯一真源（Article 6）。
/// </para>
/// <para>
/// <b>与未来输入语法的关系</b>：design-domain-contract §3.2 已认可
/// <c>#项目 @标签</c> 内联语法为正确方向。届时解析器产出的标签
/// 仍须经本类规范化，故本类不应耦合任何语法细节。
/// </para>
/// </remarks>
public static class TagNormalizer
{
    /// <summary>
    /// 标签分隔符。
    /// </summary>
    /// <remarks>
    /// 定为英文逗号。标签文本内部出现的分隔符必须被剔除，
    /// 否则单个标签会在读取时被误拆为两个（见 <see cref="Sanitize"/>）。
    /// </remarks>
    public const char Separator = ',';

    /// <summary>
    /// 将标签序列规范化为可落库的字符串。
    /// </summary>
    /// <param name="tags">原始标签序列，允许含空白项、重复项与非法字符。</param>
    /// <returns>规范化字符串；无有效标签时返回 <c>null</c> 而非空串。</returns>
    /// <remarks>
    /// 统一返回 <c>null</c> 表示「无标签」，避免库中同时存在 <c>null</c> 与 <c>""</c>
    /// 两种等价表示 —— 那会迫使每个查询都写两个条件。
    /// </remarks>
    public static string? Normalize(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return null;
        }

        var result = new List<string>();
        // 大小写不敏感去重，但保留用户首次输入的原始大小写用于显示
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in tags)
        {
            var tag = Sanitize(raw);
            if (tag.Length == 0)
            {
                continue;
            }

            if (seen.Add(tag))
            {
                result.Add(tag);
            }
        }

        return result.Count == 0 ? null : string.Join(Separator, result);
    }

    /// <summary>
    /// 规范化以分隔符拼接的标签字符串。
    /// </summary>
    public static string? Normalize(string? tags)
        => tags is null ? null : Normalize(Split(tags));

    /// <summary>
    /// 将落库字符串解析为标签列表。
    /// </summary>
    /// <returns>无标签时返回空列表，不返回 <c>null</c>，便于调用方直接遍历。</returns>
    public static IReadOnlyList<string> Split(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return Array.Empty<string>();
        }

        return tags
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// 判断任务是否含指定标签（大小写不敏感）。
    /// </summary>
    public static bool Contains(string? tags, string tag)
    {
        var target = Sanitize(tag);
        if (target.Length == 0)
        {
            return false;
        }

        return Split(tags).Any(t => string.Equals(t, target, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 清理单个标签：剔除分隔符、折叠空白、去除首尾空格。
    /// </summary>
    /// <remarks>
    /// 剔除而非转义分隔符：转义需要在读取侧对称反转义，
    /// 任何一侧遗漏都会产生难以追踪的数据错乱。
    /// 标签是轻量概念，不值得为「标签名里能带逗号」这一边缘需求引入转义机制。
    /// </remarks>
    private static string Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var cleaned = raw.Replace(Separator.ToString(), string.Empty).Trim();

        // 内部连续空白折叠为单个空格，避免「紧急  修复」与「紧急 修复」被视为不同标签
        return string.Join(' ', cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

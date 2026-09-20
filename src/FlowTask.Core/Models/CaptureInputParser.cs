namespace FlowTask.Core.Models;

/// <summary>
/// 小窗输入语法解析：<c>@项目</c> / <c>#标签</c>（R-1.6）。
/// </summary>
/// <remarks>
/// <para>
/// 纯函数、无 I/O。形如 <c>@名</c>/<c>#名</c> 的 token 一律从标题剥离；
/// 是否已存在由调用方决定匹配或创建（R-1.8，2026-09-18）。
/// </para>
/// <para>
/// Token 以空白分隔；名称本身不允许空白（与补全选中后写入的形式一致）。
/// 仅当恰好一个前导 <c>@</c>/<c>#</c> 且次字符不是同类符号时才解析，
/// 故 <c>##like</c> / <c>###like</c> / <c>@@x</c> 留在标题。
/// </para>
/// </remarks>
public static class CaptureInputParser
{
    /// <summary>一次解析的结果。</summary>
    /// <param name="Title">去掉全部合法 <c>@</c>/<c>#</c> token 后的标题。</param>
    /// <param name="ProjectName">最后一个合法 <c>@</c> token 的名称；无则为 <c>null</c>（调用方回落 Default）。</param>
    /// <param name="TagNames">合法 <c>#</c> token 名称列表（已规范化、去重，出现序）。</param>
    public readonly record struct Result(
        string Title,
        string? ProjectName,
        IReadOnlyList<string> TagNames);

    /// <summary>
    /// 解析输入文本。
    /// </summary>
    /// <param name="raw">原始输入。</param>
    /// <param name="knownProjects">保留参数以兼容调用方；匹配/创建由调用方负责。</param>
    /// <param name="knownTags">保留参数以兼容调用方；匹配/创建由调用方负责。</param>
    public static Result Parse(
        string? raw,
        IEnumerable<string> knownProjects,
        IEnumerable<string> knownTags)
    {
        _ = knownProjects;
        _ = knownTags;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Result(string.Empty, null, Array.Empty<string>());
        }

        var parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string? projectName = null;
        var tagNames = new List<string>();
        var titleParts = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            if (TryExtractSigilName(part, '@', out var projectCandidate))
            {
                projectName = projectCandidate;
                continue;
            }

            if (TryExtractSigilName(part, '#', out var tagCandidate))
            {
                if (!tagNames.Contains(tagCandidate, StringComparer.OrdinalIgnoreCase))
                {
                    tagNames.Add(tagCandidate);
                }

                continue;
            }

            titleParts.Add(part);
        }

        var title = string.Join(' ', titleParts);
        return new Result(title, projectName, tagNames);
    }

    /// <summary>
    /// 检测用于补全的「正在输入」token：光标前最后一个以单个 <c>@</c>/<c>#</c> 开头且不含空白的片段。
    /// </summary>
    public static bool TryGetCompletionToken(
        string? text,
        int caretIndex,
        out char sigil,
        out string prefix,
        out int tokenStart)
    {
        sigil = '\0';
        prefix = string.Empty;
        tokenStart = -1;

        if (string.IsNullOrEmpty(text) || caretIndex < 0)
        {
            return false;
        }

        caretIndex = Math.Min(caretIndex, text.Length);
        var i = caretIndex - 1;
        while (i >= 0 && !char.IsWhiteSpace(text[i]))
        {
            i--;
        }

        tokenStart = i + 1;
        if (tokenStart >= caretIndex)
        {
            return false;
        }

        var token = text[tokenStart..caretIndex];
        if (!IsSingleSigilToken(token, out sigil))
        {
            return false;
        }

        prefix = token[1..];
        return true;
    }

    /// <summary>
    /// 合法 token：恰好一个前导 sigil，且其后至少一字且不以同一 sigil 开头。
    /// </summary>
    private static bool TryExtractSigilName(string part, char sigil, out string name)
    {
        name = string.Empty;
        if (!IsSingleSigilToken(part, out var found) || found != sigil)
        {
            return false;
        }

        var normalized = sigil == '@'
            ? ProjectName.Normalize(part[1..])
            : TagName.Normalize(part[1..]);
        if (normalized.Length == 0)
        {
            return false;
        }

        name = normalized;
        return true;
    }

    private static bool IsSingleSigilToken(string token, out char sigil)
    {
        sigil = '\0';
        if (token.Length < 2)
        {
            return false;
        }

        var first = token[0];
        if (first != '@' && first != '#')
        {
            return false;
        }

        // ##like / @@x / ###like：多重前导符号视为普通文本
        if (token[1] == first)
        {
            return false;
        }

        sigil = first;
        return true;
    }
}

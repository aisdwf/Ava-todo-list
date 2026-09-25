namespace FlowTask.Core.Models;

/// <summary>
/// 小窗输入语法解析：<c>@项目</c>（R-1.6）。
/// </summary>
/// <remarks>
/// <para>
/// 纯函数、无 I/O。形如 <c>@名</c> 的 token 一律从标题剥离；
/// 是否已存在由调用方决定匹配或创建（R-1.8，2026-09-18）。
/// </para>
/// <para>
/// Token 以空白分隔；名称本身不允许空白（与补全选中后写入的形式一致）。
/// 仅当恰好一个前导 <c>@</c> 且次字符不是同类符号时才解析，
/// 故 <c>@@x</c> 留在标题。
/// </para>
/// </remarks>
public static class CaptureInputParser
{
    /// <summary>一次解析的结果。</summary>
    /// <param name="Title">去掉全部合法 <c>@</c> token 后的标题。</param>
    /// <param name="ProjectName">最后一个合法 <c>@</c> token 的名称；无则为 <c>null</c>（调用方回落 Default）。</param>
    public readonly record struct Result(
        string Title,
        string? ProjectName);

    /// <summary>
    /// 解析输入文本。
    /// </summary>
    /// <param name="raw">原始输入。</param>
    /// <param name="knownProjects">保留参数以兼容调用方；匹配/创建由调用方负责。</param>
    public static Result Parse(
        string? raw,
        IEnumerable<string> knownProjects)
    {
        _ = knownProjects;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Result(string.Empty, null);
        }

        var parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string? projectName = null;
        var titleParts = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            if (TryExtractSigilName(part, out var projectCandidate))
            {
                projectName = projectCandidate;
                continue;
            }

            titleParts.Add(part);
        }

        var title = string.Join(' ', titleParts);
        return new Result(title, projectName);
    }

    /// <summary>
    /// 检测用于补全的「正在输入」token：光标前最后一个以单个 <c>@</c> 开头且不含空白的片段。
    /// </summary>
    public static bool TryGetCompletionToken(
        string? text,
        int caretIndex,
        out string prefix,
        out int tokenStart)
    {
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
        if (!IsSingleSigilToken(token))
        {
            return false;
        }

        prefix = token[1..];
        return true;
    }

    /// <summary>
    /// 合法 token：恰好一个前导 <c>@</c>，且其后至少一字且不以 <c>@</c> 开头。
    /// </summary>
    private static bool TryExtractSigilName(string part, out string name)
    {
        name = string.Empty;
        if (!IsSingleSigilToken(part))
        {
            return false;
        }

        var normalized = ProjectName.Normalize(part[1..]);
        if (normalized.Length == 0)
        {
            return false;
        }

        name = normalized;
        return true;
    }

    private static bool IsSingleSigilToken(string token)
    {
        if (token.Length < 2)
        {
            return false;
        }

        if (token[0] != '@')
        {
            return false;
        }

        // @@x：多重前导符号视为普通文本
        if (token[1] == '@')
        {
            return false;
        }

        return true;
    }
}

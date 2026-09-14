namespace FlowTask.Core.Models;

/// <summary>
/// 任务标题的校验规则真源。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么抽成独立类型</b>：标题校验此前散落三处 ——
/// 主窗口创建、主窗口编辑、随手记保存，且三处规则并不一致：
/// 创建路径只判空白，编辑路径判空白并恢复原值，而**没有任何一处有长度上限**。
/// 靠注释约定一致的规则必然漂移（Article 6）。
/// </para>
/// <para>
/// 此处为唯一固定点：新增任何标题入口（如未来的输入语法解析）
/// 一律复用本类，不得自行判空或另定上限。
/// </para>
/// </remarks>
public static class TaskTitle
{
    /// <summary>
    /// 标题长度上限。
    /// </summary>
    /// <remarks>
    /// 取 200：标题是「一句话说清一件事」，超出这个量级说明内容应放入备注。
    /// 上限的作用是防御异常输入（如误粘贴整段文本）而非限制正常表达，
    /// 故取值偏宽松。
    /// </remarks>
    public const int MaxLength = 200;

    /// <summary>
    /// 校验标题。
    /// </summary>
    /// <param name="rawTitle">原始输入，允许为 <c>null</c>。</param>
    /// <returns>合法返回 <c>null</c>；非法返回可直接展示给用户的错误消息。</returns>
    public static string? Validate(string? rawTitle)
    {
        var trimmed = rawTitle?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return "任务标题不能为空。";
        }

        if (trimmed.Length > MaxLength)
        {
            return $"任务标题不能超过 {MaxLength} 个字符（当前 {trimmed.Length}）。";
        }

        return null;
    }

    /// <summary>
    /// 判断标题是否合法。
    /// </summary>
    public static bool IsValid(string? rawTitle) => Validate(rawTitle) is null;

    /// <summary>
    /// 规范化标题：仅修剪首尾空白。
    /// </summary>
    /// <remarks>
    /// <b>刻意不处理内部字符</b>：未来的 <c>#项目</c> / <c>@标签</c> 输入语法
    /// 依赖 <c>#</c> / <c>@</c> 等字符原样保留（DESIGN-0004 §3.2 预留结构）。
    /// 若在此折叠内部空白或过滤符号，那些语法将无从解析。
    /// </remarks>
    public static string Normalize(string? rawTitle) => rawTitle?.Trim() ?? string.Empty;
}

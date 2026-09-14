namespace FlowTask.Core.Interfaces;

/// <summary>
/// 时间提供者抽象。
/// </summary>
/// <remarks>
/// <para>
/// 为什么需要此抽象：业务分支一旦直接读取 <see cref="DateTime.Now"/> /
/// <see cref="DateTime.Today"/>，其行为便依赖不可控的宿主状态，
/// 既无法编写确定性测试（如「跨越午夜时任务是否移出今日视图」），
/// 也使调试结果不可复现。此为 AI_CONSTITUTION Article 9 明令禁止的模式。
/// </para>
/// <para>
/// <b>为什么同时暴露 UTC 与本地两组成员</b>：本项目存在两类语义完全不同的时间值，
/// 混用同一基准是既有时区缺陷的根源：
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <b>时刻（instant）</b> —— 「事件在哪一瞬间发生」，如任务创建、完成时间。
///     UTC 是唯一无歧义表示，用 <see cref="UtcNow"/>。
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>日历日（calendar date）</b> —— 「用户日历上的哪一天」，如任务到期日。
///     它天然属于用户所在时区，若按 UTC 时刻存取，跨时区或夏令时变更后会漂移到前后一天。
///     用 <see cref="Today"/>。
///     </description>
///   </item>
/// </list>
/// <para>
/// 把两者放在同一接口上并加以区分命名，使调用方在选择成员时
/// 就被迫明确自己要的是哪一种语义，而非事后才发现基准错配。
/// </para>
/// </remarks>
public interface IClock
{
    /// <summary>
    /// 当前 UTC 时刻。用于记录「事件何时发生」，如 <c>CreatedAt</c> / <c>CompletedAt</c>。
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// 用户本地时区的今天（当日零点，<see cref="DateTimeKind.Unspecified"/>）。
    /// </summary>
    /// <remarks>
    /// 返回 <see cref="DateTimeKind.Unspecified"/> 而非 Local：
    /// 该值代表一个「日历日」而非某个具体瞬间，附加时区信息会诱使调用方
    /// 对其做时区转换 —— 而任何转换都会破坏「哪一天」这一语义。
    /// </remarks>
    DateTime Today { get; }
}

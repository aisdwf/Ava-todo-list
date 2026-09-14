using FlowTask.Core.Interfaces;

namespace FlowTask.Tests;

/// <summary>
/// 可控时钟：让依赖「现在」的业务分支变成确定性可测。
/// </summary>
/// <remarks>
/// 存在意义即 <see cref="IClock"/> 抽象的价值证明 —— 在此之前
/// 「今日」区间的边界行为无法被断言，因为测试无从控制当前时间。
/// </remarks>
public sealed class FakeClock : IClock
{
    /// <summary>
    /// 构造可控时钟。
    /// </summary>
    /// <param name="utcNow">固定的 UTC 时刻。</param>
    /// <param name="today">
    /// 固定的本地日历日。传 <c>null</c> 时取 <paramref name="utcNow"/> 的日期部分 ——
    /// 仅适用于不关心时区差异的用例；跨时区场景须显式指定，
    /// 否则测试会隐含「本地时区等于 UTC」这一错误前提。
    /// </param>
    public FakeClock(DateTime utcNow, DateTime? today = null)
    {
        UtcNow = utcNow;
        Today = DateTime.SpecifyKind((today ?? utcNow).Date, DateTimeKind.Unspecified);
    }

    /// <inheritdoc />
    public DateTime UtcNow { get; set; }

    /// <inheritdoc />
    public DateTime Today { get; set; }
}

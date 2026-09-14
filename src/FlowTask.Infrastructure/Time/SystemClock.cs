using FlowTask.Core.Interfaces;

namespace FlowTask.Infrastructure.Time;

/// <summary>
/// 基于宿主系统时钟的 <see cref="IClock"/> 实现。
/// </summary>
/// <remarks>
/// 这是全项目**唯一**允许直接触达系统时钟的位置（AI_CONSTITUTION Article 9）。
/// 其余代码一律经 <see cref="IClock"/> 获取时间，以保证业务分支可测试、可复现。
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="DateTime.Today"/> 已返回本地当日零点且 Kind 为 Local，
    /// 此处显式转为 <see cref="DateTimeKind.Unspecified"/> 以符合契约 ——
    /// 避免调用方误将「日历日」当作可做时区换算的瞬间。
    /// </remarks>
    public DateTime Today => DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
}

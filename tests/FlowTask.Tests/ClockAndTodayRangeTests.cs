using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖「今日聚焦」区间语义与时钟注入。
/// </summary>
/// <remarks>
/// 这些用例针对两个真实缺陷：
/// <list type="number">
///   <item><description>
///   原实现以 <c>DateTime.Today.ToUniversalTime()</c> 为区间起点，
///   把「日历日」当「瞬间」处理，非 UTC 时区下区间错位。
///   </description></item>
///   <item><description>
///   原实现直接读系统时钟（违反 Article 9），使下述任何断言都无法成立 ——
///   测试无从指定「今天是哪天」。
///   </description></item>
/// </list>
/// </remarks>
public class ClockAndTodayRangeTests : IDisposable
{
    private readonly string _dbPath;

    public ClockAndTodayRangeTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_clock_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // 清理阶段的文件锁不影响断言结果
            }
        }
    }

    private SqliteTaskRepository CreateRepo(FakeClock clock) => new(clock, _dbPath);

    private static TaskItem Task(string title, DateTime createdAt, DateTime? dueDate = null)
        => new() { Title = title, CreatedAt = createdAt, DueDate = dueDate };

    [Fact]
    public async Task GetTodayTasks_IncludesTasksDueToday()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(Task("今天到期", clock.UtcNow, new DateTime(2026, 3, 10)));

        var todays = await repo.GetTodayTasksAsync();

        Assert.Single(todays);
        Assert.Equal("今天到期", todays[0].Title);
    }

    /// <summary>
    /// 逾期任务必须留在「今日」视图内。
    /// </summary>
    /// <remarks>
    /// 若只取「恰好今天」，昨天到期而未完成的任务会同时从「今日」和视觉焦点中消失，
    /// 成为注意力盲区，与该视图定位矛盾（design-domain-contract §5.4）。
    /// </remarks>
    [Fact]
    public async Task GetTodayTasks_IncludesOverdueTasks()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(Task("三天前就该做", clock.UtcNow, new DateTime(2026, 3, 7)));
        await repo.SaveTaskAsync(Task("今天到期", clock.UtcNow, new DateTime(2026, 3, 10)));

        var todays = await repo.GetTodayTasksAsync();

        Assert.Equal(2, todays.Count);
        // 逾期项排在前：到期日升序，越早越紧迫
        Assert.Equal("三天前就该做", todays[0].Title);
    }

    [Fact]
    public async Task GetTodayTasks_ExcludesFutureTasks()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(Task("明天的事", clock.UtcNow, new DateTime(2026, 3, 11)));

        var todays = await repo.GetTodayTasksAsync();

        Assert.Empty(todays);
    }

    /// <summary>
    /// 无到期日的任务不属于「今日」。
    /// </summary>
    /// <remarks>
    /// 依零必填原则，无到期日是任务的默认状态且完全正常。
    /// 它不该出现在「今日」（那会让该视图等同于「全部」），
    /// 但也因此不能作为小窗的过滤条件 —— 否则大多数任务永不可见（design-domain-contract §4.5）。
    /// </remarks>
    [Fact]
    public async Task GetTodayTasks_ExcludesTasksWithoutDueDate()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(Task("随手记的想法", clock.UtcNow));

        var todays = await repo.GetTodayTasksAsync();

        Assert.Empty(todays);
    }

    [Fact]
    public async Task GetTodayTasks_ExcludesCompletedAndDeleted()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        var due = new DateTime(2026, 3, 10);
        var done = Task("已完成", clock.UtcNow, due);
        done.IsCompleted = true;
        var removed = Task("已删除", clock.UtcNow, due);
        removed.IsDeleted = true;

        await repo.SaveTaskAsync(done);
        await repo.SaveTaskAsync(removed);

        var todays = await repo.GetTodayTasksAsync();

        Assert.Empty(todays);
    }

    /// <summary>
    /// 核心回归：本地时区领先 UTC 时，当天到期的任务不得被判为「未来」。
    /// </summary>
    /// <remarks>
    /// 这正是原实现的失效场景。以 UTC+8 的 2026-03-10 07:00 为例，
    /// UTC 时刻仍是 2026-03-09 23:00。原实现用 <c>DateTime.Today.ToUniversalTime()</c>
    /// 得到的区间起点会落在本地日期的前一天，导致区间整体偏移。
    /// 现改为在本地日历日维度直接比较、全程不做时区换算，故不受影响。
    /// </remarks>
    [Fact]
    public async Task GetTodayTasks_IsCorrectWhenLocalDateLeadsUtcDate()
    {
        // UTC 尚在 3/9 晚间，本地日历已翻到 3/10
        var clock = new FakeClock(
            utcNow: new DateTime(2026, 3, 9, 23, 0, 0, DateTimeKind.Utc),
            today: new DateTime(2026, 3, 10));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(Task("本地今天到期", clock.UtcNow, new DateTime(2026, 3, 10)));

        var todays = await repo.GetTodayTasksAsync();

        Assert.Single(todays);
        Assert.Equal("本地今天到期", todays[0].Title);
    }

    /// <summary>
    /// 反向时区场景：本地日期落后于 UTC 日期时，次日到期项仍须被排除。
    /// </summary>
    [Fact]
    public async Task GetTodayTasks_IsCorrectWhenLocalDateLagsUtcDate()
    {
        // UTC 已进入 3/10 凌晨，本地（如 UTC-5）仍是 3/9
        var clock = new FakeClock(
            utcNow: new DateTime(2026, 3, 10, 2, 0, 0, DateTimeKind.Utc),
            today: new DateTime(2026, 3, 9));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(Task("本地明天到期", clock.UtcNow, new DateTime(2026, 3, 10)));
        await repo.SaveTaskAsync(Task("本地今天到期", clock.UtcNow, new DateTime(2026, 3, 9)));

        var todays = await repo.GetTodayTasksAsync();

        Assert.Single(todays);
        Assert.Equal("本地今天到期", todays[0].Title);
    }

    /// <summary>
    /// 午夜翻页：同一任务在时钟跨日后应自动从「今日」转为「逾期但仍在今日视图」。
    /// </summary>
    /// <remarks>
    /// 该断言在引入 <c>IClock</c> 前完全无法编写 —— 这正是 Article 9
    /// 「不可注入的时钟使验证不可复现」的具体体现。
    /// </remarks>
    [Fact]
    public async Task GetTodayTasks_KeepsTaskVisibleAfterMidnightRollover()
    {
        var clock = new FakeClock(
            utcNow: new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc),
            today: new DateTime(2026, 3, 10));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(Task("拖过夜的任务", clock.UtcNow, new DateTime(2026, 3, 10)));

        Assert.Single(await repo.GetTodayTasksAsync());

        // 时钟推进到次日：任务变为逾期，但依「今日含逾期」定义仍须可见
        clock.Today = new DateTime(2026, 3, 11);
        clock.UtcNow = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);

        var afterRollover = await repo.GetTodayTasksAsync();

        Assert.Single(afterRollover);
        Assert.Equal("拖过夜的任务", afterRollover[0].Title);
    }

    /// <summary>
    /// 到期日必须以「日历日」形态落库：时间部分剥离、Kind 归一。
    /// </summary>
    /// <remarks>
    /// 若保留时间部分，「今日」区间的开区间上界比较会出现边界歧义
    /// （如 23:59 到期项在某些换算下溢出次日）。
    /// </remarks>
    [Fact]
    public async Task SaveTask_NormalizesDueDateToCalendarDay()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        var task = Task("带时间的到期日", clock.UtcNow, new DateTime(2026, 3, 10, 17, 45, 30));
        await repo.SaveTaskAsync(task);

        var stored = await repo.GetByIdAsync(task.Id);

        Assert.NotNull(stored);
        Assert.NotNull(stored.DueDate);
        Assert.Equal(new DateTime(2026, 3, 10), stored.DueDate!.Value);
        Assert.Equal(TimeSpan.Zero, stored.DueDate.Value.TimeOfDay);
    }

    /// <summary>
    /// 23:59 到期的任务归一化后仍属当天，不得溢出到次日被判为「未来」。
    /// </summary>
    [Fact]
    public async Task GetTodayTasks_IncludesTaskDueLateInTheDay()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        await repo.SaveTaskAsync(
            Task("今晚截止", clock.UtcNow, new DateTime(2026, 3, 10, 23, 59, 59)));

        var todays = await repo.GetTodayTasksAsync();

        Assert.Single(todays);
    }

    /// <summary>
    /// 同日到期时按优先级降序排列。
    /// </summary>
    [Fact]
    public async Task GetTodayTasks_OrdersSameDayByPriorityDescending()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        var due = new DateTime(2026, 3, 10);
        var low = Task("低优先", clock.UtcNow, due);
        low.Priority = TaskPriority.Low;
        var high = Task("高优先", clock.UtcNow, due);
        high.Priority = TaskPriority.High;

        await repo.SaveTaskAsync(low);
        await repo.SaveTaskAsync(high);

        var todays = await repo.GetTodayTasksAsync();

        Assert.Equal(2, todays.Count);
        Assert.Equal("高优先", todays[0].Title);
    }

    /// <summary>
    /// 未显式赋值 <c>CreatedAt</c> 时必须快速失败。
    /// </summary>
    /// <remarks>
    /// 实体默认值已改为 <c>default</c>（不再由字段初始化器隐式填充），
    /// 因此需要仓储这一唯一写入漏斗兜底，避免零值时间静默入库。
    /// </remarks>
    [Fact]
    public async Task SaveTask_RejectsUnassignedCreatedAt()
    {
        var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        var repo = CreateRepo(clock);

        var task = new TaskItem { Title = "忘记设置创建时间" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.SaveTaskAsync(task));

        Assert.Contains("CreatedAt", ex.Message);
    }
}

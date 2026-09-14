using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖校验值对象。
/// </summary>
/// <remarks>
/// 这些规则此前散落三处且互不一致（创建只判空白、编辑判空白+回滚、
/// 均无长度上限）。抽为值对象后需锁定其边界行为，
/// 否则「单一真源」只是换了个位置的约定而非保障。
/// </remarks>
public class TaskTitleTests
{
    [Fact]
    public void Validate_RejectsNull() => Assert.NotNull(TaskTitle.Validate(null));

    [Fact]
    public void Validate_RejectsEmptyAndWhitespace()
    {
        Assert.NotNull(TaskTitle.Validate(""));
        Assert.NotNull(TaskTitle.Validate("   "));
        Assert.NotNull(TaskTitle.Validate("\t\n"));
    }

    [Fact]
    public void Validate_AcceptsNormalTitle() => Assert.Null(TaskTitle.Validate("写单元测试"));

    /// <summary>
    /// 恰好等于上限时合法，超出一个字符即非法。
    /// </summary>
    [Fact]
    public void Validate_BoundaryAtMaxLength()
    {
        Assert.Null(TaskTitle.Validate(new string('a', TaskTitle.MaxLength)));
        Assert.NotNull(TaskTitle.Validate(new string('a', TaskTitle.MaxLength + 1)));
    }

    /// <summary>
    /// 长度按修剪后计算，首尾空白不占额度。
    /// </summary>
    [Fact]
    public void Validate_MeasuresLengthAfterTrim()
    {
        var padded = "  " + new string('a', TaskTitle.MaxLength) + "  ";
        Assert.Null(TaskTitle.Validate(padded));
    }

    [Fact]
    public void Normalize_TrimsOuterWhitespaceOnly()
    {
        Assert.Equal("写 测试", TaskTitle.Normalize("  写 测试  "));
    }

    /// <summary>
    /// 标题内部字符必须原样保留。
    /// </summary>
    /// <remarks>
    /// 未来的 <c>#项目</c> / <c>@标签</c> 输入语法依赖这些字符不被提前处理
    /// （design-domain-contract §3.2 预留结构）。若在此折叠内部空白或过滤符号，
    /// 那些语法将无从解析。
    /// </remarks>
    [Fact]
    public void Normalize_PreservesInnerCharactersAndSymbols()
    {
        Assert.Equal("修 bug  #FlowTask @紧急", TaskTitle.Normalize("  修 bug  #FlowTask @紧急  "));
    }

    [Fact]
    public void Normalize_ReturnsEmptyForNull() => Assert.Equal(string.Empty, TaskTitle.Normalize(null));

    [Fact]
    public void IsValid_MirrorsValidate()
    {
        Assert.True(TaskTitle.IsValid("正常标题"));
        Assert.False(TaskTitle.IsValid(""));
        Assert.False(TaskTitle.IsValid(new string('a', TaskTitle.MaxLength + 1)));
    }
}

/// <summary>
/// 覆盖项目名校验。
/// </summary>
public class ProjectNameTests
{
    [Fact]
    public void Validate_RejectsBlank()
    {
        Assert.NotNull(ProjectName.Validate(null));
        Assert.NotNull(ProjectName.Validate("   "));
    }

    [Fact]
    public void Validate_BoundaryAtMaxLength()
    {
        Assert.Null(ProjectName.Validate(new string('x', ProjectName.MaxLength)));
        Assert.NotNull(ProjectName.Validate(new string('x', ProjectName.MaxLength + 1)));
    }

    /// <summary>
    /// 项目名折叠内部空白，与任务标题的处理刻意不同。
    /// </summary>
    /// <remarks>
    /// 项目名是被反复引用的标识符：「我的 项目」与「我的  项目」
    /// 视觉上无法区分却是两个不同名称，会让用户误以为建了重复项目。
    /// 任务标题是一次性文本，无此问题。
    /// </remarks>
    [Fact]
    public void Normalize_CollapsesInnerWhitespace()
    {
        Assert.Equal("我的 项目", ProjectName.Normalize("  我的    项目  "));
    }

    [Fact]
    public void Normalize_DiffersFromTaskTitleDeliberately()
    {
        const string raw = "A    B";

        // 标题保留内部空白，项目名折叠 —— 这是有意的差异
        Assert.Equal("A    B", TaskTitle.Normalize(raw));
        Assert.Equal("A B", ProjectName.Normalize(raw));
    }

    [Fact]
    public void MaxLength_IsShorterThanTaskTitle()
    {
        // 项目名须完整显示在 272px 侧边栏内，故上限更严
        Assert.True(ProjectName.MaxLength < TaskTitle.MaxLength);
    }
}

/// <summary>
/// 覆盖仓储层的校验兜底。
/// </summary>
/// <remarks>
/// 仓储是唯一写入漏斗，即使某个 UI 入口遗漏校验，也不得让非法数据落库。
/// </remarks>
public class RepositoryValidationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteTaskRepository _tasks;
    private readonly SqliteProjectRepository _projects;

    public RepositoryValidationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_val_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _tasks = new SqliteTaskRepository(_clock, _dbPath);
        _projects = new SqliteProjectRepository(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* 清理锁不影响断言 */ }
        }
    }

    [Fact]
    public async Task SaveTask_RejectsBlankTitle()
    {
        var task = TaskItemFactory.Create(_clock, "占位");
        task.Title = "   ";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _tasks.SaveTaskAsync(task));
        Assert.Contains("标题", ex.Message);
    }

    [Fact]
    public async Task SaveTask_RejectsOverlongTitle()
    {
        var task = TaskItemFactory.Create(_clock, "占位");
        task.Title = new string('a', TaskTitle.MaxLength + 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _tasks.SaveTaskAsync(task));
    }

    /// <summary>
    /// 仓储写入时统一规范化标签，即使调用方直接赋了原始字符串。
    /// </summary>
    [Fact]
    public async Task SaveTask_NormalizesTagsAtRepositoryLevel()
    {
        var task = TaskItemFactory.Create(_clock, "任务");
        task.Tags = " Bug , bug ,线上 ";

        await _tasks.SaveTaskAsync(task);

        var stored = await _tasks.GetByIdAsync(task.Id);
        Assert.Equal("Bug,线上", stored!.Tags);
    }

    [Fact]
    public async Task SaveProject_RejectsBlankName()
    {
        var project = new Project { Name = "  ", CreatedAt = _clock.UtcNow };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _projects.SaveProjectAsync(project));
        Assert.Contains("名称", ex.Message);
    }

    [Fact]
    public async Task SaveProject_RejectsOverlongName()
    {
        var project = new Project
        {
            Name = new string('x', ProjectName.MaxLength + 1),
            CreatedAt = _clock.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _projects.SaveProjectAsync(project));
    }

    [Fact]
    public async Task SaveProject_NormalizesName()
    {
        var project = new Project { Name = "  我的    项目  ", CreatedAt = _clock.UtcNow };

        await _projects.SaveProjectAsync(project);

        var stored = await _projects.GetByIdAsync(project.Id);
        Assert.Equal("我的 项目", stored!.Name);
    }
}

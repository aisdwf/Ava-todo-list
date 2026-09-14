using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖标签规范化规则与任务创建工厂。
/// </summary>
/// <remarks>
/// 标签以分隔符拼接字符串存储（design-domain-contract §2.3），
/// 该方案的正确性完全依赖读写两侧规则一致，故需针对性覆盖。
/// </remarks>
public class TagNormalizerTests
{
    [Fact]
    public void Normalize_ReturnsNullForNullInput()
    {
        Assert.Null(TagNormalizer.Normalize((IEnumerable<string>?)null));
        Assert.Null(TagNormalizer.Normalize((string?)null));
    }

    /// <summary>
    /// 无有效标签时统一返回 <c>null</c>，不返回空串。
    /// </summary>
    /// <remarks>
    /// 若同时存在 <c>null</c> 与 <c>""</c> 两种「无标签」表示，
    /// 每个查询都得写两个条件，是典型的表示冗余。
    /// </remarks>
    [Fact]
    public void Normalize_CollapsesEmptyResultToNull()
    {
        Assert.Null(TagNormalizer.Normalize(new[] { "", "   ", "\t" }));
        Assert.Null(TagNormalizer.Normalize(",,,"));
    }

    [Fact]
    public void Normalize_JoinsWithSeparator()
    {
        var result = TagNormalizer.Normalize(new[] { "紧急", "线上" });

        Assert.Equal("紧急,线上", result);
    }

    [Fact]
    public void Normalize_TrimsWhitespace()
    {
        var result = TagNormalizer.Normalize(new[] { "  紧急  ", "\t线上\n" });

        Assert.Equal("紧急,线上", result);
    }

    /// <summary>
    /// 大小写不敏感去重，但保留首次出现的原始大小写。
    /// </summary>
    [Fact]
    public void Normalize_DeduplicatesCaseInsensitivelyKeepingFirstCasing()
    {
        var result = TagNormalizer.Normalize(new[] { "Bug", "bug", "BUG" });

        Assert.Equal("Bug", result);
    }

    /// <summary>
    /// 标签内的分隔符必须被剔除，否则读取时单个标签会被误拆为两个。
    /// </summary>
    [Fact]
    public void Normalize_StripsSeparatorInsideTag()
    {
        var result = TagNormalizer.Normalize(new[] { "前端,后端" });

        Assert.Equal("前端后端", result);
        // 关键：往返后仍是一个标签，而非两个
        Assert.Single(TagNormalizer.Split(result));
    }

    /// <summary>
    /// 内部连续空白折叠为单个空格。
    /// </summary>
    /// <remarks>
    /// 否则「紧急  修复」与「紧急 修复」会被视为两个不同标签，
    /// 而用户从视觉上无法区分二者 —— 这类不可见差异是标签发散的常见来源。
    /// </remarks>
    [Fact]
    public void Normalize_CollapsesInnerWhitespace()
    {
        var result = TagNormalizer.Normalize(new[] { "紧急    修复" });

        Assert.Equal("紧急 修复", result);
    }

    [Fact]
    public void Split_ReturnsEmptyListForNullOrBlank()
    {
        Assert.Empty(TagNormalizer.Split(null));
        Assert.Empty(TagNormalizer.Split(""));
        Assert.Empty(TagNormalizer.Split("   "));
    }

    [Fact]
    public void Split_ParsesAndTrimsEntries()
    {
        var tags = TagNormalizer.Split("紧急, 线上 ,回归");

        Assert.Equal(new[] { "紧急", "线上", "回归" }, tags);
    }

    [Fact]
    public void Contains_MatchesCaseInsensitively()
    {
        Assert.True(TagNormalizer.Contains("Bug,线上", "bug"));
        Assert.True(TagNormalizer.Contains("Bug,线上", "线上"));
        Assert.False(TagNormalizer.Contains("Bug,线上", "回归"));
    }

    /// <summary>
    /// 回归防护：不得以子串方式误命中。
    /// </summary>
    /// <remarks>
    /// 这正是仓储层不能用 <c>LIKE '%tag%'</c> 的原因 ——
    /// 查 <c>bug</c> 会误命中 <c>debug</c>。
    /// </remarks>
    [Fact]
    public void Contains_DoesNotMatchSubstring()
    {
        Assert.False(TagNormalizer.Contains("debug", "bug"));
    }

    [Fact]
    public void Contains_ReturnsFalseForBlankQuery()
    {
        Assert.False(TagNormalizer.Contains("紧急", "   "));
    }

    /// <summary>
    /// 往返一致性：规范化后再解析，应得到等价的标签集合。
    /// </summary>
    [Fact]
    public void NormalizeAndSplit_AreRoundTripConsistent()
    {
        var normalized = TagNormalizer.Normalize(new[] { " 紧急 ", "线上", "紧急" });
        var parsed = TagNormalizer.Split(normalized);

        Assert.Equal(new[] { "紧急", "线上" }, parsed);
    }
}

/// <summary>
/// 覆盖任务创建工厂。
/// </summary>
/// <remarks>
/// 工厂是任务创建的唯一入口（Article 6），也是未来输入语法的接入点，
/// 其默认值与规范化行为需被锁定。
/// </remarks>
public class TaskItemFactoryTests
{
    private static readonly FakeClock Clock =
        new(new DateTime(2026, 3, 10, 8, 30, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_StampsCreatedAtFromClock()
    {
        var task = TaskItemFactory.Create(Clock, "写测试");

        Assert.Equal(Clock.UtcNow, task.CreatedAt);
    }

    [Fact]
    public void Create_TrimsTitle()
    {
        var task = TaskItemFactory.Create(Clock, "  写测试  ");

        Assert.Equal("写测试", task.Title);
    }

    /// <summary>
    /// 标题内部字符必须原样保留。
    /// </summary>
    /// <remarks>
    /// 未来的 <c>#项目</c> / <c>@标签</c> 输入语法依赖这些字符不被提前处理掉
    /// （design-domain-contract §3.2 预留结构）。
    /// </remarks>
    [Fact]
    public void Create_PreservesSpecialCharactersInTitle()
    {
        var task = TaskItemFactory.Create(Clock, "修 bug #FlowTask @紧急");

        Assert.Equal("修 bug #FlowTask @紧急", task.Title);
    }

    /// <summary>
    /// 零必填：只给标题即可创建，其余维度全部为空且完全正常。
    /// </summary>
    [Fact]
    public void Create_LeavesClassificationEmptyByDefault()
    {
        var task = TaskItemFactory.Create(Clock, "随手记");

        Assert.Null(task.ProjectId);
        Assert.Null(task.Tags);
        Assert.Null(task.DueDate);
        Assert.Null(task.Description);
        Assert.Equal(TaskPriority.Medium, task.Priority);
        Assert.False(task.IsCompleted);
        Assert.False(task.IsDeleted);
    }

    [Fact]
    public void Create_NormalizesTags()
    {
        var task = TaskItemFactory.Create(Clock, "带标签", tags: new[] { " Bug ", "bug", "线上" });

        Assert.Equal("Bug,线上", task.Tags);
    }

    [Fact]
    public void Create_AcceptsFullClassification()
    {
        var due = new DateTime(2026, 3, 15);
        var task = TaskItemFactory.Create(
            Clock,
            "完整任务",
            TaskPriority.High,
            projectId: "proj-1",
            tags: new[] { "线上" },
            dueDate: due,
            description: "备注");

        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal("proj-1", task.ProjectId);
        Assert.Equal("线上", task.Tags);
        Assert.Equal(due, task.DueDate);
        Assert.Equal("备注", task.Description);
    }

    [Fact]
    public void Create_GeneratesDistinctIds()
    {
        var a = TaskItemFactory.Create(Clock, "甲");
        var b = TaskItemFactory.Create(Clock, "乙");

        Assert.NotEqual(a.Id, b.Id);
    }
}

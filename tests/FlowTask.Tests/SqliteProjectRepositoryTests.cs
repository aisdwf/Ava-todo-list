using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖项目仓储，重点是删除项目时级联物理删除其下任务（R-2.7）。
/// </summary>
public class SqliteProjectRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteProjectRepository _projects;
    private readonly SqliteTaskRepository _tasks;

    public SqliteProjectRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_proj_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

        // 两个仓储必须指向同一文件：删除项目需在单事务内同时改动两张表
        _projects = new SqliteProjectRepository(_dbPath);
        _tasks = new SqliteTaskRepository(_clock, _dbPath);
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

    private Project NewProject(string name, int sortOrder = 0)
        => new() { Name = name, SortOrder = sortOrder, CreatedAt = _clock.UtcNow };

    private async Task<TaskItem> AddTaskAsync(string title, string? projectId)
    {
        var task = TaskItemFactory.Create(_clock, title, projectId: projectId);
        await _tasks.SaveTaskAsync(task);
        return task;
    }

    [Fact]
    public async Task SaveProject_InsertsAndRetrieves()
    {
        var project = NewProject("FlowTask");

        await _projects.SaveProjectAsync(project);
        var stored = await _projects.GetByIdAsync(project.Id);

        Assert.NotNull(stored);
        Assert.Equal("FlowTask", stored.Name);
        Assert.False(stored.IsArchived);
    }

    [Fact]
    public async Task SaveProject_UpdatesExistingInsteadOfDuplicating()
    {
        var project = NewProject("原名");
        await _projects.SaveProjectAsync(project);

        project.Name = "改名后";
        await _projects.SaveProjectAsync(project);

        var all = await _projects.GetAllProjectsAsync();
        Assert.Single(all);
        Assert.Equal("改名后", all[0].Name);
    }

    /// <summary>
    /// 重命名项目后，其下任务的归属自动跟随 —— 这正是项目值得作为独立实体的理由。
    /// </summary>
    [Fact]
    public async Task RenameProject_TasksFollowAutomatically()
    {
        var project = NewProject("旧名");
        await _projects.SaveProjectAsync(project);
        var task = await AddTaskAsync("某任务", project.Id);

        project.Name = "新名";
        await _projects.SaveProjectAsync(project);

        var storedTask = await _tasks.GetByIdAsync(task.Id);
        var storedProject = await _projects.GetByIdAsync(project.Id);

        // 任务只持有 Id，故无需任何批量更新
        Assert.Equal(project.Id, storedTask!.ProjectId);
        Assert.Equal("新名", storedProject!.Name);
    }

    [Fact]
    public async Task SaveProject_RejectsUnassignedCreatedAt()
    {
        var project = new Project { Name = "忘记时间戳" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _projects.SaveProjectAsync(project));

        Assert.Contains("CreatedAt", ex.Message);
    }

    [Fact]
    public async Task GetActiveProjects_ExcludesArchivedAndSortsByOrder()
    {
        await _projects.SaveProjectAsync(NewProject("第二", 2));
        await _projects.SaveProjectAsync(NewProject("第一", 1));
        var archived = NewProject("已归档", 0);
        await _projects.SaveProjectAsync(archived);
        await _projects.SetArchivedAsync(archived.Id, true);

        var active = await _projects.GetActiveProjectsAsync();

        Assert.Equal(2, active.Count);
        Assert.Equal("第一", active[0].Name);
        Assert.Equal("第二", active[1].Name);
    }

    /// <summary>
    /// 归档保留任务归属 —— 这是它与删除的核心区别。
    /// </summary>
    [Fact]
    public async Task Archive_PreservesTaskAssignment()
    {
        var project = NewProject("完结项目");
        await _projects.SaveProjectAsync(project);
        var task = await AddTaskAsync("历史任务", project.Id);

        await _projects.SetArchivedAsync(project.Id, true);

        var stored = await _tasks.GetByIdAsync(task.Id);
        Assert.Equal(project.Id, stored!.ProjectId);
    }

    [Fact]
    public async Task Archive_CanBeReverted()
    {
        var project = NewProject("项目");
        await _projects.SaveProjectAsync(project);

        await _projects.SetArchivedAsync(project.Id, true);
        Assert.Empty(await _projects.GetActiveProjectsAsync());

        await _projects.SetArchivedAsync(project.Id, false);
        Assert.Single(await _projects.GetActiveProjectsAsync());
    }

    /// <summary>
    /// <b>级联删除</b>：删除项目必须物理删除其下任务，而不是改挂 Default。
    /// </summary>
    [Fact]
    public async Task Delete_PhysicallyDeletesTasks()
    {
        await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);
        var project = NewProject("建错的项目");
        await _projects.SaveProjectAsync(project);
        var taskA = await AddTaskAsync("任务甲", project.Id);
        var taskB = await AddTaskAsync("任务乙", project.Id);

        var affected = await _projects.DeleteAsync(project.Id);

        Assert.Equal(2, affected);
        Assert.Null(await _projects.GetByIdAsync(project.Id));
        Assert.Null(await _tasks.GetByIdAsync(taskA.Id));
        Assert.Null(await _tasks.GetByIdAsync(taskB.Id));
    }

    /// <summary>
    /// 历史软删行也随项目物理清掉，不留指向已删项目的孤儿行。
    /// </summary>
    [Fact]
    public async Task Delete_AlsoRemovesSoftDeletedRows()
    {
        var project = NewProject("含软删");
        await _projects.SaveProjectAsync(project);
        var ghost = TaskItemFactory.Create(_clock, "旧软删", projectId: project.Id);
        ghost.IsDeleted = true;
        await _tasks.SaveTaskAsync(ghost);

        var affected = await _projects.DeleteAsync(project.Id);

        Assert.Equal(1, affected);
        Assert.Null(await _tasks.GetByIdAsync(ghost.Id));
    }

    /// <summary>
    /// 删除某项目不得影响其他项目的任务。
    /// </summary>
    [Fact]
    public async Task Delete_DoesNotAffectOtherProjectsTasks()
    {
        var target = NewProject("待删除");
        var other = NewProject("保留");
        await _projects.SaveProjectAsync(target);
        await _projects.SaveProjectAsync(other);

        await AddTaskAsync("将被一并删除", target.Id);
        var kept = await AddTaskAsync("归属不变", other.Id);

        await _projects.DeleteAsync(target.Id);

        var storedKept = await _tasks.GetByIdAsync(kept.Id);
        Assert.Equal(other.Id, storedKept!.ProjectId);
        Assert.NotNull(await _projects.GetByIdAsync(other.Id));
    }

    [Fact]
    public async Task Delete_OnProjectWithoutTasks_ReportsZeroAffected()
    {
        var project = NewProject("空项目");
        await _projects.SaveProjectAsync(project);

        var affected = await _projects.DeleteAsync(project.Id);

        Assert.Equal(0, affected);
        Assert.Null(await _projects.GetByIdAsync(project.Id));
    }

    /// <summary>
    /// Default 不可删除：仓储必须拒绝，而不是让异常在无人捕捉的命令路径上炸掉进程。
    /// </summary>
    [Fact]
    public async Task Delete_ThrowsForDefaultProject()
    {
        await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _projects.DeleteAsync(DefaultProject.Id));

        Assert.NotNull(await _projects.GetByIdAsync(DefaultProject.Id));
    }

    [Fact]
    public async Task CountTasks_ReflectsAssignedUndeletedTasks()
    {
        var project = NewProject("统计用");
        await _projects.SaveProjectAsync(project);

        await AddTaskAsync("甲", project.Id);
        var toDelete = await AddTaskAsync("乙", project.Id);
        await AddTaskAsync("无归属", null);

        Assert.Equal(2, await _projects.CountTasksAsync(project.Id));

        await _tasks.PermanentDeleteAsync(toDelete.Id);

        // 物理删除的任务不计入影响提示
        Assert.Equal(1, await _projects.CountTasksAsync(project.Id));
    }

    [Fact]
    public async Task GetTasksByProject_ReturnsOnlyThatProject()
    {
        var a = NewProject("甲项目");
        var b = NewProject("乙项目");
        await _projects.SaveProjectAsync(a);
        await _projects.SaveProjectAsync(b);

        await AddTaskAsync("属甲", a.Id);
        await AddTaskAsync("属乙", b.Id);

        var inA = await _tasks.GetTasksByProjectAsync(a.Id);

        Assert.Single(inA);
        Assert.Equal("属甲", inA[0].Title);
    }

    /// <summary>
    /// 传 <c>null</c> 时返回未归属任务，而非全部任务。
    /// </summary>
    /// <remarks>
    /// 「未归属」本身是一个有意义的筛选维度，且依零必填原则是默认状态，
    /// 若把它解释为「不过滤」，用户就无从查看这批任务。
    /// </remarks>
    [Fact]
    public async Task GetTasksByProject_WithNull_ReturnsUnassignedOnly()
    {
        var project = NewProject("有归属");
        await _projects.SaveProjectAsync(project);

        await AddTaskAsync("有归属的", project.Id);
        await AddTaskAsync("没归属的", null);

        var unassigned = await _tasks.GetTasksByProjectAsync(null);

        Assert.Single(unassigned);
        Assert.Equal("没归属的", unassigned[0].Title);
    }

    /// <summary>
    /// 种子 Default 不得顺带改写 <c>ProjectId IS NULL</c>：那是启动迁移，不是每次打开小窗。
    /// </summary>
    [Fact]
    public async Task EnsureDefaultProject_DoesNotMigrateNullTasks()
    {
        var task = await AddTaskAsync("历史未归属", null);

        await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);

        Assert.NotNull(await _projects.GetByIdAsync(DefaultProject.Id));
        Assert.Null((await _tasks.GetByIdAsync(task.Id))!.ProjectId);
    }

    [Fact]
    public async Task MigrateNullProjectIdsToDefault_AssignsDefault()
    {
        var task = await AddTaskAsync("历史未归属", null);
        await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);

        await _projects.MigrateNullProjectIdsToDefaultAsync();

        Assert.Equal(DefaultProject.Id, (await _tasks.GetByIdAsync(task.Id))!.ProjectId);
    }

    [Fact]
    public async Task EnsureDefaultProject_ConcurrentInserts_DoNotThrowAndKeepSingleRow()
    {
        var other = new SqliteProjectRepository(_dbPath);
        var t1 = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

        await Task.WhenAll(
            _projects.EnsureDefaultProjectAsync(t1),
            other.EnsureDefaultProjectAsync(t2));

        var all = await _projects.GetAllProjectsAsync();
        Assert.Single(all);
        Assert.Equal(DefaultProject.Id, all[0].Id);
    }

    [Fact]
    public async Task EnsureDefaultProject_SecondCall_DoesNotOverwriteCreatedAt()
    {
        var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await _projects.EnsureDefaultProjectAsync(first);
        await _projects.EnsureDefaultProjectAsync(second);

        var stored = await _projects.GetByIdAsync(DefaultProject.Id);
        Assert.Equal(first, stored!.CreatedAt);
    }

    [Fact]
    public async Task SaveProject_RejectsDuplicateNameIncludingArchived()
    {
        var live = NewProject("工作");
        await _projects.SaveProjectAsync(live);
        await _projects.SetArchivedAsync(live.Id, true);

        var clash = NewProject("工作");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _projects.SaveProjectAsync(clash));
        Assert.Contains("已被使用", ex.Message, StringComparison.Ordinal);
        Assert.True(await _projects.NameIsTakenAsync("工作", exceptId: null));
        Assert.False(await _projects.NameIsTakenAsync("工作", exceptId: live.Id));
    }

    [Fact]
    public async Task NextSortOrder_UsesMaxPlusOneAfterArchive()
    {
        await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);
        var first = NewProject("甲", sortOrder: 1);
        await _projects.SaveProjectAsync(first);
        await _projects.SetArchivedAsync(first.Id, true);

        Assert.Equal(2, await _projects.NextSortOrderAsync());
    }
}

using Avalonia.Headless.XUnit;
using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖快捷小窗视图模型的单项目列表、勾选与「记忆上次项目」行为
/// （spec-quick-window-single-project-list）。
/// </summary>
/// <remarks>
/// 使用 <c>[AvaloniaFact]</c>：与 <see cref="MainViewModelTests"/> 同理，
/// 构造/操作 <see cref="ProjectItemViewModel"/> 等 <c>ObservableObject</c> 触达 Avalonia 属性系统。
/// </remarks>
public class QuickCaptureViewModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteTaskRepository _taskRepo;
    private readonly SqliteProjectRepository _projectRepo;
    private readonly SqliteAppSettingsRepository _settingsRepo;

    public QuickCaptureViewModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_qc_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _taskRepo = new SqliteTaskRepository(_clock, _dbPath);
        _projectRepo = new SqliteProjectRepository(_dbPath);
        _settingsRepo = new SqliteAppSettingsRepository(_dbPath);
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
                // 测试清理阶段的文件锁不影响断言结果
            }
        }
    }

    private QuickCaptureViewModel CreateViewModel()
        => new(_taskRepo, _projectRepo, _settingsRepo, _clock);

    [AvaloniaFact]
    public async Task PrepareAsync_DefaultsToDefaultProjectWhenNoMemoryExists()
    {
        var vm = CreateViewModel();
        await vm.PrepareAsync();

        Assert.NotNull(vm.SelectedProject);
        Assert.Equal(DefaultProject.Id, vm.SelectedProject!.Id);
        Assert.Contains(vm.Projects, p => p.Id == DefaultProject.Id);
    }

    [AvaloniaFact]
    public async Task PrepareAsync_LoadsTasksForRestoredProject()
    {
        var project = new Project { Name = "工作", CreatedAt = _clock.UtcNow };
        await _projectRepo.SaveProjectAsync(project);

        var task = TaskItemFactory.Create(_clock, "写周报", projectId: project.Id);
        await _taskRepo.SaveTaskAsync(task);

        await _settingsRepo.SetAsync("QuickWindow.LastProjectId", project.Id);

        var vm = CreateViewModel();
        await vm.PrepareAsync();

        Assert.Equal(project.Id, vm.SelectedProject!.Id);
        Assert.Single(vm.Tasks);
        Assert.Equal("写周报", vm.Tasks[0].Task.Title);
    }

    [AvaloniaFact]
    public async Task SelectedProjectChanged_ReloadsTasksAndPersistsMemory()
    {
        var projectA = new Project { Name = "项目甲", CreatedAt = _clock.UtcNow };
        var projectB = new Project { Name = "项目乙", CreatedAt = _clock.UtcNow };
        await _projectRepo.SaveProjectAsync(projectA);
        await _projectRepo.SaveProjectAsync(projectB);

        await _taskRepo.SaveTaskAsync(TaskItemFactory.Create(_clock, "甲的任务", projectId: projectA.Id));
        await _taskRepo.SaveTaskAsync(TaskItemFactory.Create(_clock, "乙的任务", projectId: projectB.Id));

        var vm = CreateViewModel();
        await vm.PrepareAsync();

        var targetB = vm.Projects.First(p => p.Id == projectB.Id);
        vm.SelectedProject = targetB;

        // 属性变更回调以 fire-and-forget 启动 ChangeSelectedProjectCommand，
        // 显式再等待一次同一命令以取得确定状态（与主窗 LoadTasksCommand 测试模式一致）
        await vm.ChangeSelectedProjectCommand.ExecuteAsync(targetB);

        Assert.Single(vm.Tasks);
        Assert.Equal("乙的任务", vm.Tasks[0].Task.Title);

        var persisted = await _settingsRepo.GetAsync("QuickWindow.LastProjectId");
        Assert.Equal(projectB.Id, persisted);
    }

    [AvaloniaFact]
    public async Task ToggleTaskComplete_KeepsTaskVisibleAndStrikesThrough()
    {
        var task = TaskItemFactory.Create(_clock, "打卡", projectId: DefaultProject.Id);
        await _taskRepo.SaveTaskAsync(task);

        var vm = CreateViewModel();
        await vm.PrepareAsync();

        Assert.Single(vm.Tasks);
        var row = vm.Tasks[0];
        row.Task.IsCompleted = true;

        await vm.ToggleTaskCompleteCommand.ExecuteAsync(row.Task);

        // 勾选后任务仍在小窗列表中（完成套件：不离开列表）
        Assert.Single(vm.Tasks);
        Assert.True(vm.Tasks[0].Task.IsCompleted);
    }

    [AvaloniaFact]
    public async Task SaveAsync_RefreshesListWhenNewTaskMatchesSelectedProject()
    {
        var vm = CreateViewModel();
        await vm.PrepareAsync();

        vm.InputText = "买菜";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(vm.Tasks);
        Assert.Equal("买菜", vm.Tasks[0].Task.Title);
    }

    [AvaloniaFact]
    public async Task PrepareAsync_FallsBackToDefaultWhenLastProjectWasDeleted()
    {
        var project = new Project { Name = "已删", CreatedAt = _clock.UtcNow };
        await _projectRepo.SaveProjectAsync(project);
        await _taskRepo.SaveTaskAsync(TaskItemFactory.Create(_clock, "随项目走", projectId: project.Id));
        await _settingsRepo.SetAsync("QuickWindow.LastProjectId", project.Id);

        await _projectRepo.DeleteAsync(project.Id);

        var vm = CreateViewModel();
        await vm.PrepareAsync();

        Assert.Equal(DefaultProject.Id, vm.SelectedProject!.Id);
        Assert.DoesNotContain(vm.Projects, p => p.Id == project.Id);
        Assert.Empty(vm.Tasks);
    }

    /// <summary>
    /// 打开小窗只确保 Default 存在，不得把历史 null 归属改写成 Default。
    /// </summary>
    [AvaloniaFact]
    public async Task PrepareAsync_DoesNotMigrateNullProjectIds()
    {
        var orphan = TaskItemFactory.Create(_clock, "历史未归属");
        await _taskRepo.SaveTaskAsync(orphan);

        var vm = CreateViewModel();
        await vm.PrepareAsync();

        Assert.Null((await _taskRepo.GetByIdAsync(orphan.Id))!.ProjectId);
    }

    /// <summary>
    /// R-1.9：新建归属跟随下拉，不再回落 Default；保存后留在小窗、可连续录入（D4/D5）。
    /// </summary>
    [AvaloniaFact]
    public async Task SaveAsync_AssignsSelectedProjectAndKeepsWindowOpen()
    {
        var project = new Project { Name = "工作", CreatedAt = _clock.UtcNow };
        await _projectRepo.SaveProjectAsync(project);
        await _settingsRepo.SetAsync("QuickWindow.LastProjectId", project.Id);

        var vm = CreateViewModel();
        await vm.PrepareAsync();
        var closeRequested = false;
        vm.RequestClose += () => closeRequested = true;

        vm.InputText = "写周报";
        await vm.SaveCommand.ExecuteAsync(null);
        vm.InputText = "开例会";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(closeRequested);
        Assert.Equal(string.Empty, vm.InputText);
        Assert.Equal(2, vm.Tasks.Count);
        var stored = await _taskRepo.GetTasksByProjectAsync(project.Id);
        Assert.Equal(2, stored.Count);
    }

    /// <summary>
    /// <c>@</c> 语法已移除（D3）：原样保留在标题里，不解析、不新建项目。
    /// </summary>
    [AvaloniaFact]
    public async Task SaveAsync_KeepsAtSignInTitleWithoutCreatingProject()
    {
        var vm = CreateViewModel();
        await vm.PrepareAsync();
        var projectCountBefore = (await _projectRepo.GetAllProjectsAsync()).Count;

        vm.InputText = "x @某项目";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("x @某项目", Assert.Single(vm.Tasks).Task.Title);
        Assert.Equal(projectCountBefore, (await _projectRepo.GetAllProjectsAsync()).Count);
    }

    /// <summary>
    /// D6：Ctrl+Tab / Ctrl+Shift+Tab 首尾循环切换，列表与记忆项同步。
    /// </summary>
    [AvaloniaFact]
    public async Task SelectNextAndPreviousProject_CycleAndReloadTasks()
    {
        var projectA = new Project { Name = "项目甲", SortOrder = 1, CreatedAt = _clock.UtcNow };
        await _projectRepo.SaveProjectAsync(projectA);
        await _taskRepo.SaveTaskAsync(TaskItemFactory.Create(_clock, "甲的任务", projectId: projectA.Id));

        var vm = CreateViewModel();
        await vm.PrepareAsync();
        Assert.Equal(2, vm.Projects.Count);
        var first = vm.Projects[0];
        var last = vm.Projects[^1];
        vm.SelectedProject = first;
        await vm.ChangeSelectedProjectCommand.ExecuteAsync(first);

        await vm.SelectNextProjectCommand.ExecuteAsync(null);
        Assert.Same(last, vm.SelectedProject);
        Assert.Equal(last.Id, await _settingsRepo.GetAsync("QuickWindow.LastProjectId"));

        await vm.SelectNextProjectCommand.ExecuteAsync(null);
        Assert.Same(first, vm.SelectedProject);

        await vm.SelectPreviousProjectCommand.ExecuteAsync(null);
        Assert.Same(last, vm.SelectedProject);
        Assert.Equal(
            last.Id == projectA.Id ? ["甲的任务"] : Array.Empty<string>(),
            vm.Tasks.Select(t => t.Task.Title).ToArray());
    }

    /// <summary>
    /// R-1.10：底栏选择器的日期随新任务落库，保存后清空，下一条默认无到期日。
    /// </summary>
    [AvaloniaFact]
    public async Task Save_WritesSelectedDueDateThenResets()
    {
        var vm = CreateViewModel();
        await vm.PrepareAsync();

        vm.NewDueDateEditor.Load(null);
        vm.NewDueDateEditor.CommitDate(new DateTime(2026, 3, 12));
        vm.InputText = "带日期";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(vm.NewTaskDueDate);
        var saved = vm.Tasks.Single(t => t.Task.Title == "带日期");
        Assert.Equal(new DateTime(2026, 3, 12), (await _taskRepo.GetByIdAsync(saved.Task.Id))!.DueDate);

        vm.InputText = "无日期";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(vm.Tasks.Single(t => t.Task.Title == "无日期").Task.DueDate);
    }

    /// <summary>
    /// 回车保存后优先级保持，便于连续录入同档任务；Esc 关闭才回到 P2。
    /// </summary>
    [AvaloniaFact]
    public async Task Save_KeepsPriority_CancelResetsIt()
    {
        var vm = CreateViewModel();
        await vm.PrepareAsync();

        vm.Priority = FlowTask.Core.Enums.TaskPriority.High;
        vm.InputText = "第一条";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(FlowTask.Core.Enums.TaskPriority.High, vm.Priority);

        vm.InputText = "第二条";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(
            FlowTask.Core.Enums.TaskPriority.High,
            vm.Tasks.Single(t => t.Task.Title == "第二条").Task.Priority);

        vm.CancelCommand.Execute(null);
        Assert.Equal(FlowTask.Core.Enums.TaskPriority.Medium, vm.Priority);
    }

    /// <summary>
    /// 小窗「默认 +N 天」读取主窗保存的偏移设置，而不是写死 1。
    /// </summary>
    [AvaloniaFact]
    public async Task DefaultPreset_UsesPersistedOffset()
    {
        await _settingsRepo.SetDefaultDueOffsetDaysAsync(5);
        var vm = CreateViewModel();
        await vm.PrepareAsync();

        vm.NewDueDateEditor.Load(null);
        vm.NewDueDateEditor.ApplyDefaultDueCommand.Execute(null);

        Assert.Equal(new DateTime(2026, 3, 10).AddDays(5).Date, vm.NewTaskDueDate!.Value.Date);
    }

    /// <summary>
    /// R-1.10：小窗行内改期即持久化，行展示值同步。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitRowDueDate_PersistsAndRefreshesRow()
    {
        var task = TaskItemFactory.Create(_clock, "改期", projectId: DefaultProject.Id);
        await _taskRepo.SaveTaskAsync(task);
        var vm = CreateViewModel();
        await vm.PrepareAsync();

        await vm.CommitRowDueDateCommand.ExecuteAsync(new DueDateCommit(vm.Tasks[0], new DateTime(2026, 3, 11)));

        Assert.Equal(new DateTime(2026, 3, 11), (await _taskRepo.GetByIdAsync(task.Id))!.DueDate);
        Assert.Equal(new DateTime(2026, 3, 11), vm.Tasks[0].DueDate);
    }
}

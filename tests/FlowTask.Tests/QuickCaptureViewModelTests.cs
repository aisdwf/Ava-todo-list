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

    [AvaloniaFact]
    public async Task SaveAsync_AtMentionMatchesArchivedProjectInsteadOfCreatingDuplicate()
    {
        var archived = new Project { Name = "旧项", CreatedAt = _clock.UtcNow };
        await _projectRepo.SaveProjectAsync(archived);
        await _projectRepo.SetArchivedAsync(archived.Id, true);

        var vm = CreateViewModel();
        await vm.PrepareAsync();
        Assert.DoesNotContain(vm.Projects, p => p.Id == archived.Id);

        vm.InputText = "续上 @旧项";
        await vm.SaveCommand.ExecuteAsync(null);

        var stored = (await _taskRepo.GetAllActiveTasksAsync()).Single(t => t.Title == "续上");
        Assert.Equal(archived.Id, stored.ProjectId);
        Assert.Single(await _projectRepo.GetAllProjectsAsync(), p => p.Name == "旧项");
    }
}

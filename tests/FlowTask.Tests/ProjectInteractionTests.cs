using Avalonia.Headless.XUnit;
using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖项目管理与项目筛选的正交性。
/// </summary>
/// <remarks>
/// 重点防护两类风险：
/// <list type="number">
///   <item><description>
///   项目筛选与 VIEWS 筛选耦合 —— `TaskFilter` 枚举此前曾混入「设置页」
///   导致视图模式与数据筛选纠缠（spec-editorial-and-ripple-theme 已修正），此处不得重犯。
///   </description></item>
///   <item><description>
///   删除项目误删任务 —— 任务是核心资产，绝不能随项目消失。
///   </description></item>
/// </list>
/// </remarks>
public class ProjectInteractionTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteTaskRepository _repo;
    private readonly SqliteProjectRepository _projectRepo;

    public ProjectInteractionTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_projvm_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _repo = new SqliteTaskRepository(_clock, _dbPath);
        _projectRepo = new SqliteProjectRepository(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* 清理锁不影响断言 */ }
        }
    }

    private MainViewModel CreateViewModel()
    {
        var settingsRepo = new SqliteAppSettingsRepository(_dbPath);
        return new(_repo, _projectRepo, _clock, settingsRepo);
    }

    private async Task<MainViewModel> CreateInitializedAsync()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        return vm;
    }


    /// <summary>侧边栏中除 Default 外的用户项目（Default 为系统种子，恒存在）。</summary>
    private static IEnumerable<ProjectItemViewModel> UserProjects(MainViewModel vm)
        => vm.Projects.Where(p => p.Id != DefaultProject.Id);

    private static ProjectItemViewModel SoleUserProject(MainViewModel vm)
        => Assert.Single(UserProjects(vm));

    // ==================== 零分类体验 ====================

    /// <summary>
    /// 初始化后仅有系统 Default 项目（取代「未归属」）（R-2.6）。
    /// </summary>
    [AvaloniaFact]
    public async Task AfterInit_OnlyDefaultProjectExists()
    {
        var vm = await CreateInitializedAsync();

        Assert.True(vm.HasProjects);
        Assert.Single(vm.Projects);
        Assert.Equal(DefaultProject.Id, vm.Projects[0].Id);
        Assert.Empty(UserProjects(vm));
    }

    [AvaloniaFact]
    public async Task CreateProject_AppearsAlongsideDefault()
    {
        var vm = await CreateInitializedAsync();

        vm.NewProjectName = "FlowTask";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.True(vm.HasProjects);
        Assert.Equal(2, vm.Projects.Count);
        Assert.Equal("FlowTask", SoleUserProject(vm).Name);
    }

    // ==================== 创建 ====================

    [AvaloniaFact]
    public async Task CreateProject_IgnoresBlankName()
    {
        var vm = await CreateInitializedAsync();

        vm.NewProjectName = "   ";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.Empty(UserProjects(vm));
    }

    [AvaloniaFact]
    public async Task CreateProject_IgnoresOverlongName()
    {
        var vm = await CreateInitializedAsync();

        vm.NewProjectName = new string('x', ProjectName.MaxLength + 1);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.Empty(UserProjects(vm));
    }

    [AvaloniaFact]
    public async Task CreateProject_ClearsInputAndCollapsesForm()
    {
        var vm = await CreateInitializedAsync();
        vm.ToggleCreateProjectCommand.Execute(null);
        Assert.True(vm.IsCreatingProject);

        vm.NewProjectName = "新项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.Empty(vm.NewProjectName);
        Assert.False(vm.IsCreatingProject);
    }

    /// <summary>
    /// 相邻新建的项目应获得不同颜色，否则色条失去区分作用。
    /// </summary>
    [AvaloniaFact]
    public async Task CreateProject_AssignsDistinctColorsToConsecutiveProjects()
    {
        var vm = await CreateInitializedAsync();

        vm.NewProjectName = "甲";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        vm.NewProjectName = "乙";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        var users = UserProjects(vm).ToList();
        Assert.Equal(2, users.Count);
        Assert.NotEqual(users[0].ColorHex, users[1].ColorHex);
    }

    // ==================== 筛选正交性 ====================

    /// <summary>
    /// 选中项目须解除 VIEWS 三个单选的高亮。
    /// </summary>
    [AvaloniaFact]
    public async Task SelectProject_ClearsViewsFilterHighlight()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.True(vm.IsActiveFilterSelected);

        await vm.SelectProjectCommand.ExecuteAsync(SoleUserProject(vm));

        Assert.False(vm.IsActiveFilterSelected);
        Assert.False(vm.IsCompletedFilterSelected);
        Assert.NotNull(vm.SelectedProject);
    }

    /// <summary>
    /// 反向：切回 VIEWS 筛选须解除项目选中。
    /// </summary>
    [AvaloniaFact]
    public async Task ChangeFilter_ClearsProjectSelection()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        await vm.SelectProjectCommand.ExecuteAsync(SoleUserProject(vm));
        Assert.NotNull(vm.SelectedProject);

        vm.ChangeFilterCommand.Execute(TaskFilter.Completed);

        Assert.Null(vm.SelectedProject);
    }

    // ==================== 结构整改回归防护 (spec-sidebar-selection-consolidation) ====================

    /// <summary>
    /// 从项目 A 切换到项目 B：<c>CurrentSelection.Kind</c> 不变（均为 <c>Project</c>），
    /// 只有 <c>ProjectId</c> 变化。这正是本次整改要根除的缺陷类的镜像场景 ——
    /// 旧实现里「同一维度内换值」最容易被误判为「无需同步」，
    /// 而 <see cref="ViewSelection"/> 的记录类型相等性保证了此处仍会被判定为值变化。
    /// </summary>
    [AvaloniaFact]
    public async Task SelectProject_SwitchingBetweenTwoProjects_MovesHighlightCorrectly()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "甲";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        vm.NewProjectName = "乙";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var users = UserProjects(vm).ToList();
        var projectA = users.Single(x => x.Name == "甲");
        var projectB = users.Single(x => x.Name == "乙");

        await vm.SelectProjectCommand.ExecuteAsync(projectA);
        Assert.True(projectA.IsSelected);
        Assert.False(projectB.IsSelected);

        await vm.SelectProjectCommand.ExecuteAsync(projectB);

        Assert.False(projectA.IsSelected);
        Assert.True(projectB.IsSelected);
        Assert.Equal(projectB.Id, vm.SelectedProject!.Id);
    }

    /// <summary>
    /// 「赋同值」场景的直接回归防护：重复选中同一个已选中的项目，
    /// 高亮与筛选结果须保持不变（幂等），不能因为值未变化而丢失状态。
    /// </summary>
    [AvaloniaFact]
    public async Task SelectProject_SelectingSameProjectTwice_StaysHighlighted()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        await vm.SelectProjectCommand.ExecuteAsync(project);
        await vm.SelectProjectCommand.ExecuteAsync(project);

        Assert.True(project.IsSelected);
        Assert.Equal(project.Id, vm.SelectedProject!.Id);
    }

    /// <summary>
    /// 「赋同值」场景：在设置页时点击当前已选中的 VIEWS 导航项 ——
    /// 目标筛选与当前值相同，仍须离开设置页（此为
    /// spec-editorial-and-ripple-theme 教训 1 的直接场景，此处在新结构下复验）。
    /// </summary>
    [AvaloniaFact]
    public void ChangeFilter_ToSameValue_StillLeavesSettingsView()
    {
        var vm = CreateViewModel();
        Assert.Equal(TaskFilter.Active, vm.CurrentFilter);

        vm.ToggleSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);

        vm.ChangeFilterCommand.Execute(TaskFilter.Active);

        Assert.False(vm.IsSettingsOpen);
        Assert.True(vm.IsActiveFilterSelected);
    }

    /// <summary>
    /// 不变量：任一时刻，VIEWS 三个高亮标志与「是否选中了某个项目」互斥 ——
    /// 有且只有一个为真。以此防止「全部未选中」或「同时多个选中」的空档状态
    /// （spec-classification-ui 的实际缺陷正是前者）。
    /// </summary>
    [AvaloniaFact]
    public async Task ExactlyOneSelectionIsActive_AcrossAllTransitions()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        AssertExactlyOneActive(vm); // 初始：全部任务

        vm.ChangeFilterCommand.Execute(TaskFilter.Completed);
        AssertExactlyOneActive(vm);

        await vm.SelectProjectCommand.ExecuteAsync(project);
        AssertExactlyOneActive(vm);

        await vm.SelectProjectCommand.ExecuteAsync(null);
        AssertExactlyOneActive(vm);

        static void AssertExactlyOneActive(MainViewModel vm)
        {
            var activeCount = new[]
            {
                vm.IsActiveFilterSelected,
                vm.IsCompletedFilterSelected,
                vm.SelectedProject is not null
            }.Count(flag => flag);

            Assert.Equal(1, activeCount);
        }
    }

    /// <summary>
    /// 选中某项目后新建任务，应直接归属该项目，不落入未分类（用户原话：
    /// 「选中某个project时，应该直接在对应project创建，而不是创建到未分类」）。
    /// </summary>
    [AvaloniaFact]
    public async Task AddTask_WhileProjectSelected_InheritsSelectedProjectId()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "甲项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        await vm.SelectProjectCommand.ExecuteAsync(project);

        vm.NewTaskTitle = "在甲项目下新建";
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Single(vm.Tasks);
        Assert.Equal(project.Id, vm.Tasks[0].Task.ProjectId);
    }

    /// <summary>
    /// 未选中具体项目（全部任务视图）时新建任务，行为保持不变：不推断归属。
    /// </summary>
    [AvaloniaFact]
    public async Task AddTask_WithoutProjectSelected_StaysUnassigned()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "甲项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        vm.NewTaskTitle = "未选中项目时新建";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var created = Assert.Single(vm.Tasks, t => t.Task.Title == "未选中项目时新建");
        Assert.Null(created.Task.ProjectId);
    }

    /// <summary>
    /// 项目筛选只返回该项目下的任务。
    /// </summary>
    [AvaloniaFact]
    public async Task SelectProject_ShowsOnlyThatProjectsTasks()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "甲项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        vm.NewTaskTitle = "属于甲";
        await vm.AddTaskCommand.ExecuteAsync(null);
        await vm.AssignProjectAsync(vm.Tasks[0].Task, project.Id);

        vm.NewTaskTitle = "无归属";
        await vm.AddTaskCommand.ExecuteAsync(null);

        await vm.SelectProjectCommand.ExecuteAsync(project);

        Assert.Single(vm.Tasks);
        Assert.Equal("属于甲", vm.Tasks[0].Task.Title);
    }

    [AvaloniaFact]
    public async Task SelectProject_UpdatesHeroTitleToProjectName()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "我的项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        await vm.SelectProjectCommand.ExecuteAsync(SoleUserProject(vm));

        Assert.Equal("我的项目", vm.CurrentCategoryTitle);
    }

    [AvaloniaFact]
    public async Task SelectProject_WithNull_ReturnsToActiveView()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        await vm.SelectProjectCommand.ExecuteAsync(SoleUserProject(vm));

        await vm.SelectProjectCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedProject);
        Assert.Equal(TaskFilter.Active, vm.CurrentFilter);
    }

    /// <summary>
    /// 选中项目时点击设置页再返回，不应残留设置视图。
    /// </summary>
    [AvaloniaFact]
    public async Task SelectProject_LeavesSettingsView()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);

        await vm.SelectProjectCommand.ExecuteAsync(SoleUserProject(vm));

        Assert.False(vm.IsSettingsOpen);
    }

    // ==================== 计数 ====================

    [AvaloniaFact]
    public async Task ProjectTaskCount_ReflectsAssignedTasks()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "计数项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var projectId = SoleUserProject(vm).Id;

        Assert.Equal(0, SoleUserProject(vm).TaskCount);

        vm.NewTaskTitle = "任务甲";
        await vm.AddTaskCommand.ExecuteAsync(null);
        await vm.AssignProjectAsync(vm.Tasks[0].Task, projectId);

        // 计数须在同一 vm 实例上原地刷新，无需重新初始化即可反映最新任务归属
        Assert.Equal(1, SoleUserProject(vm).TaskCount);
    }

    /// <summary>
    /// 删除任务同样须原地刷新项目行计数，而非停留在删除前的旧值。
    /// </summary>
    [AvaloniaFact]
    public async Task ProjectTaskCount_ReflectsDeletedTask()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "计数项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var projectId = SoleUserProject(vm).Id;

        vm.NewTaskTitle = "任务甲";
        await vm.AddTaskCommand.ExecuteAsync(null);
        await vm.AssignProjectAsync(vm.Tasks[0].Task, projectId);
        Assert.Equal(1, SoleUserProject(vm).TaskCount);

        await vm.DeleteTaskCommand.ExecuteAsync(vm.Tasks[0].Task);

        Assert.Equal(0, SoleUserProject(vm).TaskCount);
    }

    // ==================== 重命名 ====================

    [AvaloniaFact]
    public async Task RenameProject_PersistsNewName()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "原名";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        vm.BeginRenameProjectCommand.Execute(project);
        Assert.True(project.IsRenaming);
        Assert.Equal("原名", project.RenameBuffer);

        project.RenameBuffer = "新名";
        await vm.CommitRenameProjectCommand.ExecuteAsync(project);

        Assert.False(project.IsRenaming);
        Assert.Equal("新名", project.Name);
        Assert.Equal("新名", (await _projectRepo.GetByIdAsync(project.Id))!.Name);
    }

    /// <summary>
    /// 重命名为非法值时放弃修改并保留原名。
    /// </summary>
    [AvaloniaFact]
    public async Task RenameProject_RevertsOnInvalidName()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "原名";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        vm.BeginRenameProjectCommand.Execute(project);
        project.RenameBuffer = "   ";
        await vm.CommitRenameProjectCommand.ExecuteAsync(project);

        Assert.False(project.IsRenaming);
        Assert.Equal("原名", project.Name);
    }

    /// <summary>
    /// 取消重命名须丢弃输入缓冲，不影响原名。
    /// </summary>
    [AvaloniaFact]
    public async Task CancelRename_DiscardsBuffer()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "原名";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        vm.BeginRenameProjectCommand.Execute(project);
        project.RenameBuffer = "改到一半";
        vm.CancelRenameProjectCommand.Execute(project);

        Assert.False(project.IsRenaming);
        Assert.Equal("原名", project.Name);
        Assert.Empty(project.RenameBuffer);
    }

    /// <summary>
    /// 重命名当前选中的项目须同步标题区。
    /// </summary>
    [AvaloniaFact]
    public async Task RenameSelectedProject_UpdatesHeroTitle()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "原名";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);
        await vm.SelectProjectCommand.ExecuteAsync(project);

        vm.BeginRenameProjectCommand.Execute(project);
        project.RenameBuffer = "改名后";
        await vm.CommitRenameProjectCommand.ExecuteAsync(project);

        Assert.Equal("改名后", vm.CurrentCategoryTitle);
    }

    // ==================== 归档 ====================

    /// <summary>
    /// 归档后项目从侧边栏隐去，但任务归属保留。
    /// </summary>
    [AvaloniaFact]
    public async Task ArchiveProject_HidesFromSidebarButKeepsTaskAssignment()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "待归档";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        vm.NewTaskTitle = "归档项目下的任务";
        await vm.AddTaskCommand.ExecuteAsync(null);
        var taskId = vm.Tasks[0].Task.Id;
        await vm.AssignProjectAsync(vm.Tasks[0].Task, project.Id);

        await vm.ArchiveProjectCommand.ExecuteAsync(project);

        Assert.Empty(UserProjects(vm));
        Assert.True(vm.HasProjects); // Default 仍在

        // 归档与删除的核心区别：归属不变
        var stored = await _repo.GetByIdAsync(taskId);
        Assert.Equal(project.Id, stored!.ProjectId);
    }

    // ==================== 删除 ====================

    /// <summary>
    /// 删除前须先报告影响的任务条数。
    /// </summary>
    [AvaloniaFact]
    public async Task RequestDeleteProject_ReportsAffectedTaskCount()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "待删除";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        vm.NewTaskTitle = "任务甲";
        await vm.AddTaskCommand.ExecuteAsync(null);
        await vm.AssignProjectAsync(vm.Tasks[0].Task, project.Id);
        vm.NewTaskTitle = "任务乙";
        await vm.AddTaskCommand.ExecuteAsync(null);
        await vm.AssignProjectAsync(vm.Tasks.First(t => t.Task.Title == "任务乙").Task, project.Id);

        await vm.RequestDeleteProjectCommand.ExecuteAsync(project);

        Assert.NotNull(vm.ProjectPendingDeletion);
        Assert.Equal(2, vm.ProjectPendingDeletionTaskCount);
    }

    [AvaloniaFact]
    public async Task CancelDeleteProject_ClearsPendingState()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);

        await vm.RequestDeleteProjectCommand.ExecuteAsync(SoleUserProject(vm));
        vm.CancelDeleteProjectCommand.Execute(null);

        Assert.Null(vm.ProjectPendingDeletion);
        Assert.Equal(2, vm.Projects.Count); // Default + user
        Assert.Single(UserProjects(vm));
    }

    /// <summary>
    /// <b>核心保全语义</b>：删除项目后任务仍在，仅退回未归属。
    /// </summary>
    [AvaloniaFact]
    public async Task ConfirmDeleteProject_KeepsTasksAndClearsAssignment()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "建错的项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);

        vm.NewTaskTitle = "不该丢失的任务";
        await vm.AddTaskCommand.ExecuteAsync(null);
        var taskId = vm.Tasks[0].Task.Id;
        await vm.AssignProjectAsync(vm.Tasks[0].Task, project.Id);

        await vm.RequestDeleteProjectCommand.ExecuteAsync(project);
        await vm.ConfirmDeleteProjectCommand.ExecuteAsync(null);

        Assert.Empty(UserProjects(vm));

        var stored = await _repo.GetByIdAsync(taskId);
        Assert.NotNull(stored);
        Assert.Equal("不该丢失的任务", stored.Title);
        Assert.Equal(DefaultProject.Id, stored.ProjectId);
        Assert.False(stored.IsDeleted);
    }

    /// <summary>
    /// 删除当前选中的项目须回到「全部任务」，不能停留在不存在的筛选上。
    /// </summary>
    [AvaloniaFact]
    public async Task ConfirmDeleteSelectedProject_FallsBackToActiveView()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "选中后删除";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);
        await vm.SelectProjectCommand.ExecuteAsync(project);

        await vm.RequestDeleteProjectCommand.ExecuteAsync(project);
        await vm.ConfirmDeleteProjectCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedProject);
        Assert.Equal(TaskFilter.Active, vm.CurrentFilter);
        Assert.True(vm.IsActiveFilterSelected);
    }

    /// <summary>
    /// 归档当前选中项目后，选中态须被解除。
    /// </summary>
    [AvaloniaFact]
    public async Task ArchiveSelectedProject_ClearsSelection()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "选中后归档";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);
        await vm.SelectProjectCommand.ExecuteAsync(project);

        await vm.ArchiveProjectCommand.ExecuteAsync(project);

        Assert.Null(vm.SelectedProject);
        Assert.Equal(TaskFilter.Active, vm.CurrentFilter);
    }

    // ==================== 改色 ====================

    [AvaloniaFact]
    public async Task ChangeProjectColor_RotatesToDifferentColor()
    {
        var vm = await CreateInitializedAsync();
        vm.NewProjectName = "改色项目";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = SoleUserProject(vm);
        var before = project.ColorHex;

        await vm.ChangeProjectColorCommand.ExecuteAsync(project);

        Assert.NotEqual(before, project.ColorHex);
        Assert.Equal(project.ColorHex, (await _projectRepo.GetByIdAsync(project.Id))!.ColorHex);
    }

    // ==================== 空参数容错 ====================

    [AvaloniaFact]
    public async Task ProjectCommands_TolerateNullParameter()
    {
        var vm = await CreateInitializedAsync();

        await vm.SelectProjectCommand.ExecuteAsync(null);
        vm.BeginRenameProjectCommand.Execute(null);
        vm.CancelRenameProjectCommand.Execute(null);
        await vm.CommitRenameProjectCommand.ExecuteAsync(null);
        await vm.ChangeProjectColorCommand.ExecuteAsync(null);
        await vm.ArchiveProjectCommand.ExecuteAsync(null);
        await vm.RequestDeleteProjectCommand.ExecuteAsync(null);
        await vm.ConfirmDeleteProjectCommand.ExecuteAsync(null);

        Assert.Empty(UserProjects(vm));
    }
}

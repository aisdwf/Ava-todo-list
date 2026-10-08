using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using FlowTask.Desktop.Appearance;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Avalonia.Headless.XUnit;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖主工作台视图模型的状态流转与命令行为。
/// </summary>
/// <remarks>
/// 这些断言针对已修复的实际缺陷：删除命令参数类型错配、侧边栏计数恒为 0、
/// 筛选切换重复加载、停留设置页时导航无响应。均属 Article 1 定义的"测试疏漏"类根因。
///
/// 使用 <c>[AvaloniaFact]</c>：MainViewModel 构造会触达 <c>Application.Resources</c>
/// 应用强调色，而 Avalonia 资源与笔刷对象带 UI 线程校验。
/// </remarks>
public class MainViewModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteTaskRepository _repo;
    private readonly SqliteProjectRepository _projectRepo;

    /// <summary>
    /// 每个用例使用独立数据库文件，避免相互干扰。
    /// </summary>
    public MainViewModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_vm_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _repo = new SqliteTaskRepository(_clock, _dbPath);
        // 与任务仓储同库：删除项目的事务需跨两张表
        _projectRepo = new SqliteProjectRepository(_dbPath);
    }

    /// <inheritdoc />
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

    private MainViewModel CreateViewModel()
    {
        var settingsRepo = new SqliteAppSettingsRepository(_dbPath);
        return new(_repo, _projectRepo, _clock, settingsRepo);
    }

    [AvaloniaFact]
    public async Task AddTask_AppendsToStreamAndClearsInput()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "写设计文档";
        vm.NewTaskPriority = TaskPriority.High;
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Single(vm.Tasks);
        Assert.Equal("写设计文档", vm.Tasks[0].Task.Title);
        Assert.Equal(TaskPriority.High, vm.Tasks[0].Task.Priority);
        Assert.Empty(vm.NewTaskTitle);
    }

    [AvaloniaFact]
    public async Task AddTask_IgnoresWhitespaceOnlyTitle()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "   ";
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Empty(vm.Tasks);
    }

    /// <summary>
    /// 回归防护：命令此前声明为 <c>RelayCommand&lt;string&gt;</c>，
    /// 而视图传入 TaskItem，类型错配会在点击删除时抛异常。
    /// </summary>
    [AvaloniaFact]
    public async Task DeleteTask_AcceptsTaskItemParameter()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "待删除";
        await vm.AddTaskCommand.ExecuteAsync(null);
        var target = vm.Tasks[0].Task;

        await vm.DeleteTaskCommand.ExecuteAsync(target);

        Assert.Empty(vm.Tasks);
        Assert.Null(await _repo.GetByIdAsync(target.Id));
    }

    [AvaloniaFact]
    public async Task DeleteTask_ToleratesNullParameter()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        await vm.DeleteTaskCommand.ExecuteAsync(null);

        Assert.Empty(vm.Tasks);
    }

    /// <summary>
    /// 看板计数含已完成任务；勾选不把任务送出列表。
    /// </summary>
    [AvaloniaFact]
    public async Task Counts_IncludeCompletedOnAllTasksBoard()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "任务甲";
        await vm.AddTaskCommand.ExecuteAsync(null);
        vm.NewTaskTitle = "任务乙";
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.ActiveCount);

        var first = vm.Tasks[0].Task;
        first.IsCompleted = true;
        await vm.ToggleCompleteCommand.ExecuteAsync(first);

        Assert.Equal(2, vm.ActiveCount);
        Assert.Equal(2, vm.Tasks.Count);
        Assert.True(vm.Tasks.Last().Task.IsCompleted);
    }

    [AvaloniaFact]
    public async Task ToggleComplete_StampsCompletionTime()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "打卡";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var task = vm.Tasks[0].Task;
        task.IsCompleted = true;
        await vm.ToggleCompleteCommand.ExecuteAsync(task);

        var stored = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsCompleted);
        Assert.NotNull(stored.CompletedAt);
    }

    [AvaloniaFact]
    public async Task CompletedTask_StaysInListAndCanBeDeleted()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "已办";
        await vm.AddTaskCommand.ExecuteAsync(null);
        vm.NewTaskTitle = "未办";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var done = vm.Tasks.First(t => t.Task.Title == "已办").Task;
        done.IsCompleted = true;
        await vm.ToggleCompleteCommand.ExecuteAsync(done);

        Assert.Equal(2, vm.Tasks.Count);
        Assert.Equal("未办", vm.Tasks[0].Task.Title);
        Assert.Equal("已办", vm.Tasks[1].Task.Title);

        await vm.DeleteTaskCommand.ExecuteAsync(done);
        Assert.Single(vm.Tasks);
        Assert.Equal("未办", vm.Tasks[0].Task.Title);
        Assert.Null(await _repo.GetByIdAsync(done.Id));
    }

    [AvaloniaFact]
    public void ChangeFilter_SyncsRadioSelectionAndHeroTitle()
    {
        var vm = CreateViewModel();

        vm.ChangeFilterCommand.Execute(TaskFilter.Active);

        Assert.True(vm.IsActiveFilterSelected);
        Assert.Equal("全部任务", vm.CurrentCategoryTitle);
    }

    /// <summary>
    /// 调用形式随 spec-sidebar-selection-consolidation 变化：VIEWS 已从
    /// <c>RadioButton.IsChecked</c> 双向绑定改为 <c>Button + Command</c>，
    /// 派生属性故经命令驱动而非直接赋值。
    /// </summary>
    [AvaloniaFact]
    public void RadioSelection_DrivesFilter()
    {
        var vm = CreateViewModel();

        vm.ChangeFilterCommand.Execute(TaskFilter.Active);

        Assert.Equal(TaskFilter.Active, vm.CurrentFilter);
        Assert.True(vm.IsActiveFilterSelected);
    }

    /// <summary>
    /// 停留设置页时点击左侧导航必须回到任务流。
    /// 特别针对"目标筛选等于当前值"的场景：属性不变更，不能依赖变更回调。
    /// </summary>
    [AvaloniaFact]
    public void ChangeFilter_LeavesSettingsView()
    {
        var vm = CreateViewModel();
        vm.ToggleSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);

        vm.ChangeFilterCommand.Execute(TaskFilter.Active);

        Assert.False(vm.IsSettingsOpen);
    }

    /// <summary>
    /// 覆盖「点击当前已选中的导航项」这一此前需要走 IsChecked 双向绑定
    /// 才能触发的场景；调用形式随 spec-sidebar-selection-consolidation 改为命令驱动，
    /// 断言与整改前逐一致。
    /// </summary>
    [AvaloniaFact]
    public void RadioSelectionOnCurrentView_AlsoLeavesSettings()
    {
        var vm = CreateViewModel();
        vm.ToggleSettingsCommand.Execute(null);

        vm.ChangeFilterCommand.Execute(TaskFilter.Active);

        Assert.False(vm.IsSettingsOpen);
        Assert.Equal(TaskFilter.Active, vm.CurrentFilter);
    }

    [AvaloniaFact]
    public void ToggleSettings_FlipsSettingsVisibility()
    {
        var vm = CreateViewModel();

        vm.ToggleSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);

        vm.ToggleSettingsCommand.Execute(null);
        Assert.False(vm.IsSettingsOpen);
    }

    [AvaloniaFact]
    public async Task EmptyStreamFlag_TracksCollectionContent()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.True(vm.IsTaskStreamEmpty);

        vm.NewTaskTitle = "第一件事";
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.False(vm.IsTaskStreamEmpty);
    }

    /// <summary>
    /// 双击标题就地改名后回车须落库（spec-inline-task-edit）。
    /// </summary>
    /// <remarks>
    /// 回归防护：在此之前任务创建后完全无法修改，
    /// 打错一个字只能删除重建（且会丢失原 CreatedAt）。
    /// </remarks>
    [AvaloniaFact]
    public async Task CommitTitleEdit_PersistsTitleChange()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "原标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.BeginTitleEditCommand.ExecuteAsync(row);
        Assert.True(row.IsEditingTitle);
        Assert.Equal("原标题", row.TitleBuffer);

        row.TitleBuffer = "改后的标题";
        await vm.CommitTitleEditCommand.ExecuteAsync(row);

        Assert.False(row.IsEditingTitle);
        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal("改后的标题", stored!.Title);
        Assert.Equal("改后的标题", vm.Tasks.Single(t => t.Task.Id == row.Task.Id).Title);
    }

    [AvaloniaFact]
    public async Task CommitTitleEdit_TrimsTitle()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.BeginTitleEditCommand.ExecuteAsync(row);
        row.TitleBuffer = "   带空格的标题   ";
        await vm.CommitTitleEditCommand.ExecuteAsync(row);

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal("带空格的标题", stored!.Title);
    }

    /// <summary>
    /// 清空标题后提交须保留原值，而非保存空标题。
    /// </summary>
    /// <remarks>
    /// 无标题任务在列表中不可识别，等同数据垃圾。
    /// 该行为须与创建路径的「空白标题静默忽略」一致（Article 6）。
    /// </remarks>
    [AvaloniaFact]
    public async Task CommitTitleEdit_KeepsOriginalTitleWhenCleared()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "不该丢失的标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.BeginTitleEditCommand.ExecuteAsync(row);
        row.TitleBuffer = "   ";
        await vm.CommitTitleEditCommand.ExecuteAsync(row);

        Assert.False(row.IsEditingTitle);
        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal("不该丢失的标题", stored!.Title);
    }

    /// <summary>
    /// Esc 取消：丢弃输入，实体与库都保持原标题（owner 裁决「Esc取消」）。
    /// </summary>
    [AvaloniaFact]
    public async Task CancelTitleEdit_DiscardsBuffer()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "原标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.BeginTitleEditCommand.ExecuteAsync(row);
        row.TitleBuffer = "不要的修改";
        vm.CancelTitleEditCommand.Execute(row);

        Assert.False(row.IsEditingTitle);
        Assert.Equal("原标题", row.Task.Title);
        Assert.Equal("原标题", (await _repo.GetByIdAsync(row.Task.Id))!.Title);
    }

    /// <summary>
    /// 提交幂等：回车后失焦 / 点外部会再次触发提交，第二次不得覆盖或报错。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitTitleEdit_IsIdempotentAfterFirstCommit()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "原标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.BeginTitleEditCommand.ExecuteAsync(row);
        row.TitleBuffer = "新标题";
        await vm.CommitTitleEditCommand.ExecuteAsync(row);
        await vm.CommitTitleEditCommand.ExecuteAsync(row);

        Assert.Equal("新标题", (await _repo.GetByIdAsync(row.Task.Id))!.Title);
    }

    /// <summary>
    /// 行上选择器提交即持久化，列表行随之刷新（spec-due-date-picker）。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitRowDueDate_PersistsSelectedDate()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "带到期日";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.CommitRowDueDateCommand.ExecuteAsync(new DueDateCommit(row, new DateTime(2026, 3, 10)));

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal(new DateTime(2026, 3, 10), stored!.DueDate);
        Assert.Equal(new DateTime(2026, 3, 10), vm.Tasks[0].DueDate);
    }

    /// <summary>
    /// 行上选择器清除到期日后持久化为 null。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitRowDueDate_ClearPersistsNull()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "任务";
        await vm.AddTaskCommand.ExecuteAsync(null);

        await vm.CommitRowDueDateCommand.ExecuteAsync(new DueDateCommit(vm.Tasks[0], new DateTime(2026, 3, 10)));
        var row = vm.Tasks[0];
        await vm.CommitRowDueDateCommand.ExecuteAsync(new DueDateCommit(row, null));

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Null(stored!.DueDate);
    }

    /// <summary>
    /// 创建栏选择器的提交值随新任务写入，保存后清空，下一条默认无到期日。
    /// </summary>
    [AvaloniaFact]
    public async Task AddTask_UsesCreateBarDueDateThenResets()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewDueDateEditor.Load(null);
        vm.NewDueDateEditor.ApplyDefaultDueCommand.Execute(null);
        vm.NewTaskTitle = "带默认到期";
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Null(vm.NewTaskDueDate);
        var stored = await _repo.GetByIdAsync(vm.Tasks[0].Task.Id);
        Assert.Equal(_clock.Today.AddDays(vm.DefaultDueOffsetDays), stored!.DueDate);

        vm.NewTaskTitle = "无到期";
        await vm.AddTaskCommand.ExecuteAsync(null);
        var plain = vm.Tasks.Single(t => t.Task.Title == "无到期");
        Assert.Null(plain.Task.DueDate);
    }

    [AvaloniaFact]
    public async Task InlineEditCommands_TolerateNullParameter()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        await vm.BeginTitleEditCommand.ExecuteAsync(null);
        await vm.CommitTitleEditCommand.ExecuteAsync(null);
        vm.CancelTitleEditCommand.Execute(null);
        await vm.CommitRowPriorityCommand.ExecuteAsync(null);

        Assert.Empty(vm.Tasks);
    }

    /// <summary>
    /// 行上优先级浮层点选即持久化，行投影随之刷新（spec-inline-task-edit）。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitRowPriority_PersistsPickedPriority()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "任务";
        vm.NewTaskPriority = TaskPriority.Low;
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.CommitRowPriorityCommand.ExecuteAsync(new PriorityCommit(row, TaskPriority.High));

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal(TaskPriority.High, stored!.Priority);
        Assert.Equal(TaskPriority.High, vm.Tasks.Single(t => t.Task.Id == row.Task.Id).Priority);
    }

    /// <summary>
    /// 提升优先级后列表按新优先级重排（TaskListOrder：未完成内按优先级）。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitRowPriority_ReordersList()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskPriority = TaskPriority.Medium;
        vm.NewTaskTitle = "中";
        await vm.AddTaskCommand.ExecuteAsync(null);
        vm.NewTaskPriority = TaskPriority.Low;
        vm.NewTaskTitle = "低";
        await vm.AddTaskCommand.ExecuteAsync(null);
        Assert.Equal("中", vm.Tasks[0].Title);

        var low = vm.Tasks.Single(t => t.Title == "低");
        await vm.CommitRowPriorityCommand.ExecuteAsync(new PriorityCommit(low, TaskPriority.High));

        Assert.Equal("低", vm.Tasks[0].Title);
    }

    /// <summary>
    /// 同一时刻只允许一行处于标题编辑；双击另一行时前一行的输入须提交而非静默丢弃。
    /// </summary>
    [AvaloniaFact]
    public async Task BeginTitleEdit_CommitsOtherEditingRow()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "第一条";
        await vm.AddTaskCommand.ExecuteAsync(null);
        vm.NewTaskTitle = "第二条";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var first = vm.Tasks.First(t => t.Task.Title == "第一条");
        await vm.BeginTitleEditCommand.ExecuteAsync(first);
        first.TitleBuffer = "第一条改名";

        var second = vm.Tasks.First(t => t.Task.Title == "第二条");
        await vm.BeginTitleEditCommand.ExecuteAsync(second);

        // 前一行的输入已落库；提交引起整表重载后，编辑态落在列表里的当前实例上
        Assert.Equal("第一条改名", (await _repo.GetByIdAsync(first.Task.Id))!.Title);
        var editing = Assert.Single(vm.Tasks, t => t.IsEditingTitle);
        Assert.Equal(second.Task.Id, editing.Task.Id);
    }

    [AvaloniaFact]
    public async Task AssignProject_SetsAndClearsAssignment()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "待归类";
        await vm.AddTaskCommand.ExecuteAsync(null);
        var task = vm.Tasks[0].Task;

        await vm.AssignProjectAsync(task, "proj-42");
        Assert.Equal("proj-42", (await _repo.GetByIdAsync(task.Id))!.ProjectId);

        // R-2.6：不再允许写回 null，「清空」改挂 Default
        await vm.AssignProjectAsync(task, null);
        Assert.Equal(DefaultProject.Id, (await _repo.GetByIdAsync(task.Id))!.ProjectId);
    }

    /// <summary>
    /// 主窗启动才把历史 <c>ProjectId IS NULL</c> 迁到 Default。
    /// </summary>
    [AvaloniaFact]
    public async Task InitializeAsync_MigratesNullProjectIdsToDefault()
    {
        var orphan = TaskItemFactory.Create(_clock, "历史未归属");
        await _repo.SaveTaskAsync(orphan);
        Assert.Null(orphan.ProjectId);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.Equal(DefaultProject.Id, (await _repo.GetByIdAsync(orphan.Id))!.ProjectId);
    }

    /// <summary>
    /// 到期日写入后「今日聚焦」不再恒为空。
    /// </summary>
    /// <remarks>
    /// 这是 R-2.2 的核心验收点：此前 <c>DueDate</c> 无任何 UI 写入路径，
    /// 致该视图必然返回空集。
    /// </remarks>
    [AvaloniaFact]
    public async Task SetDueDate_MakesTaskAppearInTodayView()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "今天要做的事";
        await vm.AddTaskCommand.ExecuteAsync(null);
        var task = vm.Tasks[0].Task;

        // 设定前：今日视图为空
        Assert.Empty(await _repo.GetTodayTasksAsync());

        await vm.SetDueDateAsync(task, _clock.Today);

        var todays = await _repo.GetTodayTasksAsync();
        Assert.Single(todays);
        Assert.Equal("今天要做的事", todays[0].Title);
    }

    [AvaloniaFact]
    public async Task SetDueDate_ClearsWhenNull()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "有到期日";
        await vm.AddTaskCommand.ExecuteAsync(null);
        var task = vm.Tasks[0].Task;

        await vm.SetDueDateAsync(task, _clock.Today);
        Assert.NotNull((await _repo.GetByIdAsync(task.Id))!.DueDate);

        await vm.SetDueDateAsync(task, null);
        Assert.Null((await _repo.GetByIdAsync(task.Id))!.DueDate);
    }

    /// <summary>
    /// 新任务的创建时刻须来自注入的时钟，而非系统时钟。
    /// </summary>
    [AvaloniaFact]
    public async Task AddTask_StampsCreatedAtFromInjectedClock()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "检查时间戳";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var stored = await _repo.GetByIdAsync(vm.Tasks[0].Task.Id);
        Assert.Equal(_clock.UtcNow, stored!.CreatedAt);
    }

    /// <summary>
    /// 完成时刻同样须来自注入的时钟。
    /// </summary>
    [AvaloniaFact]
    public async Task ToggleComplete_StampsCompletedAtFromInjectedClock()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "打卡";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var task = vm.Tasks[0].Task;
        task.IsCompleted = true;
        await vm.ToggleCompleteCommand.ExecuteAsync(task);

        var stored = await _repo.GetByIdAsync(task.Id);
        Assert.Equal(_clock.UtcNow, stored!.CompletedAt);
    }

    [AvaloniaFact]
    public void AppearanceSelections_DefaultToFirstPreset()
    {
        var vm = CreateViewModel();

        Assert.Equal("default", vm.SelectedThemePreset.Id);
        Assert.Equal(9, vm.ThemePresets.Count);
        Assert.Equal(4, vm.MaterialPresets.Count);
    }

    /// <summary>
    /// 材质选中变更必须外发请求，否则视图层无从应用透明度等级 —— 
    /// 这正是"切换材质无任何变化"的其中一环。
    /// </summary>
    [AvaloniaFact]
    public void SelectedMaterial_RaisesChangeRequestForView()
    {
        var vm = CreateViewModel();
        string? requested = null;
        vm.MaterialPresetChanged += preset => requested = preset.Id;

        vm.SelectedMaterial = vm.MaterialPresets.First(p => p.Id == "Solid");

        Assert.Equal("Solid", requested);
    }

    /// <summary>
    /// 主题应用必须通知视图层：窗口背景是代码构建的具体笔刷，
    /// 不随主题字典重新求值，需靠该事件重建。
    /// </summary>
    [AvaloniaFact]
    public void ApplyTheme_NotifiesViewToRebuildBackground()
    {
        var vm = CreateViewModel();
        var notified = 0;
        vm.ThemeApplied += () => notified++;

        vm.ApplyTheme(false);
        Assert.False(vm.IsDarkTheme);

        vm.ApplyTheme(true);
        Assert.True(vm.IsDarkTheme);

        Assert.Equal(2, notified);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_RestoresPersistedAppearance()
    {
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await settings.SetAsync(AppearanceCoordinator.ThemePresetSettingsKey, "ocean-breeze");
        await settings.SetAsync(AppearanceCoordinator.MaterialSettingsKey, "Solid");
        await settings.SetAsync(AppearanceCoordinator.IsDarkSettingsKey, "0");

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.Equal("ocean-breeze", vm.SelectedThemePreset.Id);
        Assert.Equal("Solid", vm.SelectedMaterial.Id);
        Assert.False(vm.IsDarkTheme);
    }

    [AvaloniaFact]
    public async Task LoadAppearanceAsync_RestoresPersistedAppearanceWithoutInitialize()
    {
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await settings.SetAsync(AppearanceCoordinator.ThemePresetSettingsKey, "ocean-breeze");
        await settings.SetAsync(AppearanceCoordinator.MaterialSettingsKey, "Solid");
        await settings.SetAsync(AppearanceCoordinator.IsDarkSettingsKey, "0");

        var vm = CreateViewModel();
        await vm.LoadAppearanceAsync();

        Assert.Equal("ocean-breeze", vm.SelectedThemePreset.Id);
        Assert.Equal("Solid", vm.SelectedMaterial.Id);
        Assert.False(vm.IsDarkTheme);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_DoesNotReloadAppearanceAfterStartupLoad()
    {
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await settings.SetAsync(AppearanceCoordinator.ThemePresetSettingsKey, "ocean-breeze");
        await settings.SetAsync(AppearanceCoordinator.MaterialSettingsKey, "Solid");
        await settings.SetAsync(AppearanceCoordinator.IsDarkSettingsKey, "0");

        var vm = CreateViewModel();
        await vm.LoadAppearanceAsync();

        await settings.SetAsync(AppearanceCoordinator.ThemePresetSettingsKey, "anthropic");
        await vm.InitializeAsync();

        Assert.Equal("ocean-breeze", vm.SelectedThemePreset.Id);
    }

    [AvaloniaFact]
    public async Task AppearanceChanges_RoundTripAcrossViewModelInstances()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.SelectedThemePreset = vm.ThemePresets.First(p => p.Id == "anthropic");
        vm.SelectedMaterial = vm.MaterialPresets.First(p => p.Id == "Acrylic");
        vm.ApplyTheme(false);
        await vm.AppearancePersistTask;

        var restored = CreateViewModel();
        await restored.InitializeAsync();

        Assert.Equal("anthropic", restored.SelectedThemePreset.Id);
        Assert.Equal("Acrylic", restored.SelectedMaterial.Id);
        Assert.False(restored.IsDarkTheme);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_UnknownAppearanceIds_FallBackToFirstPreset()
    {
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await settings.SetAsync(AppearanceCoordinator.ThemePresetSettingsKey, "not-a-theme");
        await settings.SetAsync(AppearanceCoordinator.MaterialSettingsKey, "not-a-material");
        await settings.SetAsync(AppearanceCoordinator.IsDarkSettingsKey, "maybe");

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.Equal(AppearanceCoordinator.ThemePresets[0].Id, vm.SelectedThemePreset.Id);
        Assert.Equal(AppearanceCoordinator.MaterialPresets[0].Id, vm.SelectedMaterial.Id);
        Assert.True(vm.IsDarkTheme);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_MissingAppearanceKeys_KeepsCompiledDefaults()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.Equal("default", vm.SelectedThemePreset.Id);
        Assert.Equal("Mica", vm.SelectedMaterial.Id);
        Assert.True(vm.IsDarkTheme);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_MissingCloseAction_RemainsUnset()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        Assert.Null(vm.CloseAction);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_RestoresPersistedCloseAction()
    {
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await settings.SetAsync(CloseBehaviorCoordinator.SettingsKey, CloseBehaviorCoordinator.TrayStorage);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.Equal(CloseActionKind.MinimizeToTray, vm.CloseAction);
    }

    [AvaloniaFact]
    public async Task CloseActionChange_RoundTripsAcrossViewModelInstances()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.CloseAction = CloseActionKind.Exit;
        await vm.CloseActionPersistTask;

        var restored = CreateViewModel();
        await restored.InitializeAsync();
        Assert.Equal(CloseActionKind.Exit, restored.CloseAction);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_UnknownCloseAction_TreatedAsUnset()
    {
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await settings.SetAsync(CloseBehaviorCoordinator.SettingsKey, "ask-every-time");

        var vm = CreateViewModel();
        await vm.InitializeAsync();
        Assert.Null(vm.CloseAction);
    }

    [AvaloniaFact]
    public async Task ConfirmClosePrompt_WithoutRemember_DoesNotPersist()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        var hidden = false;
        vm.RequestHideToTray += () => hidden = true;

        vm.OpenClosePrompt();
        vm.RememberCloseAction = false;
        vm.ConfirmClosePromptCommand.Execute(null);

        Assert.True(hidden);
        Assert.False(vm.IsClosePromptOpen);
        Assert.Null(vm.CloseAction);
    }

    [AvaloniaFact]
    public async Task ConfirmClosePrompt_WithRemember_PersistsSelectedChoice()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.OpenClosePrompt();
        vm.ClosePromptChoice = CloseActionKind.Exit;
        vm.RememberCloseAction = true;
        vm.ConfirmClosePromptCommand.Execute(null);
        await vm.CloseActionPersistTask;

        var restored = CreateViewModel();
        await restored.InitializeAsync();
        Assert.Equal(CloseActionKind.Exit, restored.CloseAction);
    }

    [AvaloniaFact]
    public void OpenClosePrompt_PreselectsRecommendedTray()
    {
        var vm = CreateViewModel();
        vm.ClosePromptChoice = CloseActionKind.Exit;
        vm.OpenClosePrompt();

        Assert.True(vm.IsClosePromptOpen);
        Assert.Equal(CloseActionKind.MinimizeToTray, vm.ClosePromptChoice);
        Assert.False(vm.RememberCloseAction);
    }

    [AvaloniaFact]
    public void BlockingOverlay_TracksClosePrompt()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsBlockingOverlayOpen);

        vm.OpenClosePrompt();
        Assert.True(vm.IsBlockingOverlayOpen);

        vm.DismissClosePromptCommand.Execute(null);
        Assert.False(vm.IsBlockingOverlayOpen);
    }

    /// <summary>
    /// 删除项目确认改为弹层后，打开期间屏蔽主内容命中；取消后恢复。
    /// </summary>
    [AvaloniaFact]
    public async Task DeleteProjectPrompt_BlocksOverlayUntilCancelled()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.NewProjectName = "待删";
        await vm.CreateProjectCommand.ExecuteAsync(null);
        var project = vm.Projects.Single(p => p.Name == "待删");

        await vm.RequestDeleteProjectCommand.ExecuteAsync(project);
        Assert.True(vm.IsDeleteProjectPromptOpen);
        Assert.True(vm.IsBlockingOverlayOpen);

        vm.CancelDeleteProjectCommand.Execute(null);
        Assert.False(vm.IsDeleteProjectPromptOpen);
        Assert.False(vm.IsBlockingOverlayOpen);
    }

    [AvaloniaFact]
    public async Task EditingDueOffset_DoesNotApplyUntilSave()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        Assert.Equal(DueDateOffset.DefaultDays, vm.DefaultDueOffsetDays);

        vm.EditingDefaultDueOffsetDays = 7;
        Assert.Equal(DueDateOffset.DefaultDays, vm.DefaultDueOffsetDays);

        await vm.SaveDefaultDueOffsetCommand.ExecuteAsync(null);
        Assert.Equal(7, vm.DefaultDueOffsetDays);
        Assert.Equal(7, await new SqliteAppSettingsRepository(_dbPath).GetDefaultDueOffsetDaysAsync());
    }
}

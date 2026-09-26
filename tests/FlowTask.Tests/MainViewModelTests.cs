using FlowTask.Core.Enums;
using FlowTask.Desktop.Appearance;
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
    /// 回归防护：ActiveCount / CompletedCount 此前从未被赋值，侧边栏徽标恒显 0。
    /// </summary>
    [AvaloniaFact]
    public async Task Counts_ReflectActiveAndCompletedTotals()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "任务甲";
        await vm.AddTaskCommand.ExecuteAsync(null);
        vm.NewTaskTitle = "任务乙";
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.ActiveCount);
        Assert.Equal(0, vm.CompletedCount);
        Assert.Equal(0, vm.PendingArchiveCount);

        var first = vm.Tasks[0].Task;
        first.IsCompleted = true;
        await vm.ToggleCompleteCommand.ExecuteAsync(first);

        // 完成 ≠ 归档（spec-task-complete-before-archive）：勾选完成后任务仍留在活动列表，
        // ActiveCount 不变；CompletedCount（=已归档数）在显式归档前也不变
        Assert.Equal(2, vm.ActiveCount);
        Assert.Equal(0, vm.CompletedCount);
        Assert.Equal(1, vm.PendingArchiveCount);

        await vm.ArchiveCompletedCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.ActiveCount);
        Assert.Equal(1, vm.CompletedCount);
        Assert.Equal(0, vm.PendingArchiveCount);
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
    public async Task CompletedFilter_ShowsOnlyArchivedTasks()
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

        // 完成 ≠ 归档：只勾选完成时，「已完成归档」视图仍应为空
        vm.ChangeFilterCommand.Execute(TaskFilter.Completed);
        await vm.LoadTasksCommand.ExecuteAsync(null);
        Assert.Empty(vm.Tasks);

        // 显式归档后才出现在「已完成归档」视图
        await vm.ArchiveCompletedCommand.ExecuteAsync(null);
        vm.ChangeFilterCommand.Execute(TaskFilter.Completed);
        await vm.LoadTasksCommand.ExecuteAsync(null);

        Assert.Single(vm.Tasks);
        Assert.Equal("已办", vm.Tasks[0].Task.Title);
    }

    [AvaloniaFact]
    public void ChangeFilter_SyncsRadioSelectionAndHeroTitle()
    {
        var vm = CreateViewModel();

        vm.ChangeFilterCommand.Execute(TaskFilter.Completed);

        Assert.True(vm.IsCompletedFilterSelected);
        Assert.False(vm.IsActiveFilterSelected);
        Assert.Equal("已完成归档", vm.CurrentCategoryTitle);
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

        vm.ChangeFilterCommand.Execute(TaskFilter.Completed);

        Assert.Equal(TaskFilter.Completed, vm.CurrentFilter);
        Assert.False(vm.IsActiveFilterSelected);
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

        vm.ChangeFilterCommand.Execute(TaskFilter.Completed);

        Assert.False(vm.IsSettingsOpen);
        Assert.Equal(TaskFilter.Completed, vm.CurrentFilter);
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
    /// 编辑任务标题后须落库。
    /// </summary>
    /// <remarks>
    /// 回归防护：在此之前任务创建后完全无法修改，
    /// 打错一个字只能删除重建（且会丢失原 CreatedAt）。
    /// </remarks>
    [AvaloniaFact]
    public async Task SaveEdit_PersistsTitleChange()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "原标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.ToggleEditCommand.ExecuteAsync(row);
        Assert.True(row.IsEditing);
        Assert.Equal("原标题", row.EditTitle);

        row.EditTitle = "改后的标题";
        await vm.SaveEditCommand.ExecuteAsync(row);

        Assert.False(row.IsEditing);
        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal("改后的标题", stored!.Title);
    }

    [AvaloniaFact]
    public async Task SaveEdit_TrimsTitle()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.ToggleEditCommand.ExecuteAsync(row);
        row.EditTitle = "   带空格的标题   ";
        await vm.SaveEditCommand.ExecuteAsync(row);

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal("带空格的标题", stored!.Title);
    }

    /// <summary>
    /// 编辑时清空标题须保留原值，而非保存空标题。
    /// </summary>
    /// <remarks>
    /// 无标题任务在列表中不可识别，等同数据垃圾。
    /// 该行为须与创建路径的「空白标题静默忽略」一致（Article 6）。
    /// </remarks>
    [AvaloniaFact]
    public async Task SaveEdit_KeepsOriginalTitleWhenCleared()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "不该丢失的标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.ToggleEditCommand.ExecuteAsync(row);
        row.EditTitle = "   ";
        await vm.SaveEditCommand.ExecuteAsync(row);

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal("不该丢失的标题", stored!.Title);
    }

    /// <summary>
    /// 标题非法时只丢弃该项修改，其余字段照常保存。
    /// </summary>
    /// <remarks>
    /// 若因标题非法而拒绝整次提交，用户改对了的日期与标签会一并丢失 ——
    /// 那是比「标题没改成功」更严重的意外数据损失。
    /// </remarks>
    [AvaloniaFact]
    public async Task SaveEdit_AppliesOtherFieldsEvenWhenTitleInvalid()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "原标题";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.ToggleEditCommand.ExecuteAsync(row);
        row.EditTitle = "";
        row.EditPriority = TaskPriority.High;
        await vm.SaveEditCommand.ExecuteAsync(row);

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal("原标题", stored!.Title);
        Assert.Equal(TaskPriority.High, stored.Priority);
    }

    /// <summary>
    /// 行上弹出编辑器可写入到期日。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitDueDatePopup_PersistsSelectedDate()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "带到期日";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        vm.OpenDueDatePopupCommand.Execute(row);
        vm.EditingDueDateEditor.UpdateCalendarInput(new DateTime(2026, 3, 10));
        await vm.CommitDueDatePopupCommand.ExecuteAsync(null);

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal(new DateTime(2026, 3, 10), stored!.DueDate);
    }

    /// <summary>
    /// 弹出编辑器清除到期日后持久化为 null。
    /// </summary>
    [AvaloniaFact]
    public async Task CommitDueDatePopup_ClearPersistsNull()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "任务";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        vm.OpenDueDatePopupCommand.Execute(row);
        vm.EditingDueDateEditor.UpdateCalendarInput(new DateTime(2026, 3, 10));
        await vm.CommitDueDatePopupCommand.ExecuteAsync(null);

        row = vm.Tasks[0];
        vm.OpenDueDatePopupCommand.Execute(row);
        vm.EditingDueDateEditor.ClearDueCommand.Execute(null);
        await vm.CommitDueDatePopupCommand.ExecuteAsync(null);

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Null(stored!.DueDate);
    }

    [AvaloniaFact]
    public async Task SaveEdit_ToleratesNullParameter()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        await vm.SaveEditCommand.ExecuteAsync(null);
        await vm.ToggleEditCommand.ExecuteAsync(null);

        Assert.Empty(vm.Tasks);
    }

    [AvaloniaFact]
    public async Task SaveEdit_PersistsPriorityChange()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "任务";
        vm.NewTaskPriority = TaskPriority.Low;
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.ToggleEditCommand.ExecuteAsync(row);
        row.EditPriority = TaskPriority.High;
        await vm.SaveEditCommand.ExecuteAsync(row);

        var stored = await _repo.GetByIdAsync(row.Task.Id);
        Assert.Equal(TaskPriority.High, stored!.Priority);
    }

    /// <summary>
    /// 同一时刻只允许一行处于编辑态。
    /// </summary>
    /// <remarks>
    /// 多行同时展开会让任务流被面板撑满、丧失浏览性，
    /// 也使「当前在改哪一条」变得不明确。
    /// 切换时前一行的改动须被提交而非静默丢弃。
    /// </remarks>
    [AvaloniaFact]
    public async Task ToggleEdit_ClosesOtherRowsAndCommitsTheirChanges()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "第一条";
        await vm.AddTaskCommand.ExecuteAsync(null);
        vm.NewTaskTitle = "第二条";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var first = vm.Tasks.First(t => t.Task.Title == "第一条");
        await vm.ToggleEditCommand.ExecuteAsync(first);
        first.EditTitle = "第一条改名";

        var second = vm.Tasks.First(t => t.Task.Title == "第二条");
        await vm.ToggleEditCommand.ExecuteAsync(second);

        // 前一行已收起，且其改动已落库（未被静默丢弃）
        Assert.False(first.IsEditing);
        Assert.True(second.IsEditing);
        Assert.Equal("第一条改名", (await _repo.GetByIdAsync(first.Task.Id))!.Title);
    }

    /// <summary>
    /// 再次点击同一行即提交并收起。
    /// </summary>
    [AvaloniaFact]
    public async Task ToggleEdit_OnOpenRowSavesAndCollapses()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.NewTaskTitle = "任务";
        await vm.AddTaskCommand.ExecuteAsync(null);

        var row = vm.Tasks[0];
        await vm.ToggleEditCommand.ExecuteAsync(row);
        row.EditTitle = "改了";
        await vm.ToggleEditCommand.ExecuteAsync(row);

        Assert.False(row.IsEditing);
        Assert.Equal("改了", (await _repo.GetByIdAsync(row.Task.Id))!.Title);
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

        // 置空回到未归属状态 —— 这是正常的默认状态，不是数据缺失
        await vm.AssignProjectAsync(task, null);
        Assert.Null((await _repo.GetByIdAsync(task.Id))!.ProjectId);
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
    public void SelectedAccent_DefaultsToFirstPreset()
    {
        var vm = CreateViewModel();

        Assert.Equal("Blue", vm.SelectedAccent.Id);
        Assert.Equal(4, vm.AccentPresets.Count);
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
}

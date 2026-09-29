using Avalonia.Headless.XUnit;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.ViewModels.Actions;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 写入失败必须回滚实体，不得留下未落库的界面值（扫描 L5）。
/// </summary>
public class PersistThenWritebackTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

    [AvaloniaFact]
    public async Task SaveEdit_RestoresFieldsWhenSaveThrows()
    {
        var task = TaskItemFactory.Create(_clock, "原标题", projectId: DefaultProject.Id);
        var row = new TaskRowViewModel(task, null);
        row.BeginEdit(new List<ProjectChoice> { ProjectChoice.Default });
        row.EditTitle = "新标题";

        var sut = new SaveEditTaskViewModel(new ThrowingTaskRepository());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(row, () => Task.CompletedTask, this));

        Assert.Equal("原标题", task.Title);
    }

    [AvaloniaFact]
    public async Task CommitDueDate_RestoresWhenSaveThrows()
    {
        var task = TaskItemFactory.Create(_clock, "有到期日", projectId: DefaultProject.Id);
        task.DueDate = new DateTime(2026, 3, 10);
        var row = new TaskRowViewModel(task, null);
        var sut = new CommitRowDueDateViewModel(new ThrowingTaskRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(new DueDateCommit(row, new DateTime(2026, 4, 1)), () => Task.CompletedTask, this));

        Assert.Equal(new DateTime(2026, 3, 10), task.DueDate);
    }

    [Fact]
    public async Task ToggleComplete_RestoresWhenSaveThrows()
    {
        var task = TaskItemFactory.Create(_clock, "勾选", projectId: DefaultProject.Id);
        task.IsCompleted = true;
        var sut = new ToggleCompleteTaskViewModel(new ThrowingTaskRepository(), _clock);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(task, () => Task.CompletedTask, this));

        Assert.False(task.IsCompleted);
        Assert.Null(task.CompletedAt);
    }

    private sealed class ThrowingTaskRepository : ITaskRepository
    {
        public Task<List<TaskItem>> GetAllActiveTasksAsync() => Task.FromResult(new List<TaskItem>());
        public Task<List<TaskItem>> GetTodayTasksAsync() => Task.FromResult(new List<TaskItem>());
        public Task<List<TaskItem>> GetTasksByProjectAsync(string? projectId) => Task.FromResult(new List<TaskItem>());
        public Task<TaskItem?> GetByIdAsync(string id) => Task.FromResult<TaskItem?>(null);
        public Task<int> SaveTaskAsync(TaskItem item) => throw new InvalidOperationException("fail");
        public Task<int> PermanentDeleteAsync(string id) => Task.FromResult(0);
        public Task<int> CountActiveTasksAsync() => Task.FromResult(0);
        public Task<IReadOnlyDictionary<string, int>> CountTasksGroupedByProjectAsync()
            => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
    }
}

using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 提交任务行上双击后的标题就地编辑（TR-1，spec-inline-task-edit）。
/// </summary>
/// <remarks>
/// <para>
/// 非法标题（空或全空白）视为放弃修改、保留原标题，而不是报错：
/// 就地编辑没有承载错误提示的位置，且清空后回车最可能的意图是「不改了」。
/// 校验与创建路径共用 <see cref="TaskTitle"/>（Article 6）。
/// </para>
/// <para>
/// 先写实体再落库，失败回滚实体（PersistThenWriteback）。
/// 无变化时不落库也不广播，避免点击外部触发的空提交刷新整表。
/// </para>
/// </remarks>
public sealed class CommitRowTitleViewModel
{
    private readonly ITaskRepository _taskRepository;

    public CommitRowTitleViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    /// <param name="row">处于标题编辑态的任务行；未在编辑态时忽略（提交幂等）。</param>
    /// <param name="reloadTasks">落库成功后的列表刷新。</param>
    /// <param name="origin">总线消息来源，供本窗口跳过自身广播。</param>
    public async Task ExecuteAsync(TaskRowViewModel? row, Func<Task> reloadTasks, object origin)
    {
        if (row is not { IsEditingTitle: true })
        {
            return;
        }

        var buffer = row.TitleBuffer;
        row.EndTitleEdit();

        if (!TaskTitle.IsValid(buffer))
        {
            return;
        }

        var normalized = TaskTitle.Normalize(buffer);
        var task = row.Task;
        if (normalized == task.Title)
        {
            return;
        }

        var previousTitle = task.Title;
        task.Title = normalized;
        try
        {
            await _taskRepository.SaveTaskAsync(task);
        }
        catch
        {
            task.Title = previousTitle;
            throw;
        }

        TaskChangeBus.Saved(task, origin);
        row.RefreshDerivedFlags();
        await reloadTasks();
    }
}

using FlowTask.Core.Models;
using FlowTask.Core.Ordering;
using System.Collections.ObjectModel;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 把仓储里的完成态写回正在展示的任务行，而不是换一套新的行对象。
/// </summary>
/// <remarks>
/// Avalonia 的 <c>ItemsControl</c> 在 <c>Clear</c>+<c>Add</c> 后仍可能继续绑定旧行实例；
/// <see cref="TaskItem"/> 又没有属性变更通知。对端勾选看起来就像“没刷新”，
/// 直到另一次会重建可视树的操作（例如切换项目）。就地改
/// <see cref="TaskRowViewModel.IsCompleted"/> 才能驱动已在屏幕上的勾选圈。
/// </remarks>
internal static class TaskRowListSync
{
    public static bool TryApply(IEnumerable<TaskRowViewModel> rows, TaskItem persisted)
    {
        var row = rows.FirstOrDefault(r => r.Task.Id == persisted.Id);
        if (row is null)
        {
            return false;
        }

        row.ApplyPersistedCompletion(persisted.IsCompleted, persisted.CompletedAt);
        return true;
    }

    public static void Reorder(ObservableCollection<TaskRowViewModel> rows)
    {
        var orderedIds = TaskListOrder.Sort(rows.Select(r => r.Task)).Select(t => t.Id).ToList();
        for (var target = 0; target < orderedIds.Count; target++)
        {
            var source = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Task.Id == orderedIds[target])
                {
                    source = i;
                    break;
                }
            }

            if (source >= 0 && source != target)
            {
                rows.Move(source, target);
            }
        }
    }
}

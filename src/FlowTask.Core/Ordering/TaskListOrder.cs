using FlowTask.Core.Models;

namespace FlowTask.Core.Ordering;

/// <summary>
/// 全部任务看板、单项目列表与小窗共用的任务排序
/// （spec-project-managed-tasks）。
/// </summary>
/// <remarks>
/// 必须在内存中排序：规则含「未完成/已完成分组后走不同的后续关键字」，
/// 无法可靠地翻译成 sqlite-net 的 SQL <c>ORDER BY</c>。
/// 已完成组不再套用优先级：先勾的必须在最底部，优先级只作用于未完成组。
/// </remarks>
public static class TaskListOrder
{
    public static IComparer<TaskItem> Comparer { get; } = new ComparerImpl();

    public static List<TaskItem> Sort(IEnumerable<TaskItem> items)
        => items.OrderBy(t => t, Comparer).ToList();

    private sealed class ComparerImpl : IComparer<TaskItem>
    {
        public int Compare(TaskItem? x, TaskItem? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return 1;
            }

            if (y is null)
            {
                return -1;
            }

            var completed = x.IsCompleted.CompareTo(y.IsCompleted);
            if (completed != 0)
            {
                return completed;
            }

            if (x.IsCompleted)
            {
                var byStamp = CompareDescending(CompletionStamp(y), CompletionStamp(x));
                if (byStamp != 0)
                {
                    return byStamp;
                }

                return string.CompareOrdinal(x.Id, y.Id);
            }

            var priority = ((int)y.Priority).CompareTo((int)x.Priority);
            if (priority != 0)
            {
                return priority;
            }

            var due = CompareDueDateAscendingNullsLast(x.DueDate, y.DueDate);
            if (due != 0)
            {
                return due;
            }

            var created = y.CreatedAt.CompareTo(x.CreatedAt);
            if (created != 0)
            {
                return created;
            }

            return string.CompareOrdinal(x.Id, y.Id);
        }

        /// <summary>
        /// 勾选时刻；历史行可能没有 <see cref="TaskItem.CompletedAt"/>，
        /// 回退到归档占位或创建时刻，避免同键时顺序跟 SQLite 扫描走。
        /// </summary>
        private static DateTime CompletionStamp(TaskItem item)
            => item.CompletedAt ?? item.ArchivedAt ?? item.CreatedAt;

        private static int CompareDescending(DateTime leftNewer, DateTime rightOlder)
            => leftNewer.CompareTo(rightOlder);

        private static int CompareDueDateAscendingNullsLast(DateTime? left, DateTime? right)
        {
            if (left is null && right is null)
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            return left.Value.CompareTo(right.Value);
        }
    }
}

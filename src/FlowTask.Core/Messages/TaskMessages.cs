using FlowTask.Core.Models;

namespace FlowTask.Core.Messages;

/// <summary>
/// 任务已写入 SQLite。各窗口按 <see cref="Origin"/> 判断是否跳过自己刚做过的刷新。
/// </summary>
public sealed record TaskSavedMessage(TaskItem Task, object Origin);

/// <summary>
/// 任务已物理删除。各窗口按 <see cref="Origin"/> 判断是否跳过自己刚做过的刷新。
/// </summary>
public sealed record TaskDeletedMessage(string TaskId, object Origin);

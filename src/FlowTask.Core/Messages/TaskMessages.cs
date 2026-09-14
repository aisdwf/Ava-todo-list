using FlowTask.Core.Models;

namespace FlowTask.Core.Messages;

/// <summary>
/// 当有新任务被创建或更新时广播的消息
/// </summary>
public sealed record TaskSavedMessage(TaskItem Task);

/// <summary>
/// 当有任务被删除时广播的消息
/// </summary>
public sealed record TaskDeletedMessage(string TaskId);

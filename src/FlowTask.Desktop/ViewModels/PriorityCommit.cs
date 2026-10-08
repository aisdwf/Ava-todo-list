using FlowTask.Core.Enums;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 优先级浮层的一次点选：目标（任务行）与选中的优先级。与 <see cref="DueDateCommit"/> 同构。
/// </summary>
/// <param name="Target">提交目标，通常是 <see cref="TaskRowViewModel"/>。</param>
/// <param name="Priority">选中的优先级。</param>
public sealed record PriorityCommit(object? Target, TaskPriority Priority);

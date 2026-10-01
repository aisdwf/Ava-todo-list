using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Messages;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 任务写入成功后的弱引用广播。Origin 是发出窗口，对端据此跳过自己的回声。
/// </summary>
internal static class TaskChangeBus
{
    public static void Saved(TaskItem task, object origin)
        => WeakReferenceMessenger.Default.Send(new TaskSavedMessage(task, origin));

    public static void Deleted(string taskId, object origin)
        => WeakReferenceMessenger.Default.Send(new TaskDeletedMessage(taskId, origin));
}

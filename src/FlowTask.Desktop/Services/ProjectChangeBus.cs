using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Messages;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 项目写入成功后的弱引用广播。Origin 是发出窗口，对端据此跳过自己的回声。
/// </summary>
/// <remarks>
/// 与 <see cref="TaskChangeBus"/> 分开：项目变更需要对端重读项目表（下拉候选），
/// 任务消息只触发列表同步，混用会让对端无法区分该刷新哪一层。
/// </remarks>
internal static class ProjectChangeBus
{
    public static void Changed(object origin)
        => WeakReferenceMessenger.Default.Send(new ProjectsChangedMessage(origin));
}

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 到期日选择器的一次提交：目标（通常是任务行）与提交后的到期日。
/// </summary>
/// <param name="Target">提交目标；创建栏为 <c>null</c>。</param>
/// <param name="Date">提交后的到期日；<c>null</c> 表示清除。</param>
public sealed record DueDateCommit(object? Target, DateTime? Date);

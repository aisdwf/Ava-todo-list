namespace FlowTask.Core.Messages;

/// <summary>
/// 另一个进程改过任务库。收到的窗口从 SQLite 重载，不携带行快照。
/// </summary>
public sealed record CliStoreChangedMessage;

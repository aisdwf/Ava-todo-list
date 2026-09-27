namespace FlowTask.Core.Messages;

/// <summary>
/// 项目集合已变更（新建 / 重命名 / 归档 / 删除）。持有项目下拉等缓存的窗口据此重读项目表；
/// 各窗口按 <see cref="Origin"/> 判断是否跳过自己刚做过的刷新。
/// </summary>
public sealed record ProjectsChangedMessage(object Origin);

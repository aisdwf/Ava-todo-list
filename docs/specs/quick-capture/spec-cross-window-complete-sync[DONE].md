# spec-cross-window-complete-sync: 主窗与快捷小窗勾选实时同步

## Metadata

- **ID**: spec-cross-window-complete-sync
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-27）**：原话「确认修复，涉及两端，注意单一数据源原则。」
> 计划已在确认前陈述：复用 `TaskSavedMessage`，由 `ToggleCompleteTaskViewModel`
> 单点写入 SQLite 并广播，两端各自从仓储重载列表。

---

## Why

快捷小窗勾选后，主窗口内存里仍是勾选前的 `TaskItem` 副本，必须点到其他项目再点回来才刷新。反向同样断开。

根因有两层：

1. 勾选路径不发 `TaskSavedMessage`，对端内存里仍是勾选前的 `TaskItem` 副本。
2. 仅换集合不够：`TaskItem` 无属性变更通知，勾选圈绑在实体上；`Clear`+`Add` 换成新行后，Avalonia `ItemsControl` 仍可能继续渲染旧行。所以对端看起来像“没刷新”，直到切换项目迫使可视树重建。

Attribution：`code wrong` —— 总线漏发；展示层把完成态绑在了无通知的实体上。

---

## What

- SQLite 是完成态的唯一数据源。广播只发生在 `ToggleCompleteTaskViewModel.ExecuteAsync`。
- 对端从仓储读回该任务，**就地**写入已在列表里的 `TaskRowViewModel.IsCompleted`（可观察），不换新行对象。
- 勾选圈与划线样式绑定 `IsCompleted`，不绑定 `Task.IsCompleted`。
- 发起方跳过自己的总线处理，避免与 `reloadTasks` 并发重建集合。
- 任务不在当前列表时才整表重载（例如小窗新建）。

## Non-goals

- 不改完成套件语义（勾选后仍留在列表）。
- 不把范围扩到删除、改期、改项目、主窗新建任务的跨窗同步。
- 不让任一窗口持有另一窗口 ViewModel 的强引用。

## Constraints and decisions

- Constitution Article 6：完成态只有 SQLite 一处权威；窗口列表是派生视图。
- `rule-code-standards` §2.1：跨窗必须 `WeakReferenceMessenger`。
- 用户原话要求两端都实时同步，且坚持单一数据源。

## Acceptance criteria

- [x] 小窗勾选后，主窗当前列表立刻显示同一任务的完成态（无需切换项目）。
- [x] 主窗勾选后，小窗当前列表立刻显示同一任务的完成态。
- [x] 勾选后任务仍留在两边列表中（完成套件不变）。
- [x] 自动化：双向各一条测试，断言对端列表来自仓储重载后的完成态。

## Change checklist

- [x] `ToggleCompleteTaskViewModel` 在 `SaveTaskAsync` 成功后发送 `TaskSavedMessage`
- [x] `QuickCaptureViewModel` 订阅 `TaskSavedMessage` 并从仓储重载当前项目列表
- [x] `MainViewModel` 对自发勾选抑制总线重入；暴露可等待的刷新任务供测试
- [x] 勾选圈绑定 `TaskRowViewModel.IsCompleted`；对端就地套用仓储读回的完成态
- [x] 双向单测断言对端仍是同一行实例且 `IsCompleted` 为真
- [x] 本 SPEC 与 `docs/specs/README.md` 索引

## Progress log

### 2026-09-27

- Completed: 首轮总线重载未驱动勾选圈；改为就地更新 `TaskRowViewModel.IsCompleted`，勾选圈改绑该属性；测试断言同一行实例。所有者预览通过（原话「没问题了」）；SPEC 收为 `[DONE]`。
- Decisions: 复用 `TaskSavedMessage`；写入点只在 `ToggleCompleteTaskViewModel`；对端不 `Clear` 列表。
- Current resume point: 已闭环，无未完成项。

## Verification

- Automated: `dotnet build` 0 警告 0 错误；`dotnet test` 199 通过（含 2 项跨窗勾选，断言同一行实例）
- Manual: 所有者 2026-09-27 预览通过（原话「没问题了」）
- Not run or not covered: 删除/改期/改项目的跨窗刷新（非本 SPEC）

## Risks and open questions

- Owner: 无未决问题。
- Blocker or trigger: 无。

## Lessons learned

- 单元测试断言 ViewModel 集合内容为真，不能证明 Avalonia 勾选圈已重绘。完成态必须是行 ViewModel 上的 `[ObservableProperty]`，对端只能改正在屏幕上的那一行。
- `TaskItem` 与 `HasDueDate` 是同一类坑：实体无通知，界面静默停留在旧状态。

## Related documents

- SPECs: `spec-quick-window-single-project-list`（小窗勾选能力；本 SPEC 补其跨窗缺口）
- Rules: `rule-code-standards` §2.1；`project-rules` 跨窗 WeakReferenceMessenger
- Analysis: None

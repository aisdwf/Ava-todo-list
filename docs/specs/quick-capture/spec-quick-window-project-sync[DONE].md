# spec-quick-window-project-sync: 主窗项目变更实时同步到小窗

## Metadata

- **ID**: spec-quick-window-project-sync
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

## Why

用户原话（2026-09-27，预览 `feature/quick-window-project-linkage` 时发现）：

> 「目前只有主窗口可以执行项目的加减，暂时可以保留这个设计，但是修改以后不会实时更新到小窗，
> 而是需要关闭再打开小窗才能更新修改以后的项目情况。tasks是可以实时刷新的」

根因（`design wrong`）：跨窗同步只有任务总线（`TaskSavedMessage` / `TaskDeletedMessage`），
项目写入没有对应消息。`CreateProjectViewModel` / `CommitRenameProjectViewModel` /
`ArchiveProjectViewModel` 落库后只刷新主窗自己；小窗项目下拉仅在 `PrepareAsync`（打开时）重读。
删除项目能同步，只是因为它顺带借用了 `TaskDeletedMessage`，属于偶然耦合。

在 [spec-quick-window-single-project-list](./spec-quick-window-single-project-list[DONE].md) §7 把下拉定为唯一项目上下文以后，这个缺口的影响变大了。

## What

- 新增 `ProjectsChangedMessage(Origin)`（Core）与 `ProjectChangeBus`（Desktop），与 `TaskChangeBus` 同模式。
- 主窗四个项目动作（新建 / 重命名 / 归档 / 删除）落库后广播。删除时先发任务消息、后发项目消息，
  使对端最终状态由项目刷新决定。
- `QuickCaptureViewModel` 订阅该消息：重读项目表，重建下拉并保留当前选中项；
  选中项已不存在（归档 / 删除）则回退 Default 并写回记忆项，然后重载任务列表。
  原先挂在 `TaskDeletedMessage` 上的项目刷新移到这里，任务消息只刷新列表。

## Non-goals

- 小窗内增删项目（用户：「暂时可以保留这个设计」）。
- 主窗订阅项目消息：目前只有主窗写项目，没有对端。

## Acceptance criteria

- [x] 主窗新建项目 → 已打开的小窗下拉立即出现，选中项不变
- [x] 主窗重命名 → 小窗下拉名称立即更新，选中项不变
- [x] 主窗归档 / 删除当前小窗选中的项目 → 小窗回退 Default，记忆项同步
- [ ] Owner 在 `preview/dev/FlowTask.exe` 中确认（owner 已明确指示本修复与 linkage 提交一并合入 dev，未单独预览 feature 分支）

## Change checklist

- [x] `FlowTask.Core/Messages/ProjectMessages.cs`、`FlowTask.Desktop/Services/ProjectChangeBus.cs`
- [x] `Create` / `CommitRename` / `Archive` / `ConfirmDelete` `ProjectViewModel` 增加 `origin` 并广播；`MainViewModel` 传 `this`
- [x] `QuickCaptureViewModel.Receive(ProjectsChangedMessage)`
- [x] `CrossWindowCompleteSyncTests` 新增 3 项（新建 / 重命名 / 归档）

## Progress log

### 2026-09-27

- Completed：上述全部代码与测试。
- Decisions：删除项目的消息顺序见 What。
- Owner 指示「提交以后修复了，两个提交一起合并到dev」，按该指示关闭并合入。
- Current resume point：无（已关闭）；人工确认在 dev 预览上进行，若不通过另开 bugfix。

## Verification

- Automated：`dotnet build` 0 警告 0 错误；`dotnet test` 272 通过。
  反证：把 `ProjectChangeBus.Changed` 置空后，新增 3 项与既有 `MainDeleteProject_...` 共 4 项失败，
  说明这些测试确实覆盖了该缺陷。
- Manual：小窗保持打开，在主窗新建 / 重命名 / 归档 / 删除项目，观察小窗下拉立即更新。

## Risks and open questions

- 无。

## Lessons learned

- 跨窗同步按实体分别建消息：项目表变更借用任务消息，只在删除场景偶然生效，新建和改名就漏掉了。

## Related documents

- SPECs：[spec-quick-window-single-project-list](./spec-quick-window-single-project-list[DONE].md)

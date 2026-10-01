# spec-inline-task-edit: 任务行就地编辑（取代底部编辑面板）

## Metadata

- **ID**: spec-inline-task-edit
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-29
- **Last Updated**: 2026-10-01

> **开工许可**：2026-09-29 owner 确认复述与 7 步计划，原话「开始任务」。

## Why

主窗点击任务标题会在行下方展开一块编辑面板（标题输入框 + P1–P3 单选 + 项目下拉 + 「完成编辑」）。
用户原话（2026-09-29）：

> 「优化一下修改内容的表现方式，直接内容我倾向通过双击，优先级，日期我觉得用类似目前日期的方式，
> 小窗或者比较合适的UI效果来修改比较好，目前底部弹出来一个框修改的形式太蠢了，也不方便。项目我倾向于不做修改。」

面板的问题：改一个字段要展开整块、再点「完成编辑」收起；列表被面板推开，布局跳动；
到期日早已改成贴边浮层（spec-due-date-picker），同一行里两种编辑形态并存。

Attribution：design wrong —— design-domain-contract §4.2 / §5.3 选择了「就地展开面板」，
用户实测判定为不方便。

## What

仅主窗任务行（owner 裁决「只改主窗」）：

| 字段 | 新交互 | 来源 |
| --- | --- | --- |
| 标题 | **双击**标题 → 原地变为输入框；Enter / 点击外部 = 保存，Esc = 取消；清空后提交保留原标题 | 用户原话「直接内容我倾向通过双击」；问答裁决「回车/点外面保存，Esc取消」 |
| 优先级 | 点击行上 P1/P2/P3 标签 → 标签下方贴边浮层列出 P1/P2/P3，点选即保存并关闭；Esc / 点外部取消 | 用户原话「用类似目前日期的方式，小窗…」；问答裁决「锚定小弹层，点选即生效」 |
| 到期日 | 不变（既有 `DueDatePicker`） | — |
| 所属项目 | **创建后不可改**；归属只在创建时决定 | 问答裁决「任务创建后不可改项目」 |

单击标题不再有动作（原「点击展开编辑」取消）。

## Non-goals

- 小窗任务行的标题 / 优先级编辑（owner：只改主窗）。
- 创建栏、小窗底栏的 P1–P3 分段选择（新建路径不变）。
- 为任务提供「移动到其他项目」的任何入口。
- `MainViewModel.AssignProjectAsync` 保留：它是测试与跨窗同步用例的指派入口，无 UI 绑定。

## Constraints and decisions

- TR-1：每个用户动作一个 Action 类 → `CommitRowTitleViewModel`、`CommitRowPriorityViewModel`。
- PersistThenWriteback：先写实体、落库失败回滚实体。
- 标题校验复用 `TaskTitle.IsValid` / `Normalize`（Article 6）。
- 优先级浮层结构对齐 `DueDatePicker`（Popup + 淡入位移、Esc 关闭、light dismiss），
  样式复用 `DuePickerSurface` 同一表面令牌，不新造第二套浮层外观。
- 点击外部提交沿用主窗 Tunnel `PointerPressed` 模式（与项目重命名一致，见 `MainWindow.OnWindowPointerPressed` 备注）。
- BR-1：「勾选完成，点标题编辑」是首启引导步骤 → 更新 `GuideCatalog`、`GuideScenes.EditAndComplete`，
  `OnboardingProgress.CurrentVersion` 2 → 3。
- REQUIREMENTS R-2.4 追加裁决记录（项目创建后不可改）。

## Acceptance criteria

- [x] 主窗双击任务标题出现输入框并已全选；Enter 保存；点击行外保存；Esc 恢复原标题。
- [x] 清空标题后提交，标题保持原值。
- [x] 点击 P 标签弹出 P1/P2/P3 浮层；点选后浮层关闭、标签即时更新、排序随之刷新；Esc / 点外部不改动。
- [x] 任务行下方不再出现编辑面板；主窗无任何改项目的入口。
- [x] 保存失败时实体回滚（单测）。
- [x] 操作指南与首启引导描述新手势；老用户会再看一次引导。
- [x] build 0 warning / 0 error；测试不低于 315。

## Staged plan

1. SPEC + REQUIREMENTS 裁决记录。
2. 标题双击编辑：`TaskRowViewModel` 标题编辑态 + `CommitRowTitleViewModel` + 视图。
3. `PriorityPicker` 控件 + `CommitRowPriorityViewModel`。
4. 移除编辑面板链路。
5. 测试改写。
6. 指南同步。
7. build + test + preview。

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md` R-2.4 裁决记录
- [x] `ViewModels/TaskRowViewModel.cs`：去掉 `IsEditing`/`EditTitle`/`EditPriority`/`EditProject`/`GroupName`/`BeginEdit`/`EndEdit` 与 `ProjectChoice` 类；加 `IsEditingTitle`/`TitleBuffer`/`BeginTitleEdit`/`EndTitleEdit` 与 `Title`/`Priority` 可通知投影
- [x] `ViewModels/Actions/CommitRowTitleViewModel.cs`（新增）
- [x] `ViewModels/Actions/BeginRowTitleEditViewModel.cs`（新增；先提交其他编辑行，再按 Id 取回重载后的行实例）
- [x] `ViewModels/Actions/CommitRowPriorityViewModel.cs`（新增）
- [x] `ViewModels/PriorityCommit.cs`（新增）
- [x] 删除 `ViewModels/Actions/ToggleEditTaskViewModel.cs`、`SaveEditTaskViewModel.cs`
- [x] `ViewModels/MainViewModel.cs`：去掉 `ToggleEdit`/`SaveEdit`/`ProjectChoices`；加 `BeginTitleEdit`/`CancelTitleEdit`/`CommitTitleEdit`/`CommitRowPriority`
- [x] `Controls/PriorityPicker.axaml(.cs)`（新增）
- [x] `Views/MainWindow.axaml`：标题 TextBlock + TextBox、优先级改为 PriorityPicker、删 EditPanel 与外层 StackPanel
- [x] `Views/MainWindow.axaml.cs`：标题双击 / 失焦 / 点外部提交
- [x] `Styles/EditorialStyles.axaml`：删 `TitleTrigger`/`EditPanel`（`FieldLabel`/`FieldInput` 设置页与侧栏仍在用，保留）；加 `TitleInput`/`PriorityTrigger`/`PriorityPickerSurface`/`PriorityOption`/`PriorityOptionText`
- [x] 测试：`MainViewModelTests`、`ProjectInteractionTests`、`PersistThenWritebackTests` 改写；新增 `PriorityPickerTests`
- [x] Guide: updated 「勾选完成，双击改名」(was 「勾选完成，点标题编辑」) + 「到期日与优先级」extra line; EditAndComplete scene redrawn; OnboardingProgress.CurrentVersion 2 → 3

## Progress log

### 2026-09-29

- Completed: owner 问答裁决 4 项；worktree `feature/inline-task-edit` 建立；SPEC 创建；Phase 1–7 全部完成，preview 已发布。
- Decisions: 见 What 表。实施中追加：提交标题时值未变则不落库、不广播（点外部会频繁触发空提交）。
- Found during tests: `BeginTitleEdit_CommitsOtherEditingRow` 首跑失败 —— 提交前一行触发整表重载，行实例重建，
  编辑态落在旧实例上，界面不会出现输入框。修正：`BeginRowTitleEditViewModel` 提交后按 Task.Id 取回当前实例；
  视图侧聚焦也改为按编辑态重新查找输入框，而非沿用事件 sender。
- Current resume point: none — closed.

### 2026-10-01

- Completed: owner 预览通过，原话「合入dev」。SPEC 转 `[DONE]`，随代码在任务分支提交后合入 `dev`。
- Decisions: 本分支创建后 `dev` 已合入 spec-theme-bound-decoration-colors（移除 `Project.ColorHex`、项目色条改为优先级色条），
  与本分支在 `TaskRowViewModel` / `MainWindow.axaml` 重叠，合并冲突在 `dev` 内按两者并存解决。

## Verification

- Automated: `dotnet build FlowTask.sln` 0 warning / 0 error；`dotnet test` 333 通过（基线 315），2026-09-29 实测。
- Manual: owner 预览 `preview/feature/inline-task-edit/FlowTask.exe`。
- Not run or not covered: 双击、点击外部提交、浮层位置与动效无 UI 自动化，依赖 owner 预览。

## Risks and open questions

- 双击与单击：标题区域不再有单击动作，双击的第一击不会误触任何操作。
- 已放错项目的任务只能删除重建（owner 已知晓此代价并选择）。

## Lessons learned

- `LoadTasksAsync` 以 Clear+Add 重建行实例：任何「先提交再改行状态」的动作都必须在提交后按 Id 重新取行，
  不能持有提交前的引用。

## Related documents

- SPECs: [spec-due-date-picker](../task-domain/spec-due-date-picker[DONE].md), [spec-onboarding-guide](./spec-onboarding-guide[DONE].md), [spec-classification-ui](./spec-classification-ui[DONE].md)
- Rules: `docs/rules/project-rules.md` BR-1, `docs/rules/technical-rules.md` TR-1, `docs/rules/rule-no-invented-user-behavior.md`
- Requirements: `docs/requirements/REQUIREMENTS.md` R-2.4

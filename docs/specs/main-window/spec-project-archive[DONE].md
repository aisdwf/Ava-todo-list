# spec-project-archive: 项目行归档入口与「设置 → 已归档的项目」

## Metadata

- **ID**: spec-project-archive
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-10-01
- **Last Updated**: 2026-10-24

## Why

`ArchiveProjectCommand` / `SetArchivedAsync` 已实现并有测试，但没有任何 UI 入口（`TODO(archive-ui)`，期限 2026-10-12，见 spec-classification-ui §6）。
用户只能删除项目，而删除会级联物理删除任务（R-2.7）。「项目做完了但不想丢记录」没有出路。

另：现有「全部任务」看板与计数**包含**已归档项目下的任务（`MainViewModel.LoadTasksAsync` 注释、`GetAllActiveTasksAsync` 不按项目过滤），与本轮裁决相反，须一并修正。

## What

所有者原话（2026-10-01）：

- 入口：「项目行上，但是需要远离delete以免误触，我倾向于鼠标在项目行上将左侧点变为归档」。
- 「归档不能，归档后不出现在全部任务，小窗看不到，不确认。」（「归档不能」= Default 不能归档）
- 「设置提供恢复和删除，注意做区分和防误触」；位置「可在设置-已归档的项目」。

1. **侧边栏项目行**：悬停整行时，左侧 8px 色点淡出，同位置淡入归档按钮（`IconArchive`，14px，三级文字色）；点击即归档，不弹确认。Default 行悬停仍只显示色点。
2. **归档后的可见性**：从侧边栏、「全部任务」列表、「全部任务」计数、小窗项目下拉中消失；任务归属不变。若当前正选中该项目，回到「全部任务」；小窗记忆项为该项目时回落 Default（现有逻辑）。这四处是隐藏范围。命令行 `list` 不在其中：它仍返回已归档项目下的未删除任务。
3. **设置 → 已归档的项目**（新 `SettingsSection.Archived`）：列出已归档项目（名称 + 任务数）。左侧导航顺序：外观 → 通用 → 命令行 → 已归档的项目 → 操作指南 → 关于。「数据」属于 spec-json-export，本 SPEC 不插入该项。每行两个操作：
   - 「恢复」：普通文字按钮，紧跟名称右侧；点击即恢复，回到侧边栏原排序位置。
   - 「删除」：危险色描边按钮，固定在行最右端；与「恢复」之间由弹性空白隔开。点击走现有级联删除确认弹窗（写明将删除的任务数）。
   - 空列表时显示一行说明文字。
4. **仓储**：`GetAllActiveTasksAsync`、`CountActiveTasksAsync` 排除已归档项目下的任务；`GetArchivedProjectsAsync` 作为已归档项目的单一真源。命令行 `list` 改走 `GetAllNonDeletedTasksAsync`，仍返回这些任务。
5. **操作指南**（BR-1）：「用项目归类」补一条要点说明悬停圆点归档；「删除项目」补一条「已归档项目在设置 → 已归档的项目里恢复或删除」。场景 `Projects` 增加悬停色点变归档图标的一帧。

## Non-goals

- 不归档单条任务（R-4.3 无全局任务归档）。
- 不做归档确认、撤销提示。
- 不支持在已归档项目里查看或编辑任务。
- 不改项目排序（拖拽排序另立项）。

## Constraints and decisions

- R-2.7：删除仍为级联物理删除，确认弹窗复用 `RequestDeleteProjectViewModel` / `ConfirmDeleteProjectViewModel`，不另写一套。
- TR-1：新命令放 `ViewModels/Actions/`（`RestoreProjectViewModel`）。
- Article 6：「哪些任务算全部任务」只在仓储查询一处定义；视图不做二次过滤。
- R-4.3（无全局任务归档）与 2026-10-01「不做回收站」仍然有效：本 SPEC 不重开任务级归档，也不把删除改成回收站。
- design-visual-language §7：侧边栏归档按钮用已合入 dev 的 `IconArchive`。`IconRestore` 已在同一份 `spec-icon-refresh[DONE]` 落地。设置里的「恢复」仍是普通文字按钮。实施不再等待 icon-refresh。
- 本工作树已从 `149e879` 快进到当前 dev（`fa35f77`），不在旧提交上合并或变基。
- **已裁决（2026-10-24，按提案开工）**：设置左侧顺序为外观 → 通用 → 命令行 → 已归档的项目 → 操作指南 → 关于。提案里的「数据」属于 spec-json-export，本 SPEC 不拥有、不插入。
- **已裁决（2026-10-24，按提案开工）**：命令行 `list` 仍返回已归档项目下的任务。归档只隐藏侧边栏、「全部任务」列表与计数、小窗项目下拉。拆法：看板与计数走 `GetAllActiveTasksAsync` / `CountActiveTasksAsync`；`list` 走 `GetAllNonDeletedTasksAsync`。`add` 省略项目时仍落入 Default。命令行不归档、不删除项目。
- **[推断]** 色点与归档按钮共用 22px 宽的固定槽位，切换时不改变行布局。依据：现有删除槽位同样固定占位（`MainWindow.axaml:120` 注释），避免悬停时名称横跳。
- **[推断]** 点击归档按钮不触发整行的选中（`Tapped`），需在按钮上标记事件已处理。依据：否则归档的同时会先切换到该项目再回落，造成一次闪烁。

## Acceptance criteria

- [x] 悬停非 Default 项目行，左侧色点变为归档图标；离开恢复色点；点击即归档且无弹窗。
- [x] Default 行悬停不出现归档图标。
- [x] 归档后：侧边栏、全部任务列表与计数、小窗下拉均不含该项目及其任务。
- [x] 归档正选中的项目后回到「全部任务」。
- [x] 设置 → 已归档的项目：恢复后项目回到侧边栏原位置，其任务重新出现在全部任务。
- [x] 删除按钮在行最右、危险色，点击弹出级联确认；取消不删除。
- [x] 操作指南文案与场景已更新。
- [x] build 0/0；test 410 通过（高于基线 315）。所有者预览确认（2026-10-24「没问题，合入dev」）。

## Staged plan

1. 从当前 dev 开工（本工作树停在 `149e879`，不在该提交上合并或变基）。仓储：全部任务与计数排除已归档项目；补测试。
2. 侧边栏：色点/归档按钮槽位与悬停样式；接 `ArchiveProjectCommand`。
3. 设置页「已归档的项目」：VM 列表、`RestoreProjectViewModel`、删除复用确认弹窗。
4. 操作指南文案与场景。
5. build + test + 预览。

## Change checklist

- [x] `src/FlowTask.Infrastructure/Persistence/SqliteTaskRepository.cs`（全部任务 / 计数过滤）
- [x] `src/FlowTask.Core/Interfaces/ITaskRepository.cs` / `IProjectRepository.cs` 文档注释
- [x] `src/FlowTask.Desktop/ViewModels/SettingsSection.cs`、`MainViewModel.cs`
- [x] `src/FlowTask.Desktop/ViewModels/Actions/RestoreProjectViewModel.cs`（新）
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml(.cs)`、`Styles/EditorialStyles.axaml`
- [x] `src/FlowTask.Desktop/ViewModels/GuideCatalog.cs`、`Views/Guide/GuideScenes.cs`
- [x] `tests/`：仓储过滤、归档/恢复/删除 VM 测试
- [x] 核销 `TODO(archive-ui)`（spec-classification-ui、spec-sidebar-selection-consolidation）
- [x] Guide: updated 用项目归类、删除项目

## Progress log

### 2026-10-01

- Completed: 所有者裁决入口、可见性、不确认、设置页恢复/删除。
- Decisions: 见 What 原话。
- Current resume point: 等所有者确认本 SPEC → `[IN-PROGRESS]`。

### 2026-10-24

- Completed: 对照已合入 dev 的 icon-refresh 与命令行，修订本草稿供审核。未改代码。
- Decisions: 无新的所有者裁决。icon-refresh 已在 dev，`IconArchive` / `IconRestore` 可用，撤掉「实施前先合入 icon-refresh」。本工作树仍在 `149e879`，实施须从当前 dev 起。设置顺序与 `list` 对已归档项目任务的返回，记为待本次审核，不是裁决。
- Current resume point: 等所有者审核上述两项提案，并确认本 SPEC。

### 2026-10-24 实施

- Completed: 从 `fa35f77` 开工。看板与计数排除已归档项目；`list` 改走 `GetAllNonDeletedTasksAsync`。侧栏悬停归档、设置页恢复与级联删除、操作指南已接上。
- Decisions: 所有者指示按文中两项提案执行。导航不插入「数据」。`list` 与看板查询拆开。操作指南 Summary 未改，首次引导版本保持 3。
- Current resume point: 等所有者预览。不要提交，不要合入 dev。
- Subagent/task references, when used: 无。

### 2026-10-24 合入

- Completed: 所有者预览通过（「没问题，合入dev」）。SPEC 标 `[DONE]`。
- Decisions: 无新裁决。
- Current resume point: 提交任务分支并合入 dev。不推远程。

## Verification

- Automated: 仓储过滤与 VM 单测；`GuideSceneRenderTests`、`OnboardingTests`。
- Manual: 所有者于 2026-10-24 预览通过（归档、恢复、删除与小窗下拉）。
- Not run or not covered: 悬停淡入淡出动效无自动化。

## Risks and open questions

- Owner: aisdwf
- Blocker or trigger: 无。icon-refresh 已在 dev。
- 2026-10-24 所有者按提案裁定了设置导航顺序和 `list` 的返回范围，见 Constraints。
- 首次引导「项目」步锚点为整块侧栏，结构不变，tour 版本预计不升；若实施时 Summary 文案需改则升版。

## Lessons learned

看板「全部任务」和命令行 `list` 不能共用一次查询：前者要藏起已归档项目下的任务，后者是一次显式读取，仍要返回它们。

## Related documents

- SPECs: spec-classification-ui（`TODO(archive-ui)` 来源）、spec-delete-project-cascade-tasks、[spec-icon-refresh](../visual-theme/spec-icon-refresh[DONE].md)、[spec-cli-add-delete](../cli/spec-cli-add-delete[DONE].md)
- ADRs: None
- Rules: project-rules BR-1、technical-rules TR-1、rule-no-invented-user-behavior
- Analysis: None

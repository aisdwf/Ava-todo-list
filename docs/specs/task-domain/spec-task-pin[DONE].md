# spec-task-pin: 任务置顶（主窗与小窗）

## Metadata

- **ID**: spec-task-pin
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-10-01
- **Last Updated**: 2026-10-24

## Why

任务排序完全由 `TaskListOrder` 决定（未完成：优先级 → 到期 → 创建时间倒序）。用户无法把「突然非常重要」的事临时顶到最上面。

所有者原话（2026-10-01）：「tasks有自己的排序方式，不过加置顶感觉还是有必要的，对于突然非常重要的内容需要放在顶部的情况，给了用户一定空间，也不太容易打破目前的排序手段」。

## What

所有者裁决（2026-10-01）：

- 「最后置顶的排上面」。
- 「自动取消置顶」（勾选完成时）。
- 主窗：「放在优先级下方、项目左边的空间，选中tasks时（鼠标悬浮）才出现按钮，置顶后常驻显示」。
- 小窗：「置顶小窗一起变，按钮放在小窗设置日期左边，同样悬浮的时候才出现置顶按钮，已置顶常驻显示」。

1. **数据**：`TaskItem` 新增 `IsPinned`（bool）与 `PinnedAt`（UTC 时刻，可空）。sqlite-net `CreateTableAsync` 自动加列，老库默认未置顶。
2. **排序**（`TaskListOrder`，主窗与小窗共用）：未完成组内先比「是否置顶」，置顶者按 `PinnedAt` 倒序；非置顶者沿用原规则。已完成组不变。
3. **完成联动**：勾选完成时同一次写入清除 `IsPinned` / `PinnedAt`；取消勾选不恢复。
4. **主窗任务行**：14px 置顶按钮（`IconPin`）仍放在 P 标签正下方，不挪位。对照当前 dev：P 标签在 `Grid.Column="2"` `Grid.Row="0"`；其下 `Grid.Column="2"` `Grid.Row="1"` 为空，按钮放这一格（与元信息同一行、位于其左）。CLI 标记在元信息行（`Grid.Column="3"` `Grid.Row="1"`）里，不占图钉槽。未置顶：悬停整行时浮现、三级文字色；已置顶：常驻、强调色。点击切换。已完成行不显示。图钉右侧的元信息（dev 现状，不改置顶规则）：未完成仍是到期选择器，并另附创建日；已完成隐藏到期选择器，也不再另附创建日，改为 `MM/dd~MM/dd 已完成`（创建日本地日~完成日本地日，同一天仍写全区间）；缺 `CompletedAt` 时为 `MM/dd 已完成`（只有创建日）。到期日仍留在库中。勾选完成仍在同一次保存中清除置顶。
5. **小窗任务行**：同一按钮仍紧贴行内到期日控件左侧，显隐规则同主窗。对照当前 dev：CLI 徽章在标题旁边，到期控件仍是最右一列；已完成时该列改为上一条的完成文案，不显示图钉。
6. **跨窗同步**：沿用 `TaskChangeBus.Saved`，另一窗口重载后顺序一致。
7. **操作指南**（BR-1）：「勾选完成，双击改名」补要点「悬停任务，点 P 标签下方的图钉置顶；完成后自动取消」；「小窗键位」补要点说明小窗图钉位置。场景 `EditAndComplete` 加图钉一帧。

## Non-goals

- 不做手动拖拽排序。
- 不加置顶快捷键。
- 已完成任务不能置顶。
- 不限制置顶数量。

## Constraints and decisions

- Article 6：排序只在 `TaskListOrder` 一处；仓储与两个窗口不得各写一套置顶规则。
- Article 9：`PinnedAt` 经 `IClock.UtcNow` 赋值。
- TR-1：`TogglePinTaskViewModel` 放 `ViewModels/Actions/`，主窗与小窗共用（同 `ToggleCompleteTaskViewModel` 做法）。
- 完成时清除置顶写在 `ToggleCompleteTaskViewModel` 内，与 `CompletedAt` 同一次 `SaveTaskAsync`，不另发一次写入。
- `spec-icon-refresh` 已合入 dev，`Icons.axaml` 已有 `IconPin`。不再把「实施前先合入 icon-refresh」当作前置。
- 所有者 2026-10-24 确认开工基线：从当前 dev 起，不从 `149e879` 改。本工作区已快进到 `fa35f77`。
- 所有者 2026-10-24 确认导出衔接按草稿提议：本分支先于 spec-json-export 落地置顶列，备份导出含 `isPinned` / `pinnedAt`。dev 上尚无该导出器，本分支不实现导出，也不改 spec-json-export 与 spec-project-archive；后合入导出的人按此带上这两字段。CLI `list` 不增加它们。
- **[推断]** 置顶按钮用固定尺寸槽位，未置顶时只改透明度不改布局。依据：与现有 `RowAction` 删除按钮一致（`EditorialStyles.axaml:385`），悬停时行高与元信息行不跳动。

## Acceptance criteria

- [x] 主窗：悬停未完成行时，P 标签下方空位（`Grid.Column="2"` `Grid.Row="1"`）出现灰色图钉；点击后变强调色常驻，任务移到列表顶部。CLI 标记仍在元信息行，不占此格。
- [x] 多个置顶任务中，最后置顶的在最上面。
- [x] 再点一次取消置顶，任务回到原规则位置。
- [x] 勾选置顶任务：划线置底且图钉消失；取消勾选后不再置顶。已完成行元信息为「MM/dd~MM/dd 已完成」（缺 `CompletedAt` 则为「MM/dd 已完成」），不显示到期选择器、单独创建日或图钉；到期日仍在库中。
- [x] 小窗：图钉在行内日期左侧（CLI 徽章在标题旁，日期控件仍在最右列），显隐与主窗一致；两窗顺序一致且互相同步。已完成行该列显示完成文案，不显示图钉。
- [x] 老数据库升级后所有任务为未置顶，无启动错误。
- [x] 操作指南已更新。
- [x] build 0/0；test 不低于基线；所有者预览确认。

## Staged plan

1. Core：`TaskItem` 两列；`TaskListOrder` 置顶比较；单测。
2. Action：`TogglePinTaskViewModel`；`ToggleCompleteTaskViewModel` 清除置顶；单测。
3. 主窗行布局与样式。
4. 小窗行布局与样式。
5. 操作指南文案与场景；build + test + 预览。

## Change checklist

- [x] `src/FlowTask.Core/Models/TaskItem.cs`
- [x] `src/FlowTask.Core/Ordering/TaskListOrder.cs`
- [x] `src/FlowTask.Desktop/ViewModels/Actions/TogglePinTaskViewModel.cs`（新）、`ToggleCompleteTaskViewModel.cs`
- [x] `src/FlowTask.Desktop/ViewModels/TaskRowViewModel.cs`（`IsPinned` 投影）、`MainViewModel.cs`、`QuickCaptureViewModel.cs`
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml`、`QuickCaptureWindow.axaml`、`Styles/EditorialStyles.axaml`
- [x] `src/FlowTask.Desktop/ViewModels/GuideCatalog.cs`、`Views/Guide/GuideScenes.cs`
- [x] `tests/`：排序、置顶切换、完成清除置顶、老库加列
- [x] `docs/design/design-domain-contract.md` 字段表补 `IsPinned` / `PinnedAt`
- [x] Guide: updated 勾选完成，双击改名；小窗键位

## Progress log

### 2026-10-01

- Completed: 所有者裁决排序、完成联动、主窗与小窗按钮位置。
- Current resume point: 等所有者确认本 SPEC → `[IN-PROGRESS]`；实施前先合入 icon-refresh。

### 2026-10-24

- Completed: 按当前 dev 修订布局描述。icon-refresh 已合入，`IconPin` 已在 `Icons.axaml`，去掉「实施前先合入 icon-refresh」。主窗 `Grid.Column="2"` `Grid.Row="1"` 仍空，CLI 标记在元信息行；小窗 CLI 徽章在标题旁，到期控件仍在最右列，图钉位置不变。记下已完成行元信息为创建日~完成日「已完成」（缺完成时刻则只有创建日），到期日仍保留；置顶规则未改。
- Decisions: 无新的所有者裁决。json 导出字段衔接，以及「从当前 dev 而不是 `149e879` 开工」，标为待本次审核。
- Current resume point: 等所有者审核本次修订；确认前保持 `[DRAFT]`，不实施。2026-10-01 日志里的「实施前先合入 icon-refresh」已失效。

### 2026-10-24 开工

- Decisions: 所有者确认两处待审核项，按文中提议执行。工作区快进到 dev `fa35f77`。导出衔接：本分支先落地置顶列并写入领域契约；不改 spec-json-export 与 spec-project-archive，不在本分支写导出器。后合入的备份导出带 `isPinned` / `pinnedAt`。
- Current resume point: 按 Staged plan 实施。

### 2026-10-24 实现

- Completed: 置顶列、排序、完成时清除、主窗与小窗图钉、操作指南要点与 `EditAndComplete` 一帧。build 0 警告 / 0 错误；test 414 通过。未提交，未合入 dev。
- Current resume point: 等所有者预览。验收项仍未勾，预览确认前保持 `[IN-PROGRESS]`。

### 2026-10-24 关闭

- Completed: 所有者预览通过（「没问题，推到dev」）。验收项勾选，SPEC 改为 `[DONE]`。
- Current resume point: 已关闭。合入本地 `dev`，不推送远程。

### 2026-10-24 合入

- Decisions: JSON 导出已先在 `dev`。按确认过的衔接，后合入者补上备份字段 `isPinned` / `pinnedAt`。未改 spec-json-export 与 spec-project-archive。CLI `list` 仍不含这两字段。
- Current resume point: 已关闭。

## Verification

- Automated: build 0 警告 / 0 错误。`dotnet test` 414 通过，含 `TaskListOrder`（置顶排在更高优先级之前、最后置顶在最上、已完成组忽略置顶）、`TaskPinTests`（打戳、再点清除、已完成忽略、完成清除且取消完成不恢复、保存失败回滚）、老库无置顶列补列。
- Manual: 所有者 2026-10-24 预览 `preview/feature/task-pin/FlowTask.exe` 后确认通过。
- Not run or not covered: 悬停显隐动效。

## Risks and open questions

- Owner: aisdwf
- 「勾选完成，双击改名」是首次引导步骤；若 Summary 改写需升 `OnboardingProgress.CurrentVersion`。当前计划只改 Points，不升版。

## Lessons learned

排序只写在 `TaskListOrder`。完成时清除置顶必须和 `CompletedAt` 同一次 `SaveTaskAsync`，否则对端窗口会先看到已完成仍置顶。

## Related documents

- SPECs: spec-project-managed-tasks（完成套件与排序来源）、[spec-icon-refresh](../visual-theme/spec-icon-refresh[DONE].md)（`IconPin` 已在 dev）、spec-json-export（仍为 draft，字段衔接见 Constraints）
- ADRs: None
- Rules: project-rules BR-1、technical-rules TR-1、AI_CONSTITUTION Article 6 / 9
- Analysis: design-domain-contract

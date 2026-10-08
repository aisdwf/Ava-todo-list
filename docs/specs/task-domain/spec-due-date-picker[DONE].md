# spec-due-date-picker: 统一到期日选择器（贴边浮层）、小窗日期能力、任务行双行布局

## Metadata

- **ID**: spec-due-date-picker
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-29
- **Last Updated**: 2026-09-29

---

## Why

**用户本轮原话（2026-09-29）**：

> 「主界面选择时间不是很优雅，日历展开感觉违背了 ui 不能突变的原则，突然展开了一块。日期交互也明显入口过多，实际功能不明确」
>
> 「小窗没有设置日期的能力，也看不到日期」
>
> 「主界面的单项内容中，总体的风格问题不大，但是感觉中间的空间利用的不是很好，能做什么优化吗？」

现状（代码核实，路径相对 `src/FlowTask.Desktop/`）：

| # | 现状 | 问题 |
| :--- | :--- | :--- |
| 1 | 创建栏 `Calendar` 用 `IsVisible` 直接切换（`Views/MainWindow.axaml:301-332`），无过渡 | 展开瞬间把下方列表整体下推 = 布局突变 |
| 2 | 创建栏同时有「启用默认到期 / 清除 / 日历 / 收起日历 / 数字框 / 结果文字」；行上改期是另一套全屏遮罩卡片（`MainWindow.axaml:676-726`）再加「取消 / 完成」 | 同一件事（选一天或清空）6 个控件、两份手抄 XAML；「启用默认到期」实为一次性赋值却叫「启用」 |
| 3 | 数字框逐键解析（`DueDateEditorViewModel.UpdateDigitInput`），输 `10` 会先选中 1 日 | 中间态被当成结果 |
| 4 | 小窗行模板只有勾选 + 标题（`Views/QuickCaptureWindow.axaml:89-111`），`SaveCommand` 固定 `dueDate: null` | 看不到也设不了日期 |
| 5 | 任务行 7 列 `Auto,Auto,Auto,*,Auto,Auto,Auto`（`MainWindow.axaml:389-544`）；标题列 `*` 只有一行 19px 文本，到期与创建日期挤在最右 | 中间大片空白，视线需左右横跳 |
| 6 | 创建日期 `Task.CreatedAt` 以 `StringFormat MM/dd` 直接格式化 UTC 值 | [待验证] 本地凌晨时段显示成前一天 |

**Attribution**: `design wrong` —— spec-due-date-calendar 把「三来源」理解成「三组常驻控件」，未约束展开方式与入口数量；小窗日期被显式排除；任务行布局未考虑信息密度。

---

## What

### A. 统一到期日选择器 `DueDatePicker`（主窗创建栏、主窗任务行、小窗三处共用）

**用户裁决（2026-09-29）**：选择「一个按钮 + 贴边浮层，点击日期即生效」方案。

- **入口唯一**：一个紧凑触发按钮（chip）。无值显示「到期」占位，有值显示相对文案（复用 `DueDateTextConverter`：今天到期 / 明天到期 / N 天后 / 已逾期…）及对应颜色。
- **浮层**：Avalonia `Popup`，锚定触发按钮下沿，`IsLightDismissEnabled`；悬浮于内容之上，**不参与布局**，不推动任何元素。
- **过渡**：浮层内容 Opacity 0→1 + 轻微下移归位（约 160ms，CubicEaseOut），沿用 `design-visual-language` §5.1 的 Transitions 做法（Avalonia 11.2 `Animation` 不支持 `RenderTransform`）。
- **浮层内容（自上而下）**：
  1. ~~数字输入框（自动聚焦）：实时预览，按 `Enter` 提交~~ —— **H1（2026-09-29）用户裁决移除**，见 Progress log。
  2. 快捷行：「默认 +N 天」（替代「启用默认到期」文案，N 来自设置）、「清除」。点击即提交并关闭。
  3. `Calendar`（打开时获得焦点）：**点击某一天即提交并关闭**；键盘方向键只移动高亮，`Enter` 提交高亮日。
- **取消**：`Esc` / 点击浮层外 = 不改动关闭。无「完成 / 取消」按钮。
- **删除**：创建栏的整块日期控件组、「日历 / 收起日历」双按钮、全窗遮罩改期卡片（`IsDueDatePopupOpen` 及其遮罩）、未用的 `DueDateEditConverter`。
- **提交语义**：创建栏 = 只写入待创建值；主窗/小窗任务行 = 持久化（沿用 `CommitDueDatePopupViewModel` 的保存 + 广播 + 失败回滚）。

### B. 小窗日期能力

**用户裁决（2026-09-29）**：「显示 + 新建时可设 + 行内可改」。

- **用户裁决（2026-09-29）**：「这个功能不可能完全依赖快捷键，绝对需要有直接点击的入口」；具体显隐形态「要看实际的效果才能决定」。
- 底栏 P1/P2/P3 旁加一个**常驻可见、可直接点击**的 `DueDatePicker` 按钮，新建任务带入其值；保存后**重置为空**（与主窗创建栏一致）。
- 行模板变为 `勾选 | 标题 | 到期 chip`。有到期日时 chip 常驻且可点击修改；无到期日时 chip **首版**为悬停整行才显示「到期」占位（同样可点击）。**[推断]** 依据：小窗 420px 宽，常驻占位会压缩标题；依 design §7.4「各元素独立控制显隐」。形态以你预览为准，可改为常驻。
- 键盘：`Ctrl+D` 作为**补充**快捷方式打开底栏选择器（满足 R-1.4 全键盘），不替代点击入口。
- 浮层内 `Esc` 只关浮层，不隐藏小窗。
- 跨窗同步：沿用现有 `WeakReferenceMessenger` 保存广播，主窗与小窗互相刷新。

### C. 主窗任务行双行布局

**用户裁决（2026-09-29）**：「双行：标题 + 元信息行」。

```
[○] [色条] P1  标题标题标题标题（可换行）                                   [×]
               ● 项目名 · 明天到期 · 创建 09/29
```

- 第 1 行：勾选圈、项目色条、优先级、标题（`*`）、删除按钮（悬停浮现，沿用）。
- 第 2 行（元信息，Caption 11.5px，三级文字色），紧贴标题下方，与标题左对齐：
  - 项目名（有项目时显示，带项目色点）；
  - 到期 `DueDatePicker`（**常驻**，无值时为「到期」占位，保证改期入口可发现，design §2.3）；
  - 创建日期（转本地时间后格式化）。
  - 分隔符 `·` 仅在相邻两项都可见时出现。
- 行上下内边距 20 → **18**。原计划约 14，实施时发现 design-visual-language §1.3 约束「垂直内边距 18–24px」，取下限而不突破 design。
- 标题点击展开 `EditPanel` 的行为本轮不变。

### 文档同步

- `docs/requirements/REQUIREMENTS.md`：新增 R-1.10「小窗可查看并设置/修改任务到期日」，附本轮用户原话。
- `docs/design/design-interaction-principles.md`：
  - §2.2 增补：锚定触发元素、非模态、轻触外部即关闭的浮层**不属于**「跳出式交互」；模态对话框 / 全窗遮罩仍禁止（见「Risks」Q1，需你确认此解释）。
  - §4.3 快捷预设改为「默认 +N 天 / 清除」（与 spec-due-date-calendar 的裁决对齐，消除现存漂移）。
  - §8 删除「小窗不引入日期配置」，改为引用 R-1.10。
- `docs/design/design-visual-language.md` §5：新增「布局不突变」原则——展开/弹出类交互不得推动既有布局，出现与消失需过渡（用户原话）。
- `spec-due-date-calendar[DONE]`：在 §2.4 / §2.7 小窗非目标处加指向本 SPEC 的说明（不改历史裁决原文）。

---

## Non-goals

- 时刻精度（几点几分）：`DueDate` 仍为日历日（design-domain-contract §3）。
- 提醒、重复任务（REQUIREMENTS §6）。
- 「今天 / 明天 / 本周末」等额外预设（用户本轮选择不加）。
- `EditPanel`（标题/优先级/项目编辑）重构。
- 小窗「今日聚焦」视图（`TODO(quick-capture-today)` 不在本轮；2026-10-01 已关闭为不做）。

---

## Constraints and decisions

- TR-1：新增的提交/打开动作若超过阈值，拆为 `ViewModels/Actions/` 下独立类；不得扩大 `MainViewModel` 既有超长方法。
- Article 6：相对文案只走 `DueDateTextConverter`；默认偏移只来自 `AppSettings.default_due_offset_days`。（原「日期解析只走 `DueDateParser`」随 H1 数字输入移除而失效。）
- Article 9：所有「今天」经 `IClock` / `DueDateDisplay.Today`。
- 跨窗通信只用 `WeakReferenceMessenger`，小窗不持有主窗 VM。
- design-visual-language：新控件只用既有令牌（`CaptionText`、`TextTertiaryBrush`、`AccentBrush`、`FloatingSurfaceBrush`、`ControlCornerRadius` 等），不引入新色值。

---

## Acceptance criteria

- [x] 主窗创建栏只剩一个日期触发按钮；打开/关闭浮层时列表不发生位移，浮层有淡入过渡。
- [x] 浮层内：点击日历日、日历上 `Enter`、「默认 +N 天」、「清除」均一步生效并关闭；`Esc` / 点外部不改动。
- [x] 方向键只移动日历高亮，未按 `Enter` 前不提交；浮层内无文本输入（H1）。
- [x] 主窗任务行点击到期 chip 在旁边弹出同一浮层；全窗遮罩改期卡片已不存在。
- [x] 小窗行显示相对到期文案（颜色规则同主窗），可点击修改并持久化，主窗同步刷新。
- [x] 小窗 `Ctrl+D` 打开底栏选择器；新建任务带上所选日期；保存后选择器清空。
- [x] 主窗任务行为双行：元信息行含项目、到期、创建日期；无项目时项目段与分隔符不出现。
- [x] 创建日期按本地时间显示。
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不低于 272 且新增用例全绿（285，2026-09-29）。
- [x] 文档同步项全部完成。

---

## Staged plan

1. **文档**：REQUIREMENTS R-1.10、design §2.2 / §4.3 / §8、visual-language §5、spec-due-date-calendar 指向说明。
2. **选择器核心**：`DueDateEditorViewModel` 增加「预览 vs 提交」语义与 `Committed` 通知；新建 `Controls/DueDatePicker`（触发按钮 + Popup + 过渡）；单测。
3. **主窗接入**：创建栏替换为 `DueDatePicker`；任务行 chip 接入并持久化；删除遮罩卡片、双按钮、死代码与相关状态；更新既有测试。
4. **小窗接入**：行内到期 chip（可改）、底栏选择器、`Ctrl+D`、保存带日期；单测。
5. **任务行双行布局**：元信息行、内边距、创建日期本地化；测试。
6. **验证**：build + test；发布 preview；你人工验收。

---

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md`（R-1.10）
- [x] `docs/design/design-interaction-principles.md`（§2.2 贴边浮层、§2.4 布局不突变、§2.5 入口唯一、§4.3 预设、§8 小窗到期）
- [x] `docs/design/design-visual-language.md`（§5.3）
- [x] `docs/specs/task-domain/spec-due-date-calendar[DONE].md`（仅加指向）
- [x] `src/FlowTask.Desktop/ViewModels/DueDateEditorViewModel.cs`（预览 / 提交分离、`Committed`；删 `IsCalendarExpanded` / `ToggleCalendar` / `EnableDefaultDue`→`ApplyDefaultDue`）
- [x] `src/FlowTask.Desktop/ViewModels/DueDateCommit.cs`（新增）
- [x] `src/FlowTask.Desktop/Controls/DueDatePicker.axaml(.cs)`（新增）
- [x] `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`、`Icons.axaml`（`IconCalendar`）、`Tokens.Shared.axaml`（`TaskRowPadding` 20→18）
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml`（创建栏单入口；任务行双行；删全窗遮罩改期卡片）
- [x] `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`（删 `IsDueDatePopupOpen` / `EditingDueDateTarget` / 三条弹层命令；加 `RowDueDateEditor` / `NewTaskDueDate` / `CommitRowDueDate`）
- [x] `src/FlowTask.Desktop/ViewModels/Actions/`：删 `OpenDueDatePopupViewModel`；`CommitDueDatePopupViewModel` → `CommitRowDueDateViewModel`（主窗与小窗共用）
- [x] `src/FlowTask.Desktop/ViewModels/TaskRowViewModel.cs`（`DueDate` 投影、`CreatedLocalText`）
- [x] `src/FlowTask.Desktop/Views/QuickCaptureWindow.axaml(.cs)`（行内 chip、底栏常驻按钮、`Ctrl+D`、浮层按键放行、按钮排除拖拽）
- [x] `src/FlowTask.Desktop/ViewModels/QuickCaptureViewModel.cs`（`NewTaskDueDate`、两个编辑器、读偏移设置、`CommitRowDueDate`）
- [x] `src/FlowTask.Desktop/Converters/DueDateConverters.cs`（删未用的 `DueDateEditConverter`）
- [x] 数字输入移除（H1）：`DueDateEditorViewModel` 去掉 `DigitText` / `HasParseError` / `ParseErrorMessage` / `UpdateDigitInput`；`DueDatePicker` 去掉数字框，打开时聚焦日历日按钮，Enter 仅在日历内提交；删除 `FlowTask.Core/Models/DueDateParser.cs`
- [x] Guide: updated 到期日与优先级、删除项目、小窗键位、默认到期天数（`GuideCatalog` 文案 + `GuideScenes` 的 DueAndPriority / DeleteProject / DefaultDue 场景；`OnboardingProgress.CurrentVersion` 1→2，因第 2 步讲解与挖空区域变化）。H1 后「到期日与优先级」改讲日历点选与键盘路径，DueAndPriority 场景改为点选迷你日历
- [x] `tests/FlowTask.Tests/`：重写 `DueDateEditorViewModelTests`；新增 `DueDatePickerTests`、`TaskRowViewModelTests`；更新 `MainViewModelTests`、`PersistThenWritebackTests`、`QuickCaptureViewModelTests`

---

## Progress log

### 2026-09-29

- Completed: 现状调研；与你确认方案 A / 小窗全量 / 双行布局 / 单分支；建 worktree `feature/due-date-picker-unify`；本 SPEC `[DRAFT]`。
- Decisions: 见 What 中「用户裁决」。
- 用户确认开工（原话：「总体没什么问题，日期小窗口我可能要看实际的效果才能决定，目前可以提示的重点是这个功能不可能完全依赖快捷键，绝对需要有直接点击的入口」）；SPEC `draft` → `in-progress`。
- Subagent/task references: explore `ses_f16df5faaffezGWO41SLVexfyI` —— 汇总日期入口、小窗模板、任务行结构、样式与相关 SPEC（结论已并入 Why 表）。
- Staged plan 1–6 完成（机器侧）：文档同步；`DueDatePicker` 控件；主窗创建栏与任务行接入；小窗接入；任务行双行布局；build + test + preview。
- 实施中的决定：
  - 行内编辑器全列表共享一个实例，控件仅在自身浮层打开期间订阅 `Committed`，避免多行互相串提交（有测试锁定）。
  - 日历只在「指针抬起落在某一天」时提交；方向键只移动预览（R2）。
  - 小窗窗口级隧道 KeyDown 放行来自 PopupRoot 的按键，否则浮层内 Enter 会保存任务、Esc 会隐藏小窗（R3）。
  - 小窗整窗拖拽排除 `Button`，否则 `BeginMoveDrag` 吞掉底栏日期按钮与 P1/P2/P3 的点击。
  - 小窗「默认 +N 天」每次 `PrepareAsync` 从设置读取偏移，不写死 1。
  - `TaskRowPadding` 取 18（见 What §C）。
- 第一轮预览反馈（用户原话）：「总体体验不错，但是需要关注这几个小问题：1.主页面的日期小窗可以向左一点，避免溢出页面。小窗溢出无所谓 2.与时间的修复无关，小窗中enter确认一个任务是优先级会重置到p2，感觉保持不变比较好 3.针对今天到期等加强表现的形式，在任务勾选已完成以后就不需要加强表现了，淡化这个内容」
  - F1：`DueDatePicker.PopupPlacement`；主窗创建栏改 `BottomEdgeAlignedRight`，浮层向左展开。任务行入口位于列表左半，保持左对齐；小窗不变（用户明示溢出无所谓）。
  - F2：小窗保存后只清标题与日期，优先级保持；Esc 关闭才回到 P2。推翻 spec-quick-window-single-project-list §7 的 [推断]，已在该 SPEC 标注。主窗创建栏本来就保持优先级，两处现在一致。
  - F3：`DueDatePicker.IsMuted` 绑定 `IsCompleted`（主窗与小窗行）；已完成时去掉 Overdue / DueToday 强调，改用 `TextDisabledBrush` 常规字重，仍可点击改期；取消勾选即恢复。
  - build 0/0；test 287 通过；preview 重新发布。
- 第二轮预览反馈（用户原话）：「1.已完成的显示为 xx/xx已完成类似的形式感觉更合理，已经完成的任务显示逾期表现很怪 2.再伴随两个无关时间的极小风格修复：一是default的数量显示由于没有删除和下面不对齐，可以对齐一下。二是删除项目的表现，目前跳出的位置还是有点违背不突变的原则，感觉可以用弹窗的形式更加合理。」
  - G1：已完成任务（主窗元信息行、小窗行尾）不再显示到期选择器，改为 `TaskRowViewModel.CompletedLocalText`「MM/dd 已完成」（本地时区，`TextDisabledBrush`）；缺 `CompletedAt` 的历史行显示「已完成」。到期日保留在库中，取消勾选即恢复。F3 的 `IsMuted` 被取代，已删除。
  - G2：侧边栏删除列对 Default 行放等宽空占位，计数与其它项目行对齐。
  - G3：删除项目确认由任务流上方插入的 `DangerConfirm` 行改为居中弹层（遮罩点击 / Esc = 取消，`确认删除` 用 `DangerAction` 样式）；`IsDeleteProjectPromptOpen` 计入 `IsBlockingOverlayOpen`。视图 A 网格从 3 行收为 2 行。design §2.2 已登记该例外。
  - build 0/0；test 289 通过。
- 用户验收（原话）：「合并到dev」—— 三轮预览后接受当前形态；SPEC `in-progress` → `done`，按容器流程提交并合入 `dev`。
- 合并受阻：本分支开出后 `dev` 已合入 `feature/onboarding-guide`（首次引导 + 操作指南 + BR-1「指南与交互同步维护」）。`finish-task.ps1` 在 5 个文件上冲突，已在 `dev` 上 `merge --abort`（`dev` 未改动），改为在任务分支先合入 `dev`：
  - 冲突解决：保留双方新增（引导锚点 `AddBar` / `AddInput` 挂到新的单行添加栏；`CoachMarkOverlay` 与删除确认弹层并存；`IsBlockingOverlayOpen` = 关闭询问 ∥ 删除确认 ∥ 引导；design §2.2 两条受限例外并列）。
  - BR-1：指南文案与动效仍在讲旧的「启用默认到期 / 展开日历 / 确认条」，已更新（见 Change checklist 的 Guide 行），引导版本 1→2。
  - 合并后 build 0/0；test 332 通过（连续 3 次）。
- 指南预览反馈（用户原话）：「指引没有问题，但是目前的操作逻辑下我认为不需要硬编码日期了，我考虑删掉直接输入日期的形式，指引也可以做对应优化。」
  - H1：删除数字速记（`DueDateParser` 与编辑器数字状态）。保留全键盘路径：打开浮层即聚焦日历中的已选日 / 今天，方向键移动、Enter 提交；Enter 只在日历内生效，Tab 到预设按钮上按 Enter 激活按钮本身。
  - 发现：`Calendar` 本身 `Focusable=false`，`Focus()` 静默失败，须聚焦模板内的 `CalendarDayButton`（其 `IsSelected` / `IsToday` 为 internal，按 `DataContext` 的日期匹配）。原数字框版本从未暴露此问题，因为焦点一直在文本框上。
  - 指南：「到期日与优先级」改讲日历点选、预设与键盘路径；DueAndPriority 场景改为弹出日期框后点选迷你日历、入口显示「2 天后」、再点 P1，并画出下方任务行以示浮层不推动布局。引导版本维持 2（本分支尚未合入，版本 2 从未发布）。
  - design §4.3 改为「预设 + 日历」并登记推翻；§4.1、domain-contract 录入方式表、spec-due-date-calendar §2.3 加失效说明。
  - 测试：删去 7 个数字解析相关用例，新增打开即聚焦日历 + Enter 提交、浮层无文本框、未高亮时 Enter 不提交等用例；build 0/0，test 326 通过（连续 3 次），不低于 AGENTS 基线 315。
- Current resume point: 等 owner 预览 H1；确认后运行 `finish-task.ps1 feature/due-date-picker-unify`。

---

## Verification

- Automated（2026-09-29）：
  - `dotnet build FlowTask.sln`：0 警告 0 错误。
  - `dotnet test FlowTask.sln`：285 通过（基线 272 → 285）。
  - 新增覆盖：编辑器预览/提交分离与逐键中间态不提交；控件点击打开、预设提交携带目标并关闭、Esc 不提交、数字框 Enter 提交、关闭后不接收共享编辑器提交；主窗/小窗行内改期落库；创建栏与小窗新建带日期后清空；小窗读取偏移设置；创建日期本地化。
  - 连续全量运行中出现过 1 次 `CrossWindowCompleteSyncTests.MainRenameProject_UpdatesQuickDropdownAndKeepsSelection` 失败；随后本分支与 `dev` 各 10 次全量运行均 0 失败。该用例不触及本 SPEC 代码路径，判为既有偶发，未修复，记录备查。
  - `publish-preview.ps1`：`preview/feature/due-date-picker-unify/FlowTask.exe`。
- Manual: 用户三轮预览（2026-09-29）。第一轮反馈 F1–F3、第二轮反馈 G1–G3 均已修复并重发 preview；最终以「合并到dev」验收。
- Automated（最终）：build 0/0；test 289 通过。
- Not run or not covered: 浮层过渡观感、打开时列表不位移、Popup 在置顶小窗里的层级与焦点（R1）、小窗行悬停入口的实际观感（Q2）只能人工目测。

人工验收清单：

| # | 操作 | 预期 |
| :--- | :--- | :--- |
| M1 | 主窗创建栏点「到期」 | 按钮下方淡入浮层；列表**不移动** |
| M2 | 浮层打开后直接按方向键（H1 起无数字框） | 日历高亮移动；按钮文案不变 |
| M3 | 按 Enter | 浮层关闭，按钮显示「今天到期」等相对文案 |
| M4 | 再打开，点「默认 +N 天」/「清除」/点日历某天 | 各自一步生效并关闭 |
| M5 | 打开后按 Esc 或点浮层外 | 关闭且不改动 |
| M6 | 输标题回车 | 新任务带上日期；创建栏按钮回到「到期」 |
| M7 | 任务行第二行点到期 | 在该处弹出同一浮层，改完即保存；没有全窗遮罩 |
| M8 | 看任务行 | 标题下方为「● 项目 · 到期 · 创建日期」；无项目时无项目段 |
| M9 | 小窗底栏点日期按钮（或 Ctrl+D） | 弹出浮层；选完焦点回输入框，回车保存带日期 |
| M10 | 小窗行：有到期的显示文案；无到期的悬停整行出现「到期」 | 点击可改，主窗同步 |
| M11 | 小窗浮层内按 Enter / Esc | 只作用于浮层，不保存任务、不隐藏小窗 |

---

## Risks and open questions

- **Q1（已裁决 2026-09-29）**：用户「总体没什么问题」—— 锚定、非模态、点外即关的浮层不违反 §2.2，写入 design。
- **Q2（按现状接受 2026-09-29）**：小窗无到期日时入口悬停整行浮现。用户未单独裁决，以「合并到dev」接受预览现状；若日后要改为常驻，只需去掉 `EditorialStyles.axaml` 中 `DueDatePicker.HoverOnly:unset` 两条样式。
- **Q3（已裁决）**：`Ctrl+D` 可作为补充，不得是唯一入口。
- **Q4（已裁决，默认）**：元信息行项目名本轮始终显示。
- **R1（待人工）**：Popup 在置顶、无装饰的小窗里的层级与焦点行为需实测；Popup 可能超出小窗边界（桌面端 Popup 为独立顶层，预期可行）。
- **R2（已处理）**：日历只在指针落在某天时提交，方向键只移动预览。
- **R3（已处理）**：小窗窗口级按键放行来自浮层的事件。
- **R4（记录）**：`CrossWindowCompleteSyncTests.MainRenameProject_UpdatesQuickDropdownAndKeepsSelection` 出现过 1 次偶发失败，`dev` 上同样存在该用例；复现条件未知，不属本 SPEC。

---

## Lessons learned

- **Attribution**: design wrong —— spec-due-date-calendar 把「三来源同步」落成三组常驻平铺控件，未约束展开方式（`IsVisible` 切换直接推动布局）与入口数量；逐键解析即提交，把输入中间态当结果。
- 「三来源」是录入方式的数量，不是入口的数量：多种方式应收纳在**一个**入口后面（design §2.5）。
- 预览 / 提交要分离：任何边输入边生效的控件，都要先问中间态会不会被当成结果（日历方向键同理）。
- 把录入方式收进一个好用的入口后，要重新问每种方式是否还值得存在：数字速记在平铺时是「省事」，在一键可达的日历旁就成了「要记规则」（H1）。
- 删掉一个聚焦控件时要检查键盘入口是否还在：`Calendar` 不可聚焦，焦点必须落到 `CalendarDayButton`，否则 Esc / Enter 全部失效。
- 共享编辑器 + 多实例控件时，订阅必须限定在「本实例浮层打开期间」，否则一行的提交会串到所有行。
- Popup 的按键事件会经所属窗口的隧道处理器；窗口级快捷键（小窗 Enter 保存 / Esc 隐藏）必须显式放行来自 PopupRoot 的事件。
- 无装饰窗口用 `BeginMoveDrag` 做整窗拖拽时，交互控件必须排除，否则点击被系统拖拽循环吞掉。
- 完成态的信息应换义而不是只降色：已完成任务的「逾期」本身没有意义，用户要的是「何时完成」（G1 取代了 F3）。
- 「布局不突变」同样适用于非日期的内联确认条（G3），这是 design 层原则，不是单个控件的特例。
- `design-visual-language §1.3` 的留白区间在实施时才被发现与 SPEC 初值（14px）冲突——写 SPEC 数值前应先检索 design 约束。

---

## Related documents

- SPECs: [spec-due-date-calendar](./spec-due-date-calendar[DONE].md) · [spec-quick-window-single-project-list](../quick-capture/spec-quick-window-single-project-list[DONE].md) · [spec-quick-window-hotkey-capture](../quick-capture/spec-quick-window-hotkey-capture[DONE].md) · [spec-classification-ui](../main-window/spec-classification-ui[DONE].md)
- Design: [design-interaction-principles](../../design/design-interaction-principles.md) · [design-visual-language](../../design/design-visual-language.md) · [design-domain-contract](../../design/design-domain-contract.md)
- Rules: [rule-no-invented-user-behavior](../../rules/rule-no-invented-user-behavior.md) · [technical-rules](../../rules/technical-rules.md)
- Requirements: [REQUIREMENTS](../../requirements/REQUIREMENTS.md)

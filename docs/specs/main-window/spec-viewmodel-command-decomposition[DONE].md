# spec-viewmodel-command-decomposition: MainViewModel TR-1 命令拆分

## Metadata

- **ID**: spec-viewmodel-command-decomposition
- **Type**: complex
- **Status**: done
- **Owner**: AI
- **Created Date**: 2026-09-17
- **Last Updated**: 2026-09-18

---

## Why

`MainViewModel.cs`（约 1183 行）同时持有任务流状态，并内联实现项目/标签/到期日/外观等正交操作。
根因不是「行数本身」，而是 **状态持有** 与 **操作实现细节** 混在同一类（`docs/rules/technical-rules.md` TR-1）。
若不先拆分，后续功能 SPEC（如完成≠归档）会继续往同一类叠加，债务利息上升。

- **Core Motivation**：TR-1 已于 2026-09-17 生效；既存超阈值方法不得再原地加逻辑。越早拆，绑定路径与测试面越小。
- **Current Limitation**：32 个 `[RelayCommand]` 中约 19–20 个方法体 ≥10 行或含多重副作用；颜色轮转逻辑在项目/标签两侧重复。

---

## What

### Goals

- 按 TR-1：每个超阈值的用户操作抽成独立 ViewModel 类；`MainViewModel` 只保留可观察状态、派生属性、薄委托。
- 横切色轮逻辑收入 `AppearanceCoordinator`（或同等静态协调器）。
- **行为零变化**：人工验证与现有单测语义不变；XAML `Command="{Binding XxxCommand}"` 路径尽量不改（聚合根保留同名薄命令）。
- `LoadTasksAsync` **不**按「操作=类」拆出（共享加载漏斗，拆出会制造新调用混乱）；可保留并被薄命令调用。

### Non-goals

- 不改交互设计、领域契约、SQLite schema。
- 不引入 Prism / ReactiveUI / DI 容器（除非现有组装方式已要求；本 SPEC 默认保持 `App.axaml.cs` 手工组装）。
- 不顺便清理 `Class1.cs`、CancellationToken、外观持久化等其它债务。
- 不改 `QuickCaptureViewModel`（除非抽取过程暴露共享工厂需求；默认排除）。
- 不在本 SPEC 内完成「按子领域拆 `ProjectListViewModel` / `TagListViewModel`」——那是可选后续；本 SPEC 只做 **按操作拆命令**。

### Impacted files

- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`
- 新建多个 `src/FlowTask.Desktop/ViewModels/*ViewModel.cs`（或 `ViewModels/Actions/` 子目录，落地时二选一并在 Progress 记录）
- `src/FlowTask.Desktop/Appearance/AppearanceCoordinator.cs`（色轮提取）
- `tests/FlowTask.Tests/MainViewModelTests.cs`、`ProjectInteractionTests.cs`（断言对象仍为 `MainViewModel` 命令时尽量不动；若需测抽出类则补测）
- `src/FlowTask.Desktop/Views/MainWindow.axaml` — **目标零改**；仅当无法保留薄命令时才改

---

## Constraints and decisions

- **TR-1**（`docs/rules/technical-rules.md`）：超阈值必须抽出；薄委托合法；单行 toggle / 纯委托豁免。
- **rule-code-standards**：`[ObservableProperty]` / `[RelayCommand]` / `WeakReferenceMessenger`；注释写 WHY。
- **Article 6**：色轮等事实单一权威源（AppearanceCoordinator）。
- **搬移禁止重写**（防呆清单第 4 条）：每批只做抽取+接线，不夹带行为变更。
- 参考范式：`docs/technical/analysis-avalonia-architecture-references.md`（SourceGit：操作类独立，聚合根持状态）。

### 设计裁决（待用户确认；默认推荐）

| 项 | 推荐默认 | 备选 |
| :--- | :--- | :--- |
| 绑定策略 | 聚合根保留同名 `[RelayCommand]` 薄方法，内部 `new XxxAction(...).ExecuteAsync()` | 子 VM 暴露为属性，XAML 改绑定路径（风险高，不推荐） |
| 文件布局 | `ViewModels/Actions/` 子目录 | 与现有 ViewModels 平铺 |
| 批次顺序 | ①色轮 ②项目管理 ③标签管理 ④任务流/编辑/到期日 | 可按用户指定调整 |

---

## Acceptance criteria

- [x] TR-1 候选方法（下表）均已抽出或有文档记录的豁免理由；`MainViewModel` 对应命令为薄委托。
- [x] `PickNextProjectColor` / `PickNextTagColor` / 改色「找下一色」重复逻辑已收敛到 AppearanceCoordinator。
- [x] `dotnet build FlowTask.sln`：0 警告 0 错误。
- [x] `dotnet test FlowTask.sln`：185 通过。
- [x] 人工验证：创建/完成/删除任务、项目 CRUD/改色/删除确认、标签 CRUD/改色、行编辑、到期日弹层、筛选切换 — 行为与改前一致（用户确认「没有太大问题」）。
- [x] 本 SPEC 与 `docs/specs/README.md` 状态/索引同步（`done`）。

### 抽取候选清单（估时用；实施时勾选）

| 命令 | 约行数 | 批次 | 备注 |
| :--- | ---: | :--- | :--- |
| CreateProjectAsync | 23 | 2 | |
| CommitRenameProjectAsync | 26 | 2 | |
| ChangeProjectColorAsync | 24 | 2 | 依赖色轮 |
| ArchiveProjectAsync | 18 | 2 | |
| RequestDeleteProjectAsync | 10 | 2 | 可与 Confirm 同文件或拆开 |
| ConfirmDeleteProjectAsync | 21 | 2 | |
| CreateTagAsync | 26 | 3 | |
| CommitRenameTagAsync | 28 | 3 | |
| ChangeTagColorAsync | 20 | 3 | 依赖色轮 |
| DeleteTagAsync | 11 | 3 | |
| AddTaskAsync | 42 | 4 | 最复杂 |
| ToggleEditAsync / SaveEditAsync | 21/29 | 4 | |
| Open/Commit DueDate popup | 11/16 | 4 | Close 豁免或一并 |
| ToggleCompleteAsync / DeleteTaskAsync | 11/10 | 4 | 临界 |
| SelectProjectAsync / ChangeFilter | 13/18 | 4 | 筛选状态仍属聚合根 |
| LoadTasksAsync | 30 | — | **不拆为操作类**；保留 |

豁免（保持原地）：`Begin/CancelRename*`、`ToggleCreateProject`、`CancelDeleteProject`、`OpenQuickCapture`、`ToggleTheme`、`ToggleSettings`、`RefreshTasksAsync`、`CloseDueDatePopup`（若仍为薄方法）。

---

## Staged plan

1. **Phase 0 — 确认本 SPEC**（当前）：用户审核默认裁决；通过后改 `[IN-PROGRESS]` 再改代码。
2. **Phase 1 — 色轮收敛**：AppearanceCoordinator 增加 Next/Cycle API；MainViewModel 调用替换重复实现；build+test。
3. **Phase 2 — 项目管理操作类**：Create / Rename / ChangeColor / Archive / RequestDelete / ConfirmDelete；薄命令接线；build+test。
4. **Phase 3 — 标签管理操作类**：Create / Rename / ChangeColor / Delete；build+test。
5. **Phase 4 — 任务流与编辑**：AddTask / ToggleEdit / SaveEdit / DueDate popup / ToggleComplete / Delete / SelectProject / ChangeFilter 薄委托化；build+test。
6. **Phase 5 — 收尾**：跑全量测试；更新本 SPEC 勾选与 Progress；交用户人工验证；验证通过后再由用户指示提交。

每 Phase 结束输出一行状态（Gate 3）；不跨 Phase 批量提交逻辑而不验证。

---

## Change checklist

- [x] `AppearanceCoordinator` 色轮 API
- [x] Actions 类（或平铺）文件全集按上表（19 个）
- [x] `MainViewModel` 薄委托 + 删除内联实现
- [x] 测试仍绿；AppearanceCoordinator 色轮单测已补
- [x] 更新本 SPEC → `[DONE]` 与 README 索引

---

## Progress log

### 2026-09-17

- Completed:
  - 用户确认 SPEC → 改 `[IN-PROGRESS]`。
  - Phase 1：`AppearanceCoordinator.PickPaletteColor` / `CyclePaletteColor`；MainViewModel 去重；补 AppearanceCoordinatorTests。
  - Phase 2–4：`ViewModels/Actions/` 共 19 个操作类；MainViewModel 对应命令改为薄委托。
  - `MainViewModel.cs` 1183 → 912 行；`LoadTasksAsync` 按计划保留。
  - `dotnet build` 0/0；`dotnet test` 185 通过。
- Decisions: 薄命令 + Actions 子目录；命名 `*ViewModel`。
- Current resume point: **已闭环**。用户确认人工验证「没有太大问题」；状态转 `done`。提交时机由用户指示。

---

## Verification

- Automated: `dotnet build` 0 警告 0 错误；`dotnet test` 185 通过（含色轮回归 1 条）
- Manual: 用户确认主窗关键路径无重大问题（2026-09-18）
- Not run: 无

---

## Risks and open questions

| 项 | Owner | 说明 |
| :--- | :--- | :--- |
| 与 `spec-quick-window-hotkey-capture[IN-PROGRESS]` 并行 | 用户 | 建议本重构期间暂停往 MainViewModel **新增**内联逻辑；小窗 SPEC 若需改 MainViewModel，应先抽或等本 SPEC Phase 完成 |
| 抽出类如何拿仓储/时钟 | AI | 构造注入接口引用（与现 MainViewModel 字段同源），不引入新 DI 框架 |
| Actions 命名：`CreateProjectViewModel` vs `CreateProjectAction` | 用户 | 推荐 `*ViewModel` 以符合 CT MVVM；若用户偏好 Action 后缀则统一 |

---

## Lessons learned

（闭环后填写）

---

## Related documents

- Rules: [`technical-rules.md`](../../rules/technical-rules.md) TR-1
- Technical: [`analysis-avalonia-architecture-references.md`](../../technical/analysis-avalonia-architecture-references.md)
- Sibling: [`spec-sidebar-selection-consolidation[DONE].md`](./spec-sidebar-selection-consolidation[DONE].md)

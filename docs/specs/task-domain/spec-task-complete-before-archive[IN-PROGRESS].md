# spec-task-complete-before-archive: 完成 ≠ 归档（勾选容错）

## Metadata

- **ID**: spec-task-complete-before-archive
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf
- **Created Date**: 2026-09-16
- **Last Updated**: 2026-09-23

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-23）在 `feature/quick-window-standalone` 分支按 1→2→3 顺序开工。
> 本 SPEC 为第 2 份，状态 `in-progress`。**
> D1/D2/D3/D4 已裁决，见 §2.4。

**上游依据**：
- design-interaction-principles §7.3（用户抱怨勾选后瞬间进归档）
- 本轮用户原话（2026-09-16）：
  > 「勾选后不应该那么快的消失，应该先显示已完成（目前归档里面的实现）的划线低饱和状态，触发了某个时间才将内容一起放入归档。目前的方式没有容错，比如误触就必须进入归档才能回退，这明显不合理。」
  >
  > 随后澄清宽限期触发方式：**「我倾向用户手动触发，比如完整归档项目之类的操作才放入归档」**（**非**定时器自动归档）。

**建议开工顺序中的第 2 份**（在小窗列表勾选之前）。主窗与小窗共用此语义。

**与「非目标：撤销/重做」的关系**：REQUIREMENTS 排除的是**命令栈式**撤销重做。本 SPEC 是**完成态与归档态分离** + 取消勾选回退，**不是**通用 Undo 栈。须在 REQUIREMENTS 写清，避免被 §6 误伤。

---

## 0. 新会话接手须知

1. 现状：`IsCompleted == true` ⇔ 活动查询排除 ⇔ 侧边栏「已完成归档」展示。勾选后 `LoadTasksAsync` 立即消失。
2. 目标：`IsCompleted` 只表示勾选完成；**进入「已完成归档」视图**需要单独的归档动作。
3. Article 9：若曾考虑 `Task.Delay` 自动归档 —— **已否定**；禁止用墙钟定时作为归档触发。

---

## 1. Why

用户两次指向同一根因：完成操作与离开当前列表绑定过紧，误触只能去归档里找。

Attribution：`Design Wrong` —— 将「完成」与「归档」做成同一状态。

---

## 2. What

### 2.1 领域契约变更

在 `TaskItem`（或等价模型）增加归档维度，例如：

| 字段 | 语义 |
| :--- | :--- |
| `IsCompleted` / `CompletedAt` | 用户勾选完成；可再次取消勾选清除 |
| `IsArchived` / `ArchivedAt`（新） | 已进入归档堆；**仅**由显式归档动作置位 |

**不变量（草案）**：

1. `IsArchived == true` 之前必须 `IsCompleted == true`（未完成不可归档）—— **[待裁决 D1]** 是否允许例外。
2. 取消勾选：若尚未归档 → `IsCompleted=false`，`CompletedAt=null`，仍在原列表。
3. **[待裁决 D2]** 已归档后是否允许直接取消勾选拉回，或必须「取消归档」。

### 2.2 查询语义

| 视图 | 过滤 |
| :--- | :--- |
| 全部 / 项目活动列表 | `!IsDeleted && !IsArchived`（**含**已完成未归档） |
| 已完成归档 | `!IsDeleted && IsArchived` |
| 小窗单项目列表 | 同活动列表 + `ProjectId` |

### 2.3 UI

- 已完成未归档：沿用现有归档行的**划线 + 低饱和**样式，但留在当前列表。
- 取消勾选即可恢复（容错）。
- **归档触发（用户裁决 2026-09-16，范围见 §2.4 D3）**：手动；归档时**保留 `ProjectId` 来源**（不是「整棵项目归档消失」）。控件位置见 §2.4 D3。

### 2.4 已裁决（2026-09-23，续接第 1 份 SPEC 的开工确认）

| # | 议题 | 裁决 |
| :--- | :--- | :--- |
| D1 | 未完成能否归档 | **不能**（A）。`ArchiveCompletedInProjectAsync` 只处理 `IsCompleted && !IsArchived` 的行，未完成任务不受影响 |
| D2 | 已归档回退 | 本轮不做「取消归档」入口（B 的「直接取消完成并出归档」也不做）。已归档任务是终态展示，取消勾选仅对**未归档**的完成任务生效；若后续需要回退，登记为 Deferred |
| D3 | 手动归档入口 | **A**：侧边栏 VIEWS 下「已完成归档」旁新增「归档全部已完成」按钮，作用范围是当前登录用户可见的**全部项目**的已完成未归档任务（不按单项目拆分入口，降低本轮复杂度） |
| D4 | 历史数据 | **A**：迁移时将所有既有 `IsCompleted=true` 的行标记 `IsArchived=true`、`ArchivedAt=CreatedAt`（无法还原真实归档时刻，取创建时刻占位，避免引入新的系统时钟依赖）。避免升级后活动列表被历史完成项淹没 |

**范围收窄说明**：SPEC 原文 D3 曾列出「项目上下文菜单」「多选批量」等候选，本轮实现选择最小可行的**全局归档**入口（VIEWS 区域按钮），不做逐项目归档、不做多选。若后续需要更细粒度，登记为 Deferred。

### 2.5 非本 SPEC

- 通用撤销/重做栈
- 定时自动归档
- 小窗列表布局（仅消费本语义）

---

## 3. How（Staged plan）

1. `TaskItem` 新增 `IsArchived` (`bool`) / `ArchivedAt` (`DateTime?`)。sqlite-net `CreateTableAsync` 对已存在表自动 `ALTER TABLE ADD COLUMN`，新列默认 `false`/`null`，无需迁移脚本骨架；但**必须**在 `InitializeAsync` 内加一次一次性数据校正（D4）：对 `IsArchived=false && IsCompleted=true` 的历史行回填 `IsArchived=true`、`ArchivedAt=CreatedAt`，且只在首次启动执行一次（用一个 `AppSettings` 标志位守护，避免每次启动重复扫描全表）。
2. `ITaskRepository` 变更：
   - `GetAllActiveTasksAsync` 过滤条件由 `!IsDeleted && !IsCompleted` 改为 `!IsDeleted && !IsArchived`（含已完成未归档）。
   - `GetTasksByProjectAsync` 同样把 `!IsCompleted` 改为 `!IsArchived`。
   - `GetCompletedTasksAsync` 语义改为「已归档」：过滤条件改为 `!IsDeleted && IsArchived`。
   - 新增 `ArchiveAllCompletedAsync()`：对 `!IsDeleted && IsCompleted && !IsArchived` 的行批量置位 `IsArchived=true`、`ArchivedAt=<IClock.UtcNow>`，返回受影响行数。
3. `MainViewModel.ToggleCompleteAsync`（复用现有 `ToggleCompleteTaskViewModel`）：逻辑不变（只切 `IsCompleted`/`CompletedAt`），但因为查询语义已改，完成后任务会**留在**活动列表——不需要额外代码改动，是查询语义变化的自然结果。
4. 新增 `ArchiveCompletedAsync` 命令（`MainViewModel`），调用仓储 `ArchiveAllCompletedAsync` 后 `LoadTasksAsync` + `RefreshCountsAsync`。UI 入口：VIEWS 区「已完成归档」下方新增按钮「归档全部已完成」，仅在 `ActiveCount` 对应的已完成未归档数 > 0 时可点击（若为 0 则禁用或隐藏，避免空操作）。
5. 样式：无需新增，`.Completed` 已绑定 `Task.IsCompleted` 且已在活动列表内生效。
6. 单测（`SqliteTaskRepositoryTests` / `MainViewModelTests`）：
   - 完成后仍出现在 `GetAllActiveTasksAsync` / `GetTasksByProjectAsync` 结果中；
   - 取消勾选后 `IsCompleted=false`、`CompletedAt=null`，未归档不受影响；
   - `ArchiveAllCompletedAsync` 只归档 `IsCompleted && !IsArchived` 的行，未完成的行不受影响；
   - 归档后该行从 `GetAllActiveTasksAsync` 消失、出现在 `GetCompletedTasksAsync`（现为"已归档"查询）；
   - D4 迁移：预置一条 `IsCompleted=true, IsArchived=false` 的历史行，`InitializeAsync` 后应变为 `IsArchived=true`。
7. 修订 REQUIREMENTS 补 R-4.1/R-4.2/R-4.3（已在当前 REQUIREMENTS.md §4.1.1 存在，仅需勾住实现）；`docs/specs/README.md` 索引与接手入口同改。
8. `dotnet build` + `dotnet test`；人工验证见 §4。

---

## 4. 验证

### 机器

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
dotnet build FlowTask.sln -v q --nologo
dotnet test  FlowTask.sln --nologo -v q
```

- [x] `dotnet build` 零警告零错误（AI 已跑）
- [x] `dotnet test` 全绿（190 通过，含本轮新增 4 项：`GetAllActiveTasksAsync_ShouldIncludeCompletedButExcludeArchivedAndDeleted` / `ArchiveAllCompletedAsync_OnlyArchivesCompletedNotYetArchived` / `InitializeAsync_MigratesHistoricalCompletedRowsToArchived` / `MainViewModelTests.CompletedFilter_ShowsOnlyArchivedTasks` 改写 + `Counts_ReflectActiveAndCompletedTotals` 改写）
- [x] 新增仓储测试覆盖：完成后留在活动列表 / 取消勾选回退（既有 `ToggleComplete_StampsCompletionTime` 已覆盖）/ 批量归档只影响已完成未归档行 / 归档后从活动列表消失且出现在归档查询 / D4 历史数据一次性迁移

### 人工（你执行；AI 不得代勾，全部完成后统一在 Windows 上测）

| # | 操作 | 应看到的现象 | 通过？ |
| :--- | :--- | :--- | :--- |
| M1 | 勾选一条任务完成 | 任务**仍在**当前列表，标题划线+低饱和，不消失 | [ ] |
| M2 | 再次取消勾选 | 恢复正常样式，仍在原位置 | [ ] |
| M3 | 点击「归档全部已完成」 | 刚才勾选的任务从活动列表消失，出现在「已完成归档」视图 | [ ] |
| M4 | 归档后再看「全部任务」计数 | 活动计数相应减少 | [ ] |
| M5（存量数据） | 若本机已有旧版本产生的 `IsCompleted=true` 历史任务，升级后启动一次 | 这些任务应直接出现在「已完成归档」，不挤入活动列表 | [ ] |

---

## 5. Deferred Items

| 事项 | 期限 | 触发条件 |
| :--- | :--- | :--- |
| 归档前可选回顾面板 | 2026-10-31 | 用户觉得批量误归档仍难容错 |
| 取消归档（从归档视图拉回活动列表） | 2026-10-31 | 用户反馈归档后完全无法回退过于严格 |
| 逐项目归档 / 多选批量归档 | 演进 | 用户反馈全局归档粒度太粗 |

---

## Progress log

### 2026-09-23

- Completed：`TaskItem` 加 `IsArchived`/`ArchivedAt`；仓储查询语义全部改为 `!IsArchived`（含项目筛选）；新增 `ArchiveAllCompletedAsync` + D4 迁移；`MainViewModel` 新增 `PendingArchiveCount`/`HasPendingArchive`/`ArchiveCompletedCommand`；侧边栏新增「归档全部已完成」按钮；改写 3 个受语义变化影响的既有测试，新增 3 个仓储测试。
- Decisions：D1/D2/D3/D4 按 §2.4 裁决执行；D3 采用最小可行的全局归档入口（不做逐项目/多选）。
- Current resume point：机器验证已通过；等待用户在人工验证清单（§4 人工）与 SPEC-3 一起做 Windows 端统一测试。
- Subagent/task references：无（本轮由主会话直接实现）。

---

## 6. Lessons Learned

（事件发生后追加。）

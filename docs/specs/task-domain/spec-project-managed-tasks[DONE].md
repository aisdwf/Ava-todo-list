# spec-project-managed-tasks: 全任务看板 + 单项目自主管理（废除任务归档）

## Metadata

- **ID**: spec-project-managed-tasks
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-26
- **Last Updated**: 2026-09-26

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-26）「确认」。状态 `in-progress`。**
> Q1=B 物理删除；Q2=A 看板与项目同一套完成套件；Q3 排序细则见 Constraints；Q4=A 小窗对齐。
> 未完成组保留截止时间作为第二关键字（用户确认开工时未反对该解释）。

**上游依据（用户原话，2026-09-26）**：

> 「对于任务逻辑需要更加清晰的界定。在新的项目分级制度的模式下，之前的默认归档之类的能力已经完全不适用了。全部任务是有一定价值的，可以看到目前有的所有任务的情况，但是归档在项目分类的情况下完全没有意义。归档的内容同样应该由项目本身来管理。我认为应该做成全任务作为综合看板，单项目自主管理的形式。」
>
> 1. 「删除目前的归档任务逻辑」
> 2. 「已完成这些具体的动作都由项目自行管理。打勾就置底、划线、字体颜色深度改变（常规的完成套件，目的是降低重要程度）。至于彻底删除，完全交给用户自己处理，无论是否已完成，通过最右侧的『X』来做删除」
> 3. 「项目内部也需要更加合理的功能，比如排序。我的计划是优先级>截止时间>创建时间」

> **⚠ 与既有裁决的冲突（必须显式记录）**：
> `REQUIREMENTS.md` R-4.1～R-4.3、`design-domain-contract` §4.2「归档已完成任务」、
> `design-interaction-principles` §7.3、以及
> [`spec-task-complete-before-archive[SUPERSEDED]`](./spec-task-complete-before-archive[SUPERSEDED].md)
> 均将「完成 ≠ 归档 + 全局已完成归档视图」定为产品契约。
> 本 SPEC 是用户在本轮对话中**推翻此前裁决**，不是撰写者的推断。
> 依 `rule-no-invented-user-behavior.md` §2.1，此推翻已获用户本轮原话确认，可写入需求修订。
>
> 保留且**不被本 SPEC 推翻**的部分：勾选完成仍只切换完成态、划线低饱和、可就地取消勾选
> （原 R-4.1 / R-4.2 的容错意图仍有效，只是不再存在「归档」这第二层状态）。

---

## Why

项目已成为任务的主分类维度之后，全局「已完成归档」把已完成项从项目上下文里抽走，
与「任务都有价值、应能看到当前所有任务」冲突。归档堆与项目列表是同一批数据的第二种家，
用户必须到另一入口才能找回刚勾完的内容。

根因：`spec-task-complete-before-archive` 把「误触勾选后立刻从列表消失」修成了
「完成留在列表 + 显式归档才离开」，但**离开之后仍进入一个与项目无关的全局堆**。
在项目分类已经落地的前提下，这第二层状态不再有产品位置。

Attribution：`Design Wrong` —— 任务归档作为独立领域状态，在项目分级模式下不再适用。

---

## What

信息架构改成两层，共用同一套任务生命周期：

| 表面 | 职责 |
| :--- | :--- |
| **全部任务** | 综合看板：看到当前**所有未删除**任务（含已完成） |
| **单个项目** | 自主管理：该项目下全部未删除任务；完成、排序、删除都在这里发生 |

任务生命周期只剩两态 + 一条删除路径：

| 动作 | 效果 |
| :--- | :--- |
| 勾选完成 | `IsCompleted=true`；**不离开列表**；划线 + 降低视觉权重；**置底** |
| 取消勾选 | 恢复未完成样式与排序位置 |
| 右侧 `X` | **物理删除**（已完成 / 未完成同一入口；无确认框） |

废除：

- 任务字段 `IsArchived` / `ArchivedAt` 的读写与查询过滤（列保留为死字段，不 `DROP COLUMN`）
- `ArchiveAllCompletedAsync`、侧边栏「已完成归档」、按钮「归档全部已完成」
- `ViewSelectionKind.Completed` / `TaskFilter.Completed` / `GetCompletedTasksAsync`
- D4 历史迁移（把已完成行标成已归档）—— 反向效果正是本 SPEC 要的：已归档行重新出现在看板与项目中

排序（全部任务看板、单项目列表、小窗同一套；2026-09-26 所有者裁决）：

1. **未完成在上、已完成置底**
2. **未完成组**：`Priority` 降序（High → Medium → Low）> `DueDate` 升序（早的在前；**无截止日期排在有截止日期之后**）> `CreatedAt` 降序（**越晚越前**）
3. **已完成组**：只按勾选顺序，`CompletedAt` 降序（后勾的靠近未完成区；**先标记的在最底部**）。**不再套用优先级**——否则先勾的高优先级会压在后勾的低优先级上面，表现为「偶发」错序。历史行若没有 `CompletedAt`，回退 `ArchivedAt` / `CreatedAt`。

完成视觉：复用现有 `.TaskTitle.Completed`（透明度 0.4 + 删除线）。对应用户原话「划线、字体颜色深度改变」；本轮不新造完成样式。

删除：行内 `X` 改为 **物理删除**（`PermanentDeleteAsync`），已完成与未完成同一入口。历史软删除行（`IsDeleted=true`）仍被查询排除，本轮不把旧软删行批量物理清掉。

### 查询语义（拟定）

| 视图 | 过滤 |
| :--- | :--- |
| 全部任务 | `!IsDeleted`（含已完成；**不再**看 `IsArchived`） |
| 单项目 | `!IsDeleted && ProjectId == <id>` |
| 已完成归档 | **删除该视图** |

### 小窗（消费方，非本 SPEC 新能力）

小窗单项目列表已经「未完成在上、已完成置底」。本轮必须停止按 `IsArchived` 过滤，
否则历史已归档任务会在主窗出现、小窗仍消失。排序关键字对齐主窗（补上 `CreatedAt`）。
小窗**不**加 `X` 删除——用户未要求；删除主战场在主窗。

### `Project.IsArchived`

项目实体自己的归档字段与任务归档是两回事。本 SPEC **不改**项目归档
（该能力目前仍无 UI 入口，见分类 UI SPEC 的既有推迟项）。

---

## Non-goals

- 项目层级（父子项目）；本 SPEC 的「项目分级」按用户原文理解为**以项目为分类维度**，不是嵌套树
- 给 `X` 加确认框或回收站恢复 UI（用户未要求；Q1 已裁决为直接物理删除）
- 把历史 `IsDeleted=true` 的旧行批量物理清掉（只改今后的 `X` 路径）
- 拖拽手动排序
- 小窗增加删除按钮
- 项目归档（`Project.IsArchived`）的 UI 入口
- 通用撤销/重做栈
- 物理删除 `IsArchived` / `ArchivedAt` 数据库列（与 `Description` 死字段同一策略）

---

## Constraints and decisions

- Constitution Article 3：先改契约（REQUIREMENTS + design-domain-contract）再改代码，禁止仓储里静默不再写 `IsArchived`
- Constitution Article 8：`spec-task-complete-before-archive` 开工后改为 `[SUPERSEDED]`，头部重定向到本 SPEC，禁止只停用代码不改文档
- Constitution Article 10：不要留「归档入口隐藏但 `IsArchived` 仍参与查询」的半废弃状态
- `rule-no-invented-user-behavior`：交互决策必须追溯用户原话；未写明的排序细则标为 `[推断]` 或开放问题
- `sqlite-net-pcl` 不物理删列；`IsArchived`/`ArchivedAt` 停用后保留列
- TR-1：删除归档命令时同步删除对应 ViewModel 薄封装，禁止在 `MainViewModel` 里留下空壳
- 时钟：任何新时间戳仍经 `IClock`，禁止 `DateTime.UtcNow`（Article 9）

已绑定（有用户原话，不另作推断）：

1. 删除任务归档逻辑与全局「已完成归档」入口
2. 完成套件 = 置底 + 划线 + 降低视觉权重；目的是降低重要程度，不是把任务送走
3. 彻底离开列表只走右侧 `X`，已完成与未完成同一入口
4. 组内排序关键字顺序：优先级 > 截止时间 > 创建时间
5. 全部任务保留为综合看板；单项目自行管理其任务
6. **Q1 = B**：`X` 物理删除（2026-09-26）
7. **Q2 = A**：看板与项目同一套完成套件（2026-09-26）
8. **Q3**：未完成组优先级第一，再截止、再创建越晚越前；已完成组**只按勾选顺序**（2026-09-26 自定义裁决；2026-09-26 预览反馈后明确优先级不作用于已完成组）
9. **Q4 = A**：小窗过滤与排序对齐主窗，不加 `X`（2026-09-26）

---

## Acceptance criteria

- [x] 侧边栏不再有「已完成归档」和「归档全部已完成」
- [x] 「全部任务」展示所有未删除任务（含已完成、含此前已归档的历史行）
- [x] 选中某项目时，该项目下列出其全部未删除任务（含已完成）
- [x] 勾选完成：任务留在当前列表，划线 + 低饱和，并排到未完成任务下方
- [x] 取消勾选：恢复未完成样式，并回到未完成组内按三关键字排序后的位置
- [x] 右侧 `X` 对已完成和未完成任务都做物理删除，删除后不再出现在看板、项目列表或数据库该行
- [x] 排序：未完成在上已完成置底；未完成按优先级降序 + 截止升序（无截止最后）+ 创建降序；已完成只按勾选时间降序（先标记在最底，不看优先级）
- [x] 代码路径不再读取或写入任务 `IsArchived` / `ArchivedAt`（测试夹具除外若仍需证明列存在）
- [x] `REQUIREMENTS.md` R-4 从「完成 ≠ 归档」改为本生命周期；design 契约同步
- [x] `spec-task-complete-before-archive` 在开工同一改动中改为 `[SUPERSEDED]`
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不退化（基线以实现时实测为准，不得预填）

---

## Staged plan

1. **契约**：修订 `REQUIREMENTS.md` R-4、`design-domain-contract` §4.2、`design-interaction-principles` §7.1/§7.3；将 `spec-task-complete-before-archive` 标为 `[SUPERSEDED]` 并重定向。
2. **仓储**：活动/按项目查询改为 `!IsDeleted`；删除 `GetCompletedTasksAsync`、`ArchiveAllCompletedAsync` 与 D4 迁移；抽出统一排序（完成置底 + 三关键字）；`IsArchived` 列停写。
3. **主窗状态与 UI**：去掉 `ViewSelectionKind.Completed` / `TaskFilter.Completed` / 归档按钮与计数；全部任务与项目列表走新查询。
4. **完成套件**：勾选仍只切 `IsCompleted`；列表顺序由仓储/共享排序保证置底；视觉复用 `.Completed`。
5. **删除**：`DeleteTaskViewModel` 改为 `PermanentDeleteAsync`；已完成/未完成同一 `X`；无确认框（维持现状）。
6. **小窗对齐**：去掉归档过滤；排序与主窗同一比较器。
7. **测试与联动 SPEC**：改写/删除归档测试；给排序与置底补测试；修订 `spec-quick-window-single-project-list`、`spec-create-task-inherits-selected-project` 中对 Completed 视图的引用。
8. **机器验证**：`dotnet build` + `dotnet test`；提供人工验证表（不代勾）。

---

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md` — 重写 §4.1.1 / R-4.*
- [x] `docs/design/design-domain-contract.md` — 删除「归档已完成任务」；`TaskItem` 字段表将 `IsArchived`/`ArchivedAt` 标为停用
- [x] `docs/design/design-interaction-principles.md` — §7.1 侧边栏不再含已归档；§7.3 改为完成套件（置底/划线/降权）+ `X` 删除
- [x] `docs/specs/task-domain/spec-task-complete-before-archive[SUPERSEDED].md` — 重定向
- [x] `docs/specs/quick-capture/spec-quick-window-single-project-list[DONE].md` — 去掉归档依赖；排序对齐 `TaskListOrder`
- [x] `docs/specs/main-window/spec-create-task-inherits-selected-project[DONE].md` — 删除 `Completed` 视图分支
- [x] `docs/specs/README.md` — 索引与接手入口
- [x] `src/FlowTask.Core/Models/TaskItem.cs` — 字段注释改为停用
- [x] `src/FlowTask.Core/Interfaces/ITaskRepository.cs` — 查询语义与删除归档 API
- [x] `src/FlowTask.Infrastructure/Persistence/SqliteTaskRepository.cs` — 查询/排序/删除归档实现与迁移
- [x] `src/FlowTask.Desktop/ViewModels/ViewSelection.cs` — 去掉 `Completed`
- [x] `src/FlowTask.Desktop/ViewModels/MainViewModel.cs` — 去掉归档命令、计数、Completed 加载分支
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml` — 去掉归档导航与按钮
- [x] `src/FlowTask.Desktop/ViewModels/Actions/DeleteTaskViewModel.cs` — 改为物理删除
- [x] `src/FlowTask.Desktop/ViewModels/QuickCaptureViewModel.cs` — 改用共享比较器
- [x] 测试：`SqliteTaskRepositoryTests` / `MainViewModelTests` / `TaskListOrderTests` / 受波及的筛选标题测试
- [x] 共享排序：`FlowTask.Core.Ordering.TaskListOrder`

---

## Progress log

### 2026-09-26

- Completed：所有者预览通过（2026-09-26「没问题」）。SPEC 关闭为 `[DONE]`。
- Decisions：Q1=B 物理删除；Q2=A 看板与项目同一套完成套件；Q3 未完成走优先级/截止/创建，已完成只走勾选顺序（预览后收紧）；Q4=A 小窗对齐。
- Current resume point：已关闭。合入 `dev` 后本分支工作树删除，git 分支保留。
- Subagent/task references：只读调研 explore `f16ea04b-676f-43ae-8a0f-6edfa545e94e`。

---

## Verification

- Automated：`dotnet build FlowTask.sln -v q --nologo` — 0 警告 0 错误。`dotnet test FlowTask.sln --nologo -v q` — 186 通过 / 0 失败。
- Manual：所有者 2026-09-26 预览通过（「没问题」），含已完成排序修正后的二次预览。
- Not run or not covered：视觉「颜色深度」复用现有 opacity 0.4，未改成独立前景色。

### 人工（所有者 2026-09-26 通过）

| # | 操作 | 应看到的现象 | 通过？ |
| :--- | :--- | :--- | :--- |
| M1 | 侧边栏 | 只有「全部任务」和项目列表，没有「已完成归档」「归档全部已完成」 | [x] |
| M2 | 全部任务看板 | 能看到未完成和已完成；以前归档过的任务也回来了 | [x] |
| M3 | 勾选完成 | 留在当前列表，划线+变淡，排到未完成下方 | [x] |
| M4 | 取消勾选 | 恢复样式，回到未完成组按优先级/截止/创建排序的位置 | [x] |
| M5 | 点右侧 X | 已完成和未完成都能删掉，且不再出现（物理删除，无确认） | [x] |
| M6 | 排序 | 未完成：高优先级在上。已完成：**只看出勾先后**，先勾的在最底 | [x] |
| M7 | 小窗 | 同一项目下顺序与主窗一致；勾选置底；没有 X | [x] |

---

## Risks and open questions

Q1–Q4 已于 2026-09-26 由所有者裁决，不再阻塞。未完成组保留截止时间作为第二关键字，开工确认时未反对。

### 其他风险

- 历史已归档任务会重新出现在看板和项目中——这是目标，不是缺陷；若数量很大，列表会变长，不在本轮做分页。
- 历史 `IsDeleted=true` 行仍被查询藏住；新的 `X` 走物理删除。
- 物理删除无确认框、不可撤销。误点 `X` 即永久丢失。这是 Q1=B 的直接代价。

---

## Lessons learned

全局「已完成归档」是在项目分类落地之前，为「勾选后立刻消失」做的补丁。项目一旦成为主分类维度，第二层归档状态就会和「任务都有价值、应能看见」冲突。完成只应降权，删除只应走显式 `X`。

---

## Related documents

- SPECs：
  - [`spec-task-complete-before-archive[SUPERSEDED]`](./spec-task-complete-before-archive[SUPERSEDED].md)（已被本 SPEC 取代）
  - [`spec-quick-window-single-project-list[DONE]`](../quick-capture/spec-quick-window-single-project-list[DONE].md)
  - [`spec-create-task-inherits-selected-project[DONE]`](../main-window/spec-create-task-inherits-selected-project[DONE].md)
  - [`spec-sidebar-selection-consolidation[DONE]`](../main-window/spec-sidebar-selection-consolidation[DONE].md)
- ADRs：无新增
- Rules：`rule-no-invented-user-behavior.md`；`rule-spec-review-gate.md`；`rule-doc-boundary.md`
- Analysis：无

# spec-create-task-inherits-selected-project: 新建任务继承当前选中项目

## Metadata

- **ID**: spec-create-task-inherits-selected-project
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf
- **Created Date**: 2026-09-24
- **Last Updated**: 2026-09-24

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-24）「一起做」，本 SPEC 与 `spec-remove-tag-feature` 同批开工。**

**上游依据**：
- 用户原话（本轮，2026-09-24）：「选中某个project时，应该直接在对应project创建，而不是创建到未分类。」
- 历史先例：[rule-no-invented-user-behavior](../../rules/rule-no-invented-user-behavior.md) §1.1 记录了同类缺陷曾被用户实测证伪：
  「在某个创建的项目下新增一个内容，但是内容最后出现在全部任务，还需要手动配置到项目才能纳入，这明显不符合直觉」。
  本 SPEC 是对该历史缺陷的补救，非新发明的交互假设。

---

## 0. 新会话接手须知

1. 现状：`MainViewModel.AddTaskAsync()`（`MainViewModel.cs:566`）调用
   `AddTaskViewModel.ExecuteAsync()`，后者经 `TaskItemFactory.Create()` 创建任务，
   `projectId` 参数从未被传入，实际永远是 `null`（落入未分类）。
2. 当前选中项目保存在 `MainViewModel.CurrentSelection`
   （`ViewSelection` record struct，见 `spec-sidebar-selection-consolidation[DONE]`），
   派生属性 `SelectedProject` 在 `CurrentSelection.Kind == ViewSelectionKind.Project` 时返回对应项目。
3. 本 SPEC **只做**：创建任务时读取当前选中项目并写入 `ProjectId`。
   **不做**：QuickCapture 小窗的项目归属逻辑（该窗口无「当前选中项目」这一状态，语法层的
   `@项目` 归属属于 `spec-quick-window-hotkey-capture`，与本 SPEC 无关，不在此改动）。

---

## 1. Why

### 1.1 现状缺陷

`AddTaskViewModel.ExecuteAsync()`（`ViewModels/Actions/AddTaskViewModel.cs:26-52`）内部调用：

```csharp
var task = TaskItemFactory.Create(_clock, title, priority, dueDate: dueDate);
```

未传入 `projectId`，`TaskItemFactory.Create()` 的该参数默认值为 `null`
（`Core/Models/TaskItemFactory.cs:38-55`）。用户在侧边栏选中具体项目后点击「新建任务」，
新任务仍会落入未分类，需要额外一步手动分配项目才能归入当前项目。

### 1.2 用户依据

用户本轮明确表述：「选中某个project时，应该直接在对应project创建，而不是创建到未分类。」
这与 `rule-no-invented-user-behavior.md` 记录的历史缺陷属同一类问题
（项目上下文在创建时被丢弃），此前已因同类问题造成用户实测返工。

### 1.3 Attribution

`Code Wrong` —— `AddTaskViewModel.ExecuteAsync()` 未读取当前选中项目上下文，
是实现遗漏，不是设计契约本身有问题（`ViewSelection`/`SelectedProject` 契约已存在，只是未被此路径消费）。

---

## 2. What

### 2.1 行为变更

| 当前选中态（`CurrentSelection.Kind`） | 新建任务的 `ProjectId` |
| :--- | :--- |
| `Project`（选中具体项目） | 该项目的 `Id` |
| `Active` / `Completed`（全部任务 / 已完成归档视图） | `null`（维持现状，不推断） |

### 2.2 影响范围（已核实）

**生产代码**：
- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`
  （`AddTaskAsync` 命令方法，行 566 附近）：调用 `AddTaskViewModel.ExecuteAsync()` 时
  新增传入 `SelectedProject?.Id`。
- `src/FlowTask.Desktop/ViewModels/Actions/AddTaskViewModel.cs`
  （`ExecuteAsync` 方法，行 26-52）：新增 `string? projectId` 参数，
  转发给 `TaskItemFactory.Create(..., projectId: projectId)`。

**不涉及**：`TaskItemFactory.Create()` 签名本身已支持 `projectId` 参数，无需改动；
`QuickCaptureViewModel` 及其相关 SPEC 不在本轮范围内。

### 2.3 非目标

| 不做 | 理由 |
| :--- | :--- |
| QuickCapture 小窗的项目归属推断 | 该窗口没有「当前选中项目」状态，属于另一套语法驱动的归属机制（`@项目`），已由独立 SPEC 覆盖 |
| 已存在任务的批量重新归属 | 未提出，超出本次范围 |
| 「全部任务」视图下创建任务时的项目推断 | 用户只明确了「选中项目时」的行为，未提及全局视图下的默认归属，维持现状 `null`，不推断 |

---

## 3. 分阶段实施计划（须先呈现给用户确认）

- [x] **Phase 1: 行为改造**
  - [x] `AddTaskViewModel.ExecuteAsync()` 新增 `projectId` 参数并转发给 `TaskItemFactory.Create()`
  - [x] `MainViewModel.AddTaskAsync()` 传入 `SelectedProject?.Id`
- [x] **Phase 2: 测试**
  - [x] 补单元测试：在项目视图下创建任务 → 新任务 `ProjectId` 等于该项目 Id
  - [x] 补单元测试：在「全部任务」/「已完成」视图下创建任务 → `ProjectId` 仍为 `null`（防回归）
- [x] **Phase 3: 验证**
  - [x] `dotnet build` 0 警告 0 错误 —— 2026-09-24 实测
  - [x] `dotnet test` 全绿，不低于当前基线 —— 2026-09-24 实测 197 通过（基线 195 + 本轮新增 2）
  - [ ] 文档同步（本 SPEC 转 `DONE`，需等待用户人工验证后再转终态）

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| 2026-09-24 | explore | 摸底新建任务的 ProjectId 归属现状 | ses_f2e9f8558ffeCf1e2pWG43kUjk | 确认 `AddTaskViewModel` 未传入 projectId，问题定位准确 |

### 关键决策与状态增量

- **[2026-09-24]** 用户明确提出需求原话，SPEC 创建为 `draft`，尚未获实施确认。
- **[2026-09-24]** 用户确认「一起做」，SPEC 转 `in-progress` 开工。
- **[2026-09-24]** Phase 1-3 机器侧完成：`MainViewModel.AddTaskAsync()` 传入
  `SelectedProject?.Id`；`AddTaskViewModel.ExecuteAsync()` 新增 `projectId` 参数转发给
  `TaskItemFactory.Create()`；补测试 `AddTask_WhileProjectSelected_InheritsSelectedProjectId`
  与 `AddTask_WithoutProjectSelected_StaysUnassigned`；构建 0/0，测试 197 通过。
  等待用户人工验证后再转 `DONE`。

<!-- 以下按 Phase 实际完成情况追加，严禁提前填写 -->

---

## 5. 验证记录

### 机器门禁

- [x] 构建：`dotnet build FlowTask.sln` —— 0 警告 0 错误，2026-09-24 实测
- [x] 测试：`dotnet test` —— 197 通过，2026-09-24 实测

### 人工验证（由用户执行）

- [ ] 侧边栏选中某项目 → 新建任务 → 任务出现在该项目下，不在未分类
- [ ] 侧边栏切到「全部任务」→ 新建任务 → 行为与当前一致（未分类）

---

## 6. Deferred Items

- **发现但未处理**：`design-domain-contract.md` §2.3 与 §3「继承当前上下文」早已规定
  「不想填项目时任务落入系统 Default 项目，`ProjectId` 不可为 null」，但当前代码
  （本 SPEC 改动前后均如此）在「全部任务」视图下创建任务、以及删除项目后的回退路径，
  实际写入的仍是 `null` 而非 `DefaultProject.Id`，与既定契约不一致。
  本轮向用户核实是否要在本次一并修复（涉及是否将「全部任务」视图与 Default 项目的
  信息架构合并），用户原话：「我不想做这个抉择，你自行决定」。
  经评估：合并「全部任务」与 Default 项目的信息架构改动面大（侧边栏结构、
  项目色条显示、`ProjectChoice.None` 语义等均受影响），超出本 SPEC 「继承选中项目」
  的原始范围，故**本轮不处理**，维持现状（`null` 表示未归属）。已登记至
  `docs/specs/README.md` 待办事项索引，供后续单独排期。

---

## 7. Commit Attribution 与经验教训

- **Attribution**: `Code Wrong` —— 创建路径未消费已存在的选中项目上下文。

---

## 8. Lessons Learned

<!-- 执行过程中如实追加，严禁预先编写 -->

# spec-delete-project-cascade-tasks: 删除项目时级联删除其下任务

## Metadata

- **ID**: spec-delete-project-cascade-tasks
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-27）「确认」开工；同日「没问题」预览通过。状态 `done`。**
> 确认条保留；文案「将删除其下 N 条任务，不可撤销」。

**上游依据（用户原话，2026-09-27）**：

> 「目前删除项目仍然提示挂载到 default，这与目前希望达成的目标不符合。删除项目应该直接删除对应的 tasks」
>
> 确认条：需要确认；赞同文案「将删除其下 N 条任务，不可撤销」。

> **⚠ 与既有裁决的冲突（必须显式记录）**：
> `design-domain-contract` §2.3 / §4.2 写明「删除项目绝不删除其下任务，改挂 Default」。
> 本 SPEC 是用户在本轮对话中**推翻此前裁决**，不是撰写者的推断。
> 依 `rule-no-invented-user-behavior.md` §2.1，此推翻已获用户本轮原话确认，可写入需求与契约修订。

---

## Why

项目已成为任务的主分类维度之后，「删项目、任务改挂 Default」会把本该随项目一起结束的内容送进 Default，用户在确认条上还看到「不会被删除」。这与「单项目自主管理」冲突：项目没了，其下任务不应换一个家继续活着。

根因：早期契约把任务当成独立于项目的核心资产、项目只是可选属性。在项目分级落地后，该不变量已经过时，但仓储事务、确认文案、测试仍按旧不变量实现。

Attribution：`design wrong` —— 删除项目的任务保全语义不再符合当前产品目标。

## What

删除**用户项目**时：

1. 仍先出确认条（所有者选择保留确认，不采用行内 `X` 那种零确认）。
2. 文案改为告知将**删除**其下 N 条任务，不再提 Default。
3. 确认后，同一事务内**物理删除**该 `ProjectId` 下的全部任务行，再删除项目行。
4. 系统 **Default** 仍不可删；不想填项目时新建任务落入 Default（R-2.6）不变。

确认条（所有者已赞同）：

| 位置 | 原文案 | 新文案 |
| :--- | :--- | :--- |
| 标题 | `删除项目「{0}」？` | 不变 |
| 说明 | `其下 {0} 条任务不会被删除，将改挂 Default。此操作不可撤销。` | `将删除其下 {0} 条任务，不可撤销。` |
| 侧边栏删除 tooltip | `删除项目（任务会保留）` | `删除项目（其下任务一并删除）` |
| 按钮 | 取消 / 确认删除 | 不变 |

N 为确认时 `CountTasksAsync` 的结果（未删除任务，与用户在列表里能看到的一致）。N=0 时仍用同一句式，不另写空项目文案。

事务内 `DELETE FROM Tasks WHERE ProjectId = ?` 覆盖该项目下全部行（含历史 `IsDeleted=true`），避免项目删掉后留下指向已不存在项目的孤儿行。确认条的 N 仍只统计 `!IsDeleted`。

选中正被删除的项目时，沿用现有 `LoadProjectsAsync` 回退到「全部任务」看板。小窗若正选中该项目，回退 Default（与 `PrepareAsync` 在项目已不存在时的路径一致），并刷新列表。

## Non-goals

- 删除 Default 项目，或给 Default 开删除入口
- 取消确认条（行内 `X` 那种零确认）
- 项目归档 UI（`Project.IsArchived` 仍无入口）
- 回收站 / 撤销栈
- 改变新建任务落入 Default 的 R-2.6 语义
- 物理删除 `IsArchived` 等死字段列

## Constraints and decisions

- Constitution Article 3：先改 REQUIREMENTS + `design-domain-contract`，禁止只改仓储、文档仍写「绝不删除任务」
- Constitution Article 10：不要留「文案说会删、仓储仍改挂 Default」的半废弃状态
- `rule-no-invented-user-behavior`：确认条保留 + 文案已由所有者 2026-09-27 确认；不得再改成零确认或改挂 Default
- `sqlite-net-pcl`：`RunInTransactionAsync` 回调必须用同步连接 API
- TR-1：逻辑仍放在 `RequestDeleteProjectViewModel` / `ConfirmDeleteProjectViewModel`，不把级联细节写回 `MainViewModel`
- 物理删除对齐 `spec-project-managed-tasks` Q1=B（行内 `X` 已走 `PermanentDeleteAsync`）
- Default 不可删：现有 `Request`/`Confirm` no-op + 仓储抛错，保持

已绑定（有用户原话，不另作推断）：

1. 删除项目直接删除对应 tasks，不再改挂 Default
2. 需要确认条
3. 确认说明采用「将删除其下 N 条任务，不可撤销」

## Acceptance criteria

- [x] 删除用户项目的确认条不再出现「改挂 Default」或「任务不会被删除」
- [x] 确认说明为「将删除其下 {N} 条任务，不可撤销。」（N 为当时可见任务数）
- [x] 确认后：项目行消失；其下任务在全部任务看板、该项目视图、数据库中均不存在（物理删除）
- [x] 取消确认：项目与任务都还在，归属不变
- [x] Default 仍不可删；点删除不进入确认态
- [x] 正在查看被删项目时，确认后回到「全部任务」，且看不到刚被级联删除的任务
- [x] 小窗若正选中被删项目，回退 Default 且列表不再含那些任务
- [x] `REQUIREMENTS.md` 与 `design-domain-contract` 不再写「删除项目绝不删除其下任务」
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不退化

## Staged plan

1. **契约**：修订 `REQUIREMENTS.md`（新增级联删除需求，修正 R-4.4「只走行内 X」的表述）与 `design-domain-contract` §2.3 / §4.2；同步实体/接口注释。
2. **仓储**：`SqliteProjectRepository.DeleteAsync` 改为同一事务内物理删除该 `ProjectId` 的任务行再删项目；禁止再 `UPDATE ... SET ProjectId = Default`。
3. **确认 UI**：确认条与 tooltip 换成新文案；`ConfirmDeleteProjectViewModel` 注释改为级联删除。
4. **跨窗**：确认删除后通知小窗刷新；若小窗选中的就是被删项目，回退 Default。
5. **测试**：把「任务保全 / 改挂 Default」断言改成「任务物理删除」；保留 Default 不可删、取消确认、影响条数、选中回退。
6. **机器验证**：`dotnet build` + `dotnet test`；提供人工验证表（不代勾）。

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md` — 记录推翻；级联删除需求；R-4.4 不再暗示任务只能走行内 `X` 删除
- [x] `docs/design/design-domain-contract.md` — §2.3 / §4.2 改为级联物理删除
- [x] `src/FlowTask.Core/Interfaces/IProjectRepository.cs` — `DeleteAsync` 契约
- [x] `src/FlowTask.Core/Models/TaskItem.cs` / `Project.cs` — 字段注释
- [x] `src/FlowTask.Infrastructure/Persistence/SqliteProjectRepository.cs` — 事务改为删任务 + 删项目
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml` — 确认条与 tooltip
- [x] `src/FlowTask.Desktop/ViewModels/Actions/ConfirmDeleteProjectViewModel.cs` — 注释；跨窗刷新
- [x] `src/FlowTask.Desktop/ViewModels/MainViewModel.cs` — 命令注释与 `DeleteAsync` 语义
- [x] `tests/FlowTask.Tests/SqliteProjectRepositoryTests.cs` — 级联删除断言
- [x] `tests/FlowTask.Tests/ProjectInteractionTests.cs` — 确认后任务不在
- [x] 小窗选中被删项目时的回退（实现 + 测试，若现有 `PrepareAsync` 路径不够）
- [x] `docs/specs/README.md` — 由 `scripts/build-spec-index.ps1` 生成，不手改

## Progress log

### 2026-09-27

- Completed：所有者预览通过（2026-09-27「没问题」）。SPEC 关闭为 `[DONE]`，合入 `dev`。
- Decisions：级联物理删除；Default 仍不可删；R-2.6 新建落入 Default 不变；确认条保留。
- Current resume point：已关闭。合入 `dev` 后本分支工作树删除，git 分支保留。
- Subagent/task references：无

## Verification

- Automated：`dotnet build FlowTask.sln -v q --nologo` — 0 警告 0 错误。`dotnet test FlowTask.sln --nologo -v q` — 272 通过 / 0 失败。
- Manual：所有者 2026-09-27 预览通过（「没问题」）。
- Not run or not covered：无。

### 人工（所有者 2026-09-27 通过）

| # | 操作 | 应看到的现象 | 通过？ |
| :--- | :--- | :--- | :--- |
| M1 | 删除含任务的用户项目 | 确认条写「将删除其下 N 条任务，不可撤销」，无 Default 字样 | [x] |
| M2 | 点确认删除 | 项目消失；那些任务在全部任务与数据库中都不在 | [x] |
| M3 | 点取消 | 项目和任务都还在，仍属于该项目 | [x] |
| M4 | 删除空项目 | 确认条 N=0；确认后项目消失 | [x] |
| M5 | Default 行 | 无删除入口；不会进入确认 | [x] |
| M6 | 正在查看该项目时确认删除 | 回到全部任务，看不到刚删的任务 | [x] |

## Risks and open questions

无阻塞开放问题。确认条与文案已由所有者 2026-09-27 裁决。

- 物理删除不可撤销。误确认会丢掉该项目下全部任务。这是保留确认条的直接原因。
- 历史 `IsDeleted=true` 行会随项目一起物理清掉，用户不可见，但与「不留孤儿行」一致。

## Lessons learned

「删除项目绝不删除任务、改挂 Default」是项目分类落地前的保全语义。项目一旦成为任务的家，删家就应带走屋里的东西；确认条的职责是说清会删多少条，而不是承诺任务会换到 Default。

## Related documents

- SPECs：
  - [`spec-project-managed-tasks[DONE]`](./spec-project-managed-tasks[DONE].md)（行内 `X` 物理删除；本 SPEC 把同一删除语义扩到「删项目」）
  - [`spec-classification-ui[DONE]`](../main-window/spec-classification-ui[DONE].md)（历史清单仍写「确认后任务仍在」；以本 SPEC 为准，不回改那份已关闭 SPEC）
  - [`spec-create-task-inherits-selected-project[IN-PROGRESS]`](../main-window/spec-create-task-inherits-selected-project[IN-PROGRESS].md)（删除项目回退已改为指向本 SPEC 的级联删除）
- ADRs：无新增
- Rules：`rule-no-invented-user-behavior.md`；`rule-spec-review-gate.md`；`rule-doc-boundary.md`
- Design：`design-domain-contract.md` §2.3 / §4.2（本轮已修订）
- Requirements：`REQUIREMENTS.md` R-2.6（保留）；R-2.7 / R-4.5（本轮新增）

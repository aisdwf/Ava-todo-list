# spec-codegraph-audit-remediation: Codegraph 扫描问题的整改清单

## Metadata

- **ID**: spec-codegraph-audit-remediation
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

> 证据与推导见 [`docs/analysis/analysis-codegraph-code-audit.md`](../../analysis/analysis-codegraph-code-audit.md)，本文件不重复长文。

## Why

`dev@695d741` 的 Codegraph 静态扫描（79 文件 / 1,336 符号 / 3,218 边）发现 4 条高优先级、8 条中优先级、8 条低优先级问题。构建 0 警告 0 错误、测试 218 全绿，缺陷不在「编不过」，而在数据归属静默漂移、无全局异常兜底、热键竞态，以及文档与实现脱节。

没有一份工作记忆把「要改什么」收成可勾选清单时，后续会话只能重读长篇分析，且容易漏项或擅自开工。

## What

把扫描报告第 4 节的 **H1–H4、M1–M8、L1–L8** 全部列入本 SPEC 的分阶段计划与改动清单。实施在单一 `bugfix/codegraph-audit-remediation` 分支上按报告第 6 节顺序推进，**每条问题一次提交**，全部完成并经所有者预览后再合入 `dev`。扫描类问题以回归测试证明修复，不依赖手工复现。

受影响范围：`FlowTask.Core`、`FlowTask.Infrastructure`、`FlowTask.Desktop`、`FlowTask.Tests`、`.github/workflows`、相关注释与 `AGENTS.md` 测试基线。

## Non-goals

- 不覆盖扫描未审的样式文件、macOS 图标路径、第三方 CVE。
- 不在本文件重写分析报告正文。
- 不把 ORM 特性从 Core 实体迁到 Infrastructure（M5 已定为补 ADR）。
- 不在每条修复后要求所有者预览；仅在 20 条全部完成后打一次预览 exe，等所有者验收后再合 `dev`。

## Constraints and decisions

- 需求 R-2.6：任务必须归属项目；不填则落入 Default。H1/H2 按此裁决。
- Article 6：配置与路径只有一处权威定义（M4）。
- Article 9：禁止用 `Sleep` 做同步（H4）；业务时间走 `IClock`。
- Article 1 / 10：注释与行为必须一致（M6）；同一概念只在一处表达（H2「未归属」）。
- `rule-code-standards`：跨窗弱引用；CI 应以警告当错误构建（M7）。
- **M5（所有者 2026-09-27）**：补 ADR 承认实体上的 sqlite-net 特性；去掉 Core 未使用的 `CommunityToolkit.Mvvm`；**不**把映射移出 Core。
- **分支（所有者 2026-09-27）**：一条 `bugfix/codegraph-audit-remediation`，20 条全做，逐条提交；合 `dev` 前 SPEC 收成 `[DONE]`。
- TR-1：整改时不得把逻辑重新堆回 `MainViewModel`。
- 保持已确认的良好实践：`IClock`、仓储写入漏斗、删除项目的同步事务、`WeakReferenceMessenger`、`UiThread.RunAsync`。

## Acceptance criteria

- [x] 分析报告落在 `docs/analysis/`。
- [x] 本 SPEC 列出 H1–H4、M1–M8、L1–L8，无遗漏。
- [x] 所有者确认本清单后，才允许翻成 `[IN-PROGRESS]` 并改代码。
- [ ] 每条问题在本任务分支修复，回归测试不回退；本清单同步打钩。
- [ ] 全部勾选后本 SPEC 收为 `[DONE]`。
- [ ] 所有者预览通过后再合入 `dev`。

## Staged plan

阶段划分对齐分析报告 §6；全部落在同一 `bugfix/codegraph-audit-remediation` 分支。

1. **H1 + H2** — 归档任务编辑不丢归属；去掉与 R-2.6 冲突的「未归属」。
2. **H3** — 全局异常兜底、可见失败、日志。
3. **H4** — 热键注册改为可等待结果，去掉 `Sleep(50)`。
4. **M4** — 数据库路径单一真源；预览构建隔离日常库。
5. **M7** — PR / 发布前构建+测试门禁。
6. **M1 + M2 + M3** — 列表加载版本、跨窗消息补全、Default 种子竞态。
7. **M5、M6、M8 与 L 类** — ADR、注释对齐、单实例失败即退出、死代码与小债务。

## Change checklist

### 高优先级

- [x] **H1** 编辑已归档项目下的任务时，未改项目下拉不得把 `ProjectId` 写成 null；补回归测试
- [x] **H2** 按 R-2.6 移除或改写 `ProjectChoice.None`；`EnsureDefaultProjectAsync` 的 null→Default 迁移只留在启动路径
- [x] **H3** 注册 UI / Task / AppDomain 未处理异常；日志写入 `%LOCALAPPDATA%\FlowTask\logs`；`InitializeAsync` 失败有可见提示；fire-and-forget 统一记录异常
- [x] **H4** `GlobalHotkeyService.TryStart` 用事件/`TaskCompletionSource` 等待注册结果（带超时）；`_registered` 正确发布；`WndProc` 透传真实 `hWnd`（需运行验证）

### 中优先级

- [ ] **M1** `LoadTasksAsync` / `LoadTasksForSelectedProjectAsync` 用加载序号或 `CancellationToken` 丢弃过期结果
- [ ] **M2** 所有任务写入成功后发总线消息（含删除）；`TaskDeletedMessage` 必须有发送点；抑制逻辑按消息来源而非跨 await 的布尔窗
- [ ] **M3** `EnsureDefaultProjectAsync` 改为 `INSERT OR IGNORE` 或单事务，避免并发主键冲突
- [x] **M4** 抽出单一 `DatabaseLocation` / 连接工厂；预览与 `dotnet run` 默认可指向独立 db 文件
- [ ] **M5** 补 ADR 承认实体上的 sqlite-net 特性；去掉 Core 未使用的 `CommunityToolkit.Mvvm`
- [ ] **M6** 对齐注释与文档：删除项目改挂 Default、删除 `TaskTags` 表述、清理 `TaskFilter.Settings` / `SaveDefaultDueOffsetAsync` 注释、`AGENTS.md` 测试基线改为 218
- [ ] **M7** PR 工作流：`TreatWarningsAsErrors=true` 的 build + test；Release 工作流在 publish 前跑测试
- [ ] **M8** 单实例拿不到锁则退出并提示；强杀前校验进程路径；替换管道校验对端

### 低优先级

- [ ] **L1** 仓储提供按项目 `GROUP BY` 计数，去掉 `RefreshCountsAsync` / `LoadProjectsAsync` 的 N+1
- [ ] **L2** 清理死代码，或在对应 SPEC 写明保留理由：`TaskFilter.Settings`、`CurrentFilter`、`knownProjects`、测试专用 `AssignProjectAsync`/`SetDueDateAsync`、`EditDueDate`、`IsDeleted`、两个 `Class1.cs`、无查询的 Title `[Indexed]`
- [ ] **L3** 托盘跨午夜后刷新到期文案 / 逾期样式（或可注入时钟）
- [ ] **L4** 启动路径只调用一次 `LoadAppearanceAsync`
- [ ] **L5** 落库成功后再回写实体，或写入失败时回滚（编辑 / 到期日 / 重命名 / 勾选）
- [ ] **L6** 项目名唯一性校验；`SortOrder` 取 `MAX+1`；`@项目` 匹配含归档
- [ ] **L7** 默认到期偏移改为编辑缓冲，点保存才写回属性与仓储
- [ ] **L8** 抽出热键注册等待、关闭策略分派等可测逻辑并补单测

## Progress log

### 2026-09-27

- Completed: Codegraph 扫描；分析报告已写；清单入库 `dev`。所有者确认：M5 补 ADR、20 条全做、单分支逐条提交、全部完成后再预览合 `dev`。本 SPEC 翻成 `[IN-PROGRESS]`。H1：归档项目任务编辑只改标题时保留 `ProjectId`；`LoadTasksAsync` 用全部项目（含归档）解析色条；回归测试 `SaveEdit_OnArchivedProjectTask_KeepsAssignmentWhenProjectUnchanged`。H2：移除 `ProjectChoice.None`；新建/清空归属落到 Default；null 迁移拆到 `MigrateNullProjectIdsToDefaultAsync`，仅主窗 `InitializeAsync` 调用。
- Decisions: 一条 `bugfix/codegraph-audit-remediation`；M5 不搬家实体映射。
- Current resume point: 阶段 5，M7（CI 构建+测试门禁）。

## Verification

- Automated: 每条修复必须有会失败的回归测试（或等价的可重复断言）；全量 `dotnet test FlowTask.sln` 不回退。基线开工时 218 通过。
- Manual: 全部完成后打 `preview/bugfix/codegraph-audit-remediation/FlowTask.exe`，由所有者预览。H1 UI 色条、H4 `DefWindowProc`、H3 可见提示等运行态项列入该次预览。
- Not run or not covered: 未做性能压测、CVE 扫描。

## Risks and open questions

- H4 的 `DefWindowProc(IntPtr.Zero)` 是否在本机导致 hwnd=0，需所有者最终预览。
- 若干 Win32 / 双进程路径（H4 注册、M8 互斥、M7 GitHub Actions）单测只能覆盖抽出的策略，运行态留给预览。

## Lessons learned

静态扫描在测试全绿时仍能挖出「静默改数据」和「无声失败」。跨窗消息只覆盖完成态、编辑候选不含归档项目，属于同一类「半套语义」；清单必须按编号收口，避免只修症状。

## Related documents

- Analysis: [analysis-codegraph-code-audit](../../analysis/analysis-codegraph-code-audit.md)
- SPECs: [spec-project-managed-tasks[DONE]](../task-domain/spec-project-managed-tasks[DONE].md)；[spec-cross-window-complete-sync[DONE]](../quick-capture/spec-cross-window-complete-sync[DONE].md)；[spec-quick-window-hotkey-capture[IN-PROGRESS]](../quick-capture/spec-quick-window-hotkey-capture[IN-PROGRESS].md)；[spec-close-to-tray[DONE]](../main-window/spec-close-to-tray[DONE].md)；[spec-viewmodel-command-decomposition[DONE]](../main-window/spec-viewmodel-command-decomposition[DONE].md)
- ADRs: [adr-technology-stack](../../adr/adr-technology-stack.md)
- Rules: `AI_CONSTITUTION.md`；`project-rules.md`；`technical-rules.md`；`rule-code-standards.md`
- Requirements: [REQUIREMENTS.md](../../requirements/REQUIREMENTS.md) R-2.6

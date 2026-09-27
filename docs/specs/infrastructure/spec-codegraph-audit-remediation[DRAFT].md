# spec-codegraph-audit-remediation: Codegraph 扫描问题的整改清单

## Metadata

- **ID**: spec-codegraph-audit-remediation
- **Type**: complex
- **Status**: draft
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

> **本 SPEC 保持 `[DRAFT]`。** 所有者 2026-09-27 原话：「新增 SPEC，把所有修改列为清单，并且标记 draft。完成以后推送到 dev」。
> 本分支只交付扫描报告与整改清单，**不改生产代码**。开工须另经 Gate 1 确认后再翻成 `[IN-PROGRESS]`。
> 证据与推导见 [`docs/analysis/analysis-codegraph-code-audit.md`](../../analysis/analysis-codegraph-code-audit.md)，本文件不重复长文。

## Why

`dev@695d741` 的 Codegraph 静态扫描（79 文件 / 1,336 符号 / 3,218 边）发现 4 条高优先级、8 条中优先级、8 条低优先级问题。构建 0 警告 0 错误、测试 218 全绿，缺陷不在「编不过」，而在数据归属静默漂移、无全局异常兜底、热键竞态，以及文档与实现脱节。

没有一份工作记忆把「要改什么」收成可勾选清单时，后续会话只能重读长篇分析，且容易漏项或擅自开工。

## What

把扫描报告第 4 节的 **H1–H4、M1–M8、L1–L8** 全部列入本 SPEC 的分阶段计划与改动清单。实施时按报告第 6 节顺序拆到独立 `bugfix/*` / `feature/*`，每项修完在本清单打钩。

受影响范围（实施阶段才动代码）：`FlowTask.Core`、`FlowTask.Infrastructure`、`FlowTask.Desktop`、`FlowTask.Tests`、`.github/workflows`、相关注释与 `AGENTS.md` 测试基线。

## Non-goals

- 本 `[DRAFT]` 周期不修改任何 `.cs` / `.axaml` / CI。
- 不把本 SPEC 当成已批准开工。
- 不覆盖扫描未审的样式文件、macOS 图标路径、第三方 CVE。
- 不在本文件重写分析报告正文。

## Constraints and decisions

- 需求 R-2.6：任务必须归属项目；不填则落入 Default。H1/H2 按此裁决。
- Article 6：配置与路径只有一处权威定义（M4）。
- Article 9：禁止用 `Sleep` 做同步（H4）；业务时间走 `IClock`。
- Article 1 / 10：注释与行为必须一致（M6）；同一概念只在一处表达（H2「未归属」）。
- `rule-code-standards`：跨窗弱引用；CI 应以警告当错误构建（M7）。
- `project-rules`：Core 不泄漏持久化细节；M5 要么补 ADR，要么把 ORM 特性移出 Core。
- TR-1：整改时不得把逻辑重新堆回 `MainViewModel`。
- 保持已确认的良好实践：`IClock`、仓储写入漏斗、删除项目的同步事务、`WeakReferenceMessenger`、`UiThread.RunAsync`。
- **合入 `dev` 仍为 `[DRAFT]`**：所有者明确要求清单先入库、暂不开工。这与「实施中 SPEC 须先收成 `[DONE]` 再合 `dev`」不冲突——本分支没有实施。

## Acceptance criteria

- [x] 分析报告落在 `docs/analysis/`。
- [x] 本 SPEC 以 `[DRAFT]` 列出 H1–H4、M1–M8、L1–L8，无遗漏。
- [ ] 所有者确认本清单后，才允许翻成 `[IN-PROGRESS]` 并改代码。
- [ ] 每条问题在对应任务分支修复，回归测试不回退；本清单同步打钩。
- [ ] 全部勾选后本 SPEC 收为 `[DONE]`。

## Staged plan

实施须等所有者确认。阶段划分对齐分析报告 §6。

1. **H1 + H2（建议 `bugfix/*`）** — 归档任务编辑不丢归属；去掉与 R-2.6 冲突的「未归属」。
2. **H3（建议 `feature/*`）** — 全局异常兜底、可见失败、日志。
3. **H4（建议 `bugfix/*`）** — 热键注册改为可等待结果，去掉 `Sleep(50)`。
4. **M4（建议 `feature/*`）** — 数据库路径单一真源；预览构建隔离日常库。
5. **M7（建议 `feature/*`）** — PR / 发布前构建+测试门禁。
6. **M1 + M2 + M3（建议 `bugfix/*`）** — 列表加载版本、跨窗消息补全、Default 种子竞态。
7. **M5、M6 与 L 类** — 分层/ADR、注释对齐、死代码与小债务，可随相关功能顺带。

## Change checklist

### 高优先级

- [ ] **H1** 编辑已归档项目下的任务时，未改项目下拉不得把 `ProjectId` 写成 null；补回归测试
- [ ] **H2** 按 R-2.6 移除或改写 `ProjectChoice.None`；`EnsureDefaultProjectAsync` 的 null→Default 迁移只留在启动路径
- [ ] **H3** 注册 UI / Task / AppDomain 未处理异常；日志写入 `%LOCALAPPDATA%\FlowTask\logs`；`InitializeAsync` 失败有可见提示；fire-and-forget 统一记录异常
- [ ] **H4** `GlobalHotkeyService.TryStart` 用事件/`TaskCompletionSource` 等待注册结果（带超时）；`_registered` 正确发布；`WndProc` 透传真实 `hWnd`（需运行验证）

### 中优先级

- [ ] **M1** `LoadTasksAsync` / `LoadTasksForSelectedProjectAsync` 用加载序号或 `CancellationToken` 丢弃过期结果
- [ ] **M2** 所有任务写入成功后发总线消息（含删除）；`TaskDeletedMessage` 必须有发送点；抑制逻辑按消息来源而非跨 await 的布尔窗
- [ ] **M3** `EnsureDefaultProjectAsync` 改为 `INSERT OR IGNORE` 或单事务，避免并发主键冲突
- [ ] **M4** 抽出单一 `DatabaseLocation` / 连接工厂；预览与 `dotnet run` 默认可指向独立 db 文件
- [ ] **M5** 补 ADR 承认实体上的 sqlite-net 特性，或把映射移到 Infrastructure；去掉 Core 未使用的 `CommunityToolkit.Mvvm`
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

- Completed: Codegraph 扫描；分析报告已写；本 SPEC 以 `[DRAFT]` 收录全部 20 条改动。
- Decisions: 本分支不合入任何代码修复；清单入库后保持 draft，等所有者确认再开工。
- Current resume point: 等待所有者确认本清单（或指定先做哪一阶段）。确认后：翻成 `[IN-PROGRESS]`，从阶段 1（H1+H2）开独立 `bugfix/*`。

## Verification

- Automated: 扫描工作树 `dotnet build FlowTask.sln` → 0 警告 0 错误；`dotnet test FlowTask.sln` → 218 通过 / 0 失败（基线，非本 SPEC 的修复验证）。
- Manual: 分析为静态扫描；H1 UI 色条、H4 `DefWindowProc` 影响标注为需运行验证。
- Not run or not covered: 未做运行时复现、性能压测、CVE 扫描。

## Risks and open questions

- Owner: 是否按 §6 顺序开工，或先做其中几条。
- Owner: M5 接受 ORM 特性留在 Core（补 ADR）还是迁到 Infrastructure。
- Blocker: Gate 1 — 未确认前不得改代码。
- H4 的 `DefWindowProc(IntPtr.Zero)` 是否在本机导致 hwnd=0，需运行验证后再改。

## Lessons learned

静态扫描在测试全绿时仍能挖出「静默改数据」和「无声失败」。跨窗消息只覆盖完成态、编辑候选不含归档项目，属于同一类「半套语义」；清单必须按编号收口，避免只修症状。

## Related documents

- Analysis: [analysis-codegraph-code-audit](../../analysis/analysis-codegraph-code-audit.md)
- SPECs: [spec-project-managed-tasks[DONE]](../task-domain/spec-project-managed-tasks[DONE].md)；[spec-cross-window-complete-sync[DONE]](../quick-capture/spec-cross-window-complete-sync[DONE].md)；[spec-quick-window-hotkey-capture[IN-PROGRESS]](../quick-capture/spec-quick-window-hotkey-capture[IN-PROGRESS].md)；[spec-close-to-tray[DONE]](../main-window/spec-close-to-tray[DONE].md)；[spec-viewmodel-command-decomposition[DONE]](../main-window/spec-viewmodel-command-decomposition[DONE].md)
- ADRs: [adr-technology-stack](../../adr/adr-technology-stack.md)
- Rules: `AI_CONSTITUTION.md`；`project-rules.md`；`technical-rules.md`；`rule-code-standards.md`
- Requirements: [REQUIREMENTS.md](../../requirements/REQUIREMENTS.md) R-2.6

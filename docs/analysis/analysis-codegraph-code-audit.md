# analysis-codegraph-code-audit：基于 Codegraph 的代码潜在问题扫描

## 元信息

- **类型**：exploratory 调研报告（只读扫描，未改动任何代码）
- **扫描对象**：`dev` 分支 `695d741`（在 `feature/codegraph-code-audit` worktree 中扫描）
- **扫描日期**：2026-09-27
- **工具**：Codegraph 索引（79 文件 / 1,336 符号节点 / 3,218 条关系边）+ `rg` 定点检索 + 人工走读
- **基线**：`dotnet build` 0 警告 / 0 错误；`dotnet test` 218 / 218 通过

---

## 1. 结论摘要

代码整体质量较好：编译零警告，测试 218 条全绿，`IClock` 注入、仓储单一写入漏斗、
`WeakReferenceMessenger` 跨窗解耦、TR-1 命令拆分等既有约束基本落实到位。

但扫描发现 **4 个高优先级问题**，集中在「数据归属一致性」与「异常/并发兜底」两类：

| 级别 | 编号 | 一句话 |
| :--- | :--- | :--- |
| 高 | H1 | 编辑「已归档项目」下的任务，保存后项目归属被静默清空 |
| 高 | H2 | 编辑面板仍提供「未归属」选项，与 R-2.6「任务必须归属项目（Default）」冲突，且会被后台静默迁移 |
| 高 | H3 | 启动与后台异步链路没有任何全局异常兜底，数据库异常时用户无提示、无日志 |
| 高 | H4 | 全局热键注册用 `Thread.Sleep(50)` 等待结果，存在竞态，可能导致一次按键触发两次 Toggle |

另有 8 个中优先级、8 个低优先级问题，见第 4 节。建议处置顺序见第 6 节。

---

## 2. 扫描方法与范围

1. `codegraph init` 建立索引后，按「启动与组合根」「SQLite 持久化」「跨窗消息与热键」「外观协调器」
   四条链路调用 `codegraph_explore`，取得调用路径与影响面（blast radius），据此定位热点文件。
2. 用 `rg` 对全库做模式检索：fire-and-forget（`_ = ...Async()`）、`async void`、
   `catch (Exception`、系统时钟直读（`DateTime.Now/Today/UtcNow`）、全局异常处理器、消息发送点等。
3. 对照本仓库规则判定问题：`AI_CONSTITUTION.md`（Article 1/6/9/10）、
   `docs/rules/project-rules.md`（分层）、`docs/rules/technical-rules.md`（TR-1）、
   `docs/rules/rule-code-standards.md`、`docs/requirements/REQUIREMENTS.md`。
4. 跑一遍构建与测试作为基线。

**置信度标注**：每条问题标注「代码确认」（从源码可直接读出）或「需运行验证」（由代码推导，未做运行时复现）。

---

## 3. 代码规模与热点

| 文件 | 非空行 | 说明 |
| :--- | ---: | :--- |
| `src/FlowTask.Desktop/ViewModels/MainViewModel.cs` | 1037 | 聚合根 VM；命令已按 TR-1 拆到 `Actions/`，剩余主要是状态与加载逻辑 |
| `src/FlowTask.Desktop/Styles/EditorialStyles.axaml` | 730 | 样式，本次未深入 |
| `src/FlowTask.Desktop/Views/MainWindow.axaml` | 722 | 主窗布局 |
| `src/FlowTask.Desktop/Appearance/AppearanceCoordinator.cs` | 541 | 主题/材质/色板静态协调器 |
| `src/FlowTask.Desktop/Views/MainWindow.axaml.cs` | 493 | 主窗 code-behind：水波纹、小窗 Toggle、关闭策略 |
| `src/FlowTask.Desktop/ViewModels/QuickCaptureViewModel.cs` | 346 | 小窗 VM |

Codegraph 影响面分析显示以下组件 **3 跳内无测试覆盖**：`GlobalHotkeyService`、`SingleInstanceGuard`、
`AppearanceCoordinator.RefreshMaterialBackground`，以及 `App` / `MainWindow` code-behind。
这些正是 H3、H4 与 M8 所在位置。

---

## 4. 问题清单

### 4.1 高优先级

#### H1 编辑归档项目下的任务会静默清空项目归属

- **位置**：`TaskRowViewModel.BeginEdit`（`TaskRowViewModel.cs:146-155`）、
  `MainViewModel.LoadProjectsAsync`（`MainViewModel.cs:423-461`）、`SaveEditTaskViewModel.ExecuteAsync`
- **推导**：
  1. `ProjectChoices` 只由 `GetActiveProjectsAsync()` 构建，**不含已归档项目**；
  2. 「全部任务」视图用 `GetAllActiveTasksAsync()`，**会显示归属于已归档项目的任务**；
  3. 对这类任务展开编辑时，`BeginEdit` 在候选中找不到其 `ProjectId`，回落为 `ProjectChoice.None`；
  4. 用户只改标题并收起，`SaveEditTaskViewModel` 无条件写回 `task.ProjectId = row.EditProject.ProjectId`，
     即 `null`；下次启动或打开小窗时再被 `EnsureDefaultProjectAsync` 迁到 Default。
  `ToggleEditTaskViewModel` 展开另一行时会先自动保存其它编辑行，同样触发该路径。
- **影响**：用户未做任何项目相关操作，任务却离开了原（归档）项目，且不可察觉。违反 `Project.IsArchived`
  注释所述「归档时任务的 `ProjectId` 保持不变」。
- **相关现象**：`LoadTasksAsync` 的 `projectLookup` 同样只含活跃项目，此类行 `HasProject == true`
  但 `ProjectColorHex` 为空串，色条渲染结果需运行确认。
- **建议**：编辑候选只在用户**显式改动**项目下拉时才写回 `ProjectId`；或候选中补入当前任务所属的归档项目。
  补一条「编辑归档项目下任务不改变归属」的回归测试。
- **置信度**：代码确认（逻辑链完整），需运行验证 UI 表现。

#### H2 「未归属」选项与 R-2.6 冲突，且会被后台静默改写

- **位置**：`ProjectChoice.None`（`TaskRowViewModel.cs:30`）、`MainViewModel.cs:252,443`；
  `SqliteProjectRepository.EnsureDefaultProjectAsync`（`SqliteProjectRepository.cs:175-188`），
  调用点 `MainViewModel.cs:364`、`QuickCaptureViewModel.cs:106`
- **需求**：`REQUIREMENTS.md:103` R-2.6「以 Default 项目取代『全部 / 未归属』切片，任务必须归属项目」。
- **现状**：编辑面板仍可选「未归属」并把 `ProjectId` 写为 `null`；而 `EnsureDefaultProjectAsync`
  在**每次打开小窗**时执行 `UPDATE Tasks SET ProjectId = Default WHERE ProjectId IS NULL`。
  结果是用户刚设成「未归属」，打开一次小窗后它就变成了 Default，且主窗不刷新，直到下次重载才可见。
- **影响**：一个概念两套语义（Article 10）；用户可见的「设了又变」。
- **建议**：按 R-2.6 移除 `ProjectChoice.None`，或将其改为指向 Default 的候选；
  迁移逻辑只保留在启动路径。
- **置信度**：代码确认。

#### H3 启动与后台异步链路无全局异常兜底

- **位置**：
  - `MainWindow.axaml.cs:129-136`：`Opened += async (_, _) => { ... await vm.InitializeAsync(); }`（`async void`）；
  - `Program.cs:17-24`：`catch (Exception ex) { Console.WriteLine(...) }`，而项目是 `WinExe`，**无控制台**；
  - 全库检索不到 `Dispatcher.UIThread.UnhandledException`、`TaskScheduler.UnobservedTaskException`、
    `AppDomain.CurrentDomain.UnhandledException` 的注册；
  - fire-and-forget 调用点：`MainViewModel.cs:536,1100,1110,1175`、`QuickCaptureViewModel.cs:160`、
    `MainWindow.axaml.cs:304,309`。
- **影响**：数据库文件损坏、被占用或磁盘满时，初始化异常要么让进程直接退出，要么被吞掉，
  界面停在空列表；设置写入失败（`CloseActionPersistTask` / `AppearancePersistTask`）同样无感知。
  用户和开发者都拿不到任何诊断信息。
- **建议**：在 `App` 注册三类全局异常处理，落地到 `%LOCALAPPDATA%\FlowTask\logs`；
  `InitializeAsync` 失败时给出可见提示；fire-and-forget 统一经一个记录异常的扩展方法。
- **置信度**：代码确认。

#### H4 全局热键注册结果存在竞态

- **位置**：`GlobalHotkeyService.cs:47-49`（`Thread.Sleep(50); return _registered;`）、`:21`（`_registered` 非 `volatile`）
- **推导**：`RegisterHotKey` 在后台 STA 线程执行，主线程只等 50 ms 就读取结果。若注册晚于 50 ms 完成
  （冷启动、系统繁忙），`TryStart` 返回 `false`，`App` 据此**保留窗内 KeyDown 监听**，
  但系统级热键随后注册成功，两条路径同时生效。这正是 `MainWindow._systemHotkeyActive` 注释描述的
  「一次按键触发两次 Toggle」缺陷的另一个入口；`_isTogglingQuickCapture` 重入锁只能挡住 await 期间的重复，
  挡不住「关闭后立即再开」。
- **规则**：Article 9 明令禁止以 sleep 作为同步手段。
- **附带疑点**：`WndProc` 以 `DefWindowProc(IntPtr.Zero, ...)` 转发（`GlobalHotkeyService.cs:124-125`），
  应传入实际 `hWnd`。若 `WM_NCCREATE` 因此失败，`_hwnd` 为 0，热键退化为线程级注册，
  `Dispose` 中的 `PostMessage(WM_QUIT)` 不会发出，线程靠 `Join(500)` 超时收尾。需运行验证。
- **建议**：用 `ManualResetEventSlim` / `TaskCompletionSource<bool>` 等待注册结果（带超时）；
  `WndProc` 透传 `hWnd`。
- **置信度**：竞态为代码确认；`DefWindowProc` 影响需运行验证。

### 4.2 中优先级

#### M1 任务列表并发加载无版本控制，快速切换可能显示旧结果

- **位置**：`MainViewModel.LoadTasksAsync`（`MainViewModel.cs:541-565`）、
  `QuickCaptureViewModel.LoadTasksForSelectedProjectAsync`（`QuickCaptureViewModel.cs:173-188`）
- **推导**：两处都是「await 查询 → `Clear()` → 逐条 `Add`」，没有取消令牌或请求序号。
  快速从项目 A 切到 B 时，若 A 的查询后返回，列表显示 A 的任务，标题却是 B。
  跨窗消息、`Dispatcher.Post(() => _ = LoadTasksAsync())` 也会与用户操作交错。
- **建议**：加递增的加载序号，丢弃过期结果；或 `CancellationTokenSource` 取消上一次加载。
- **置信度**：代码确认，触发概率与数据量相关。

#### M2 跨窗同步存在缺口

- **位置**：`TaskMessages.cs:13`、`MainViewModel.cs:348,1174-1175`；各 `Actions/*ViewModel`
- **现状**：
  - `TaskDeletedMessage` 只有注册与处理，**全库没有任何发送点**；
  - 主窗的新增、编辑、改到期日、删除都不发消息，只有「勾选完成」发 `TaskSavedMessage`。
    小窗打开期间，主窗的这些改动不会反映到小窗列表；
  - `_suppressTaskSavedReload` 是跨越 `await` 的布尔标记（`MainViewModel.cs:720-731`、
    `QuickCaptureViewModel.cs:194-206`），其间**对端窗口**发来的消息也会被一并丢弃。
- **建议**：所有写入路径统一在仓储成功后发消息（或由仓储装饰器发）；抑制逻辑改为按消息来源判断，而非时间窗口。
- **置信度**：代码确认。

#### M3 `EnsureDefaultProjectAsync` 先查后插存在竞态

- **位置**：`SqliteProjectRepository.cs:175-188`
- **推导**：主窗 `InitializeAsync` 与小窗 `PrepareAsync` 都会调用。首次启动后立即按热键时两者可能并发，
  都读到「不存在」再各自 `InsertAsync`，第二次插入触发主键冲突。结合 H3，该异常会被静默吞掉。
- **建议**：改为 `INSERT OR IGNORE`，或放进同一事务。
- **置信度**：代码确认，触发窗口很窄。

#### M4 数据库路径解析重复三处，且各环境共用同一数据库

- **位置**：`SqliteTaskRepository.cs:27-43`、`SqliteProjectRepository.cs:24-43`、
  `SqliteAppSettingsRepository.cs:16-35`；组合根 `App.axaml.cs:57-63`
- **现状**：
  - 同一段「`LocalApplicationData\FlowTask\flowtask.db`」逻辑复制了三份（Article 6）；
  - 三个仓储各自 `new SQLiteAsyncConnection(dbPath)`。`App.axaml.cs:57` 注释称「保证 SQLite 连接唯一」，
    实际能共享底层连接依赖的是 sqlite-net 按路径池化连接的内部实现，而非代码本身的保证；
  - `preview/main`、`preview/dev`、`preview/feature/*` 与 `dotnet run` **共用同一个用户数据库**。
    功能分支若新增列或写入试验数据，会直接落到日常使用的数据里（`CreateTableAsync` 只增不删列）。
- **建议**：抽出单一 `DatabaseLocation`（或连接工厂）注入三个仓储；支持以环境变量或命令行参数切换数据库路径，
  预览构建默认使用独立文件。
- **置信度**：代码确认。

#### M5 Core 层依赖 SQLite，且带一个未使用的包引用

- **位置**：`FlowTask.Core.csproj:10-12`；`TaskItem.cs`、`Project.cs`、`AppSetting.cs` 均 `using SQLite;`
  并使用 `[Table]` / `[PrimaryKey]` / `[Indexed]`
- **规则**：`project-rules.md` 规定 Core 为领域/接口层，持久化属于 Infrastructure。
  `adr-technology-stack.md` 只记录了选用 sqlite-net，未记录「在领域实体上直接标注 ORM 特性」这一取舍。
- **附带**：Core 引用了 `CommunityToolkit.Mvvm`，但 Core 源码中没有任何使用。
- **建议**：若接受该取舍，补一条 ADR 明确记录；否则把映射移到 Infrastructure。
  移除 Core 中未使用的 `CommunityToolkit.Mvvm` 引用。
- **置信度**：代码确认。

#### M6 注释与文档已与行为漂移

| 位置 | 文档/注释说法 | 实际 |
| :--- | :--- | :--- |
| `TaskItem.ProjectId` 注释 | 项目被删除时置 `null` | 改挂 Default（R-2.6，`SqliteProjectRepository.DeleteAsync`） |
| `Project.IsArchived` 注释 | 删除后任务 `ProjectId` 置 `null` | 同上 |
| `MainViewModel.ConfirmDeleteProjectAsync` 注释（约 `:999`） | 仅 `ProjectId` 置空退回未归属 | 同上 |
| `TaskItem.cs:36` | 标签维度由 `TaskTags` 关联表承担 | 标签功能已于 2026-09-24 移除，无此表 |
| `TaskFilter` 枚举注释（`MainViewModel.cs:18-31`） | 设置页已从筛选枚举中剥离 | 枚举仍保留 `Settings` 成员 |
| `SaveDefaultDueOffsetAsync`（`MainViewModel.cs:653-662`） | 「无效值时恢复到当前值」 | 直接 `return`，不做任何恢复 |
| `AGENTS.md:65` | 测试基线 163 条 | 实际 218 条 |

- **影响**：违反 Article 1（文档与代码共同维护）。后续 AI 会话按注释推理时会得出错误结论。
- **置信度**：代码确认。

#### M7 CI 没有构建与测试门禁

- **位置**：`.github/workflows/` 下只有 `release-windows.yml`，仅在推送 `v*` 标签时触发；`Directory.Build.props`
  中 `TreatWarningsAsErrors=false`
- **现状**：PR 与推送不跑构建和测试；发布流程也**不跑测试就直接发 Release**。
  `rule-code-standards.md` §4 要求 CI 以 `TreatWarningsAsErrors=true` 构建，目前没有任何地方执行。
- **建议**：新增 PR 工作流，执行 `dotnet build -p:TreatWarningsAsErrors=true` 与 `dotnet test`；
  发布工作流在 publish 前先跑测试。
- **置信度**：代码确认。

#### M8 单实例守卫失败时降级为多实例

- **位置**：`SingleInstanceGuard.cs:41-63,131-167,169-212`
- **现状**：
  - 请求退出、强杀之后仍拿不到 Mutex 时返回 `null`，调用方照常启动。两个实例会同时写同一个数据库、争抢同一热键；
  - 按进程名 `FlowTask` / `FlowTask.Desktop` 强杀，会误杀其它构建（如另一个 preview 目录的 exe）；
  - 命名管道接受同会话内任意进程发来的 `replace`，任何本地程序都能让 FlowTask 退出。
- **建议**：拿不到锁时明确退出并提示；强杀前校验进程路径；管道加一次性令牌或校验客户端进程。
- **置信度**：代码确认，发生概率低。

### 4.3 低优先级

| 编号 | 问题 | 位置 | 建议 |
| :--- | :--- | :--- | :--- |
| L1 | 计数 N+1：每次 `LoadTasksAsync` 后，`RefreshCountsAsync` 全量加载任务只为取 `Count`，再对每个项目各发一次 `CountTasksAsync`；`LoadProjectsAsync` 同样逐项目计数 | `MainViewModel.cs:435-439,582-591` | 仓储新增一条 `GROUP BY ProjectId` 计数查询 |
| L2 | 死代码：`TaskFilter.Settings`、恒返回常量的 `CurrentFilter`、`ChangeFilterViewModel` 中的 `_ = filter`、`CaptureInputParser.Parse` 的 `knownProjects` 参数、只被测试调用的 `AssignProjectAsync` / `SetDueDateAsync`、`TaskRowViewModel.EditDueDate` 缓冲（保存时不使用）、从未置 true 的 `TaskItem.IsDeleted`、两个模板 `Class1.cs`、`TaskItem.Title` 上无查询使用的 `[Indexed]` | 多处 | 按 Article 5/10 清理，或在对应 SPEC 登记保留理由 |
| L3 | 到期日文案与逾期样式直读 `DateTime.Today`。注释已声明这是展示层豁免，但应用常驻托盘跨过午夜后，「今天到期」等文案不会刷新 | `DueDateConverters.cs:36,64,80` | 午夜触发一次列表重建，或改为可注入时钟 |
| L4 | 启动时 `LoadAppearanceAsync` 执行两次（`Opened` 中一次，`InitializeAsync` 内又一次），主题字典重复写入 | `MainWindow.axaml.cs:132`、`MainViewModel.cs:360` | 保留一处 |
| L5 | 先改实体再落库：`SaveEditTaskViewModel`、`CommitDueDatePopupViewModel`、`CommitRenameProjectViewModel`，以及勾选时的 `TaskRowViewModel.OnIsCompletedChanged`。写入失败时界面显示未落库的值，与 `Tasks` 只读投影所防的「幽灵数据」相悖 | `Actions/*`、`TaskRowViewModel.cs:121` | 落库成功后再回写实体，或失败时回滚 |
| L6 | 项目名可重复：新建路径不查重；小窗 `@项目` 只在活跃项目中匹配，遇到同名归档项目会再建一个；新项目 `SortOrder = 活跃项目数`，归档/删除后会与既有项目撞序 | `CreateProjectViewModel`、`QuickCaptureViewModel.cs:347-379` | 仓储层做名称唯一性校验；`SortOrder` 取 `MAX + 1` |
| L7 | 默认到期偏移为 TwoWay 绑定，未点「保存」时内存值已变，本次会话即按未保存的值生效，重启后又恢复 | `MainWindow.axaml:620-630` | 改为编辑缓冲，保存时再写回属性 |
| L8 | 测试缺口：热键、单实例、`App` / `MainWindow` code-behind 无自动化测试 | 见第 3 节 | 把可测逻辑（注册结果等待、关闭策略分派）抽出后补单测 |

---

## 5. 确认的良好实践

以下做法经扫描确认有效，后续改动时应保持：

- **时钟注入**：业务层全部经 `IClock` 取时间，`SystemClock` 是唯一直读系统时钟的实现（Article 9）。
- **仓储是唯一写入漏斗**：`SaveTaskAsync` / `SaveProjectAsync` 统一校验标题与名称、断言 `CreatedAt`、归一化 `DueDate`。
- **事务边界注释到位**：`SqliteProjectRepository.DeleteAsync` 明确说明事务回调必须使用同步连接，避开了 sqlite-net 的常见陷阱。
- **跨窗解耦**：窗口之间无强引用，统一走 `WeakReferenceMessenger` 与视图层事件。
- **TR-1 已落实**：`MainViewModel` 的大部分命令已拆为 `Actions/*ViewModel`，聚合根只保留状态与转发。
- **UI 线程回归**：`UiThread.RunAsync` 把消息续延拉回 UI 线程，避免在线程池中修改 `ObservableCollection`。

---

## 6. 建议处置顺序

| 顺序 | 问题 | 理由 | 建议任务类型 |
| :--- | :--- | :--- | :--- |
| 1 | H1、H2 | 直接影响用户数据归属，且二者同源（项目候选与 Default 语义） | 合并为一个 `bugfix/*` |
| 2 | H3 | 其它问题一旦触发都会被它放大为「无声失败」 | `feature/*`（新增日志与全局兜底） |
| 3 | H4 | 用户可感知的热键双触发 | `bugfix/*` |
| 4 | M4 | 预览构建污染日常数据，越早隔离越好 | `feature/*` |
| 5 | M7 | 为后续修复提供回归保护 | `feature/*` |
| 6 | M1、M2、M3 | 并发与同步一致性，可合并处理 | `bugfix/*` |
| 7 | M5、M6、L 类 | 文档与结构债务，可随相关功能顺带处理 | 视情况 |

---

## 7. 局限与未覆盖

- 本次为**静态扫描**，未做运行时复现；标注「需运行验证」的条目应先复现再修。
- `EditorialStyles.axaml` 等样式文件、macOS 平台路径（`MacOSApplicationIcon`）未深入审查。
- 未做性能压测；L1 的影响在任务量较小时可忽略。
- 未评估第三方依赖版本的已知漏洞（Avalonia 11.2.3、sqlite-net-pcl 1.9.172 等）。

---

## 8. 相关文档

- 整改清单（工作记忆，`[DONE]`）：[`spec-codegraph-audit-remediation[DONE].md`](../specs/infrastructure/spec-codegraph-audit-remediation[DONE].md)
- 规则：[`AI_CONSTITUTION.md`](../../AI_CONSTITUTION.md)、[`project-rules.md`](../rules/project-rules.md)、
  [`technical-rules.md`](../rules/technical-rules.md)、[`rule-code-standards.md`](../rules/rule-code-standards.md)
- 需求：[`REQUIREMENTS.md`](../requirements/REQUIREMENTS.md)（R-2.6）
- ADR：[`adr-technology-stack.md`](../adr/adr-technology-stack.md)

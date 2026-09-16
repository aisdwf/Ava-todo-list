# spec-task-contract-and-clock: 任务数据契约扩展、IClock 整改与编辑闭环

## Metadata

- **ID**: spec-task-contract-and-clock
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-14
- **Last Updated**: 2026-09-14

**上游依据**：
- 需求：[REQUIREMENTS](../../requirements/REQUIREMENTS.md) R-2.1 / R-2.2 / R-2.4 / R-2.5
- 设计：[design-domain-contract](../../archived/design-task-classification-superseded.md)（Approved）

**本 SPEC 是 design-domain-contract 三段实施拆分中的第 1 段。**
后续两段（主窗口分类交互、小窗改造）另立 SPEC，依赖本段产出的契约。

---

## 1. Why（问题与背景）

### 1.1 契约层缺口

design-domain-contract 确立的数据契约尚未落地。当前 `TaskItem` 无项目归属与标签概念，
仅有 P1/P2/P3 三档优先级，**不足以支撑分类需求**（用户明示）。

### 1.2 既有缺陷（与需求无关，但必须在本段一并整改）

均经代码核实，来源 [design-domain-contract（技术缺陷已并入）](../../archived/design-task-domain-superseded.md)：

1. **时区区间错位**：`SqliteTaskRepository.GetTodayTasksAsync:68` 以
   `DateTime.Today.ToUniversalTime()` 为起点 + 24h 为终点计算「今日」。
   在非 UTC 时区下，该区间与用户心中的「今天」错位。
2. **违反 Article 9**：同一方法直接依赖系统时钟，使「今日」边界不可测试 ——
   无法编写「跨越午夜时任务是否正确移出今日视图」这类用例。
3. **`DueDate` 为死字段**：无任何 UI 写入路径，致「今日聚焦」视图恒为空。
4. **任务创建后无法修改**：除勾选完成与软删除外，标题、优先级等一律不可编辑。
   打错一个字只能删除重建，且重建会丢失原 `CreatedAt`。

### 1.3 为什么契约必须先行

用户明确要求「少一次返工」。若先做 UI 再改契约，
小窗与主窗口的行布局需随字段变化重做。
契约先落地，后续两段才有稳定基础。

**Attribution**：`N/A (Feature)` 为主，叠加 `Design Wrong`（时区语义）
与 `Test Wrong`（时钟不可注入导致无法测试）。

---

## 2. What（范围与边界）

### 2.1 目标

- 新增 `Project` 独立实体与仓储能力（含删除时置空关联任务的事务语义）
- `TaskItem` 新增 `ProjectId` / `Tags` 两列
- 引入 `IClock` 抽象，消除全部对系统时钟的直接依赖
- 修正「今日」区间的时区语义
- 补齐任务编辑能力（ViewModel 层命令与仓储支持）

### 2.2 非目标（本段明确不做）

| 不做 | 归属 |
| :--- | :--- |
| 主窗口项目侧边栏、任务行分类呈现、项目管理 UI | 第 2 段 SPEC |
| 小窗列表、勾选、键盘导航、拖拽区改造 | 第 3 段 SPEC |
| 编辑态的 XAML 视图与展开动效 | 第 2 段 SPEC（本段只做 ViewModel 与仓储支撑） |
| 启用 `Description` | design-domain-contract §2.4：与分类主题正交，避免混入变更边界 |
| 回收站视图 | 未纳入 REQUIREMENTS，暂不处理 |
| 输入语法 `#项目` `@标签` | design-domain-contract §5：初版不做，仅预留结构 |
| 打包整改（R-3） | design-domain-contract：置于功能完成之后 |

### 2.3 影响的文件与模块

**新增**：
- `src/FlowTask.Core/Models/Project.cs`
- `src/FlowTask.Core/Interfaces/IClock.cs`
- `src/FlowTask.Core/Interfaces/IProjectRepository.cs`
- `src/FlowTask.Infrastructure/Time/SystemClock.cs`
- `src/FlowTask.Infrastructure/Persistence/SqliteProjectRepository.cs`
- `tests/FlowTask.Tests/FakeClock.cs`（测试替身）
- `tests/FlowTask.Tests/SqliteProjectRepositoryTests.cs`
- `tests/FlowTask.Tests/ClockAndTodayRangeTests.cs`

**修改**：
- `src/FlowTask.Core/Models/TaskItem.cs`（+ `ProjectId` / `Tags`，`CreatedAt` 默认值处置）
- `src/FlowTask.Core/Interfaces/ITaskRepository.cs`（+ 按项目查询、+ 标签提取）
- `src/FlowTask.Infrastructure/Persistence/SqliteTaskRepository.cs`（注入 `IClock`、修时区）
- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`（+ 编辑命令、注入 `IClock`）
- `src/FlowTask.Desktop/ViewModels/QuickCaptureViewModel.cs`（注入 `IClock`）
- `src/FlowTask.Desktop/App.axaml.cs`（装配 `IClock` 与项目仓储）
- `tests/FlowTask.Tests/*`（既有用例适配新构造签名）

### 2.4 契约决策（来自 design-domain-contract，此处为实现约束）

**`CreatedAt` 赋值归属**：依 design-domain-contract §3.3 倾向并确认 ——
实体默认值改为 `default`，由**创建方经 `IClock` 显式赋值**。
理由：实体无法被注入依赖；让创建时间成为被显式决定的业务事实，而非隐式副作用。

> **风险与缓解**：此改动使「忘记赋值」从不可能变为可能（原先由字段初始化器兜底）。
> 缓解：仓储 `SaveTaskAsync` 在插入路径断言 `CreatedAt != default`，
> 违反则抛异常快速失败，而非静默写入零值。

**时区语义**（design-domain-contract §3）：
- `DueDate` 存**本地日期语义**（当日零点，无有意义的时间部分）
- `CreatedAt` / `CompletedAt` 存 **UTC**
- 「今日」判断在**本地日期维度**进行

**标签存储**（design-domain-contract §4.3）：`,` 分隔字符串，输入时过滤标签内的 `,`，
大小写不敏感去重但保留首次输入的原始大小写。

**项目删除**（design-domain-contract §4.2）：单事务完成「置空关联任务 `ProjectId`」+「删除项目行」。
**绝不删除其下任务。**

---

## 3. 分阶段实施计划

- [x] **Phase 1: 时间抽象与时区整改**
  - [x] 新增 `IClock`（`UtcNow` + `Today` 本地日期）
  - [x] 新增 `SystemClock` 实现
  - [x] 新增 `FakeClock` 测试替身
  - [x] `SqliteTaskRepository` 注入 `IClock`，重写「今日」区间为本地日期维度
  - [x] 补测试：跨午夜边界、非 UTC 时区区间正确性（12 例）
- [x] **Phase 2: TaskItem 契约扩展**
  - [x] `TaskItem` 新增 `ProjectId` / `Tags`
  - [x] `CreatedAt` 默认值改 `default`，创建方显式赋值
  - [x] `SaveTaskAsync` 插入路径断言 `CreatedAt != default`
  - [x] 标签规范化工具 `TagNormalizer`（已由 spec-tag-entity 的 `TagName` 与实体关联模型取代）
  - [x] 补测试：自动迁移兼容性（3 例）、标签规范化与工厂（20 例）
- [x] **Phase 3: Project 实体与仓储**
  - [x] 新增 `Project` 模型与 `IProjectRepository`
  - [x] `SqliteProjectRepository`：增删改查、归档、删除时事务置空关联
  - [x] `ITaskRepository` 增加按项目查询、按标签查询与标签提取
  - [x] 补测试：删除项目不删任务且置空归属、归档保留归属（13 例）
- [x] **Phase 4: 编辑闭环（ViewModel 层）**
  - [x] `MainViewModel` 增加 `CommitTaskEdit` / `AssignProject` / `SetDueDate`
  - [x] 空标题恢复原值语义
  - [x] 创建路径统一至 `TaskItemFactory`（为未来输入语法预留单一入口）
  - [x] 补测试：编辑落库、空标题回滚、时钟注入生效（11 例）
- [x] **Phase 5: 装配与验证**
  - [x] `App.axaml.cs` 装配 `IClock`
  - [x] ~~装配项目仓储~~ —— **本段不装配，理由见 §4 决策记录**
  - [x] 既有测试适配新构造签名
  - [x] 全量 `dotnet build` + `dotnet test` 绿灯
  - [x] 文档同步与索引更新

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| — | — | 本 SPEC 未派发 subagent（改动集中、上下文已完整持有） | — | — |

### 关键决策与状态增量

- **[2026-09-14]** SPEC 建立，状态直接置 `in-progress`（开工铁律）。
- **[2026-09-14]** 前置实测已完成（见 design-domain-contract §1.1）：
  `sqlite-net-pcl` 自动 `ALTER TABLE ADD COLUMN`，新增列**无需迁移脚本**，
  既有行保留、新列取默认值。故 Phase 2 / 3 不含迁移脚本工作项。
- **[2026-09-14]** 同时确认 `sqlite-net-pcl` **无关系映射能力**
  （无 `OneToMany` / `ManyToMany` / `GetChildren`），
  故项目-任务关联全部手写查询，标签采用字符串方案而非关联表。

<!-- 以下按 Phase 实际完成情况追加，严禁提前填写 -->

### Phase 执行记录

- **[Phase 1]** `IClock` 暴露 `UtcNow`（时刻，UTC）与 `Today`（日历日，本地、
  Kind 为 `Unspecified`）两个语义分明的成员，使调用方在选成员时即被迫明确语义。
  「今日」区间改为 `DueDate < clock.Today.AddDays(1)`，**全程不做任何时区换算** ——
  不换算正是正确性的保证。
- **[Phase 1 · 连带缺陷]** 整改时发现「今日聚焦」原本还漏掉逾期任务：
  原条件为 `DueDate >= 今天 && DueDate < 明天`，昨天到期未完成的任务
  既不在「今日」也不显眼于「全部」。该问题与时区无关，
  但同属「今日语义」这一内聚概念且 design-domain-contract §3.4 已有明确定义，故一并修正。
- **[Phase 2 · 命名冲突]** 任务工厂初名 `TaskFactory`，与
  `System.Threading.Tasks.TaskFactory` 冲突 —— 项目启用 `ImplicitUsings`，
  该冲突会迫使每个调用点写全限定名。已重命名为 `TaskItemFactory`，
  同时更准确表达其产出实体（Article 5：命名反映身份）。
- **[Phase 3 · 事务 API 陷阱]** `RunInTransactionAsync` 的回调签名经查证为
  `Action<SQLiteConnection>` —— 传入**同步**连接。
  回调内若误用异步 API，操作会脱离事务边界，原子性静默失效且不报错。
  已确认使用同步 `conn.Execute` / `conn.Delete`。
  项目删除以单条 `UPDATE Tasks SET ProjectId = NULL WHERE ProjectId = ?`
  批量置空，避免读改写竞态。
- **[Phase 5 · 范围修正]** **项目仓储未装配进 `App.axaml.cs`。**
  原计划包含此项，实际未做，理由：`MainViewModel` 本段不消费项目数据
  （项目侧边栏属第 2 段 SPEC）。若此时注入一个无人使用的依赖，
  属于为未落地功能提前搭骨架，与 Article 10 相悖。
  第 2 段 SPEC 引入项目 UI 时一并装配。
  **实测确认后果**：真实数据库中 `Projects` 表因此尚未创建 —— 这是预期状态，
  `SqliteProjectRepository.InitializeAsync` 会在首次使用时自动建表，
  且已有回归测试覆盖「在既有旧库上创建 Projects 表」这一路径。
- **[Phase 5 · 真实数据库升级验证]** 对用户实际的
  `~/Library/Application Support/FlowTask/flowtask.db`（旧 schema、4 行数据）
  执行了升级验证（事前已备份）：
  - 升级后列：`... IsDeleted, ProjectId, Tags` — 新列已追加
  - 4 行数据全部保留，`ProjectId` / `Tags` 均为 `NULL`（正常的「未归属/无标签」状态）
  - 确认了 design-domain-contract §1.1 的迁移结论在真实数据上成立

---

## 5. 验证记录

### 机器门禁

- [x] 构建：`dotnet build FlowTask.sln` — **0 警告 0 错误**
- [x] 测试：`dotnet test` — **113 通过 / 0 失败**
  - 变更前基线 54 例（`AppearanceCoordinator` 13 + `Converter` 7 +
    `MainViewModel` 16 + `SqliteTaskRepository` 3，其余为 Theory 展开）
  - 本段新增 **59 例**：时钟与今日区间 12、标签与工厂 20、
    项目仓储 13、schema 迁移 3、编辑闭环 11
- [x] 真实数据库升级实测（见 §4 Phase 5 记录），数据零丢失

### 人工验证

本段产出集中在 Core / Infrastructure / ViewModel 层。

- [x] 应用可正常启动（实机运行 18 秒，进程存活，日志无异常）
- [ ] **项目分类与编辑态的实际交互 —— 本段无法验证**

> **如实声明（依 spec-editorial-and-ripple-theme 教训 4）**：
> 本段新增的编辑命令（`CommitTaskEdit` / `AssignProject` / `SetDueDate`）
> 与项目仓储虽有测试覆盖其代码路径，但**当前没有任何界面能触达它们** ——
> 编辑态视图与项目侧边栏属第 2 段 SPEC。
>
> 「进程存活 18 秒」只证明依赖装配与数据库升级无误，
> **不证明任何功能可用**，不得以此冒充功能验证。
> 真正的人工验证须待第 2 段完成后进行。

> **如实声明**：本段的编辑命令、项目仓储虽已测试覆盖代码路径，
> 但**没有任何界面能触达它们**。这是分段实施的预期状态，非遗漏。
> 依 spec-editorial-and-ripple-theme 教训 4，不以「启动不崩」冒充功能验证。

---

## 6. Deferred Items（显式追踪）

- **TODO(desc-field): [2026-10-05] `Description` 仍为死字段。**
  - 本段明确不启用（§2.2），避免混入分类变更边界。
  - Owner: aisdwf
- **TODO(recycle-bin): [2026-10-05] 软删除任务无恢复入口，`PermanentDeleteAsync` 仍无调用方。**
  - 未纳入 REQUIREMENTS，需先决定是否作为需求。
  - Owner: aisdwf
- **TODO(appearance-persist): [2026-09-28] 外观偏好重启后回退默认（承自 spec-editorial-and-ripple-theme）。**
  - Owner: aisdwf

---

## 7. Commit Attribution 与经验教训

- **Attribution**: `N/A (Feature)` + `Design Wrong`（时区语义）+ `Test Wrong`（时钟不可注入）
- **Root Cause**：
  - 时区错位源于把「日期」当「时刻」处理 —— `DueDate` 回答「日历上的哪一天」，
    却以 UTC 时刻存取，跨时区必然漂移；
  - 时钟不可注入使该缺陷无法被测试捕获，属 Article 9 所禁止的
    「业务分支依赖不可控外部状态」的直接后果。
- **Lessons Learned**: 见 §8。

---

## 8. Lessons Learned

1. **「日期」与「时刻」是两种不同的类型，不可共用同一存储策略。**
   `DueDate`（日历日）与 `CreatedAt`（事件时刻）基准不同是有意设计，
   强行统一为 UTC 正是原缺陷的根源。
   本次在 `IClock` 上同时暴露 `UtcNow` 与 `Today` 并加以区分命名，
   使调用方在选择成员时就被迫明确语义，而非事后才发现基准错配。
   **推论**：修正时区问题的正确方向往往是**减少换算**而非增加换算 ——
   `DueDate` 现在全程不做任何时区转换，因为任何转换都会破坏「哪一天」这一语义。

2. **修一个缺陷会暴露相邻缺陷，须同时警惕范围蔓延与漏改。**
   整改时区区间时发现「今日」原本还漏掉逾期任务。
   判断标准是：该问题是否属于同一内聚概念、是否已有明确设计定义。
   本例两者皆是（同属「今日语义」，design-domain-contract §3.4 已定义），故一并修正；
   若是无关问题（如 `Description` 死字段），则登记为待办而不混入。

3. **框架 API 的同步/异步边界必须查证签名，不能凭直觉。**
   `RunInTransactionAsync` 名字带 Async，但回调是
   `Action<SQLiteConnection>` —— 同步连接。
   回调内误用异步 API 会脱离事务边界，**原子性静默失效且不报错**。
   这类「能编译、语义错」的陷阱只能靠读签名发现，
   与 spec-editorial-and-ripple-theme 教训 6（框架能力边界须实验确认）同源。

4. **一次性的实测结论必须固化为回归测试，否则等于没有验证。**
   design-domain-contract §1.1 的「新增列无需迁移脚本」原本只是临时项目里的一次实测。
   已补 `SchemaMigrationTests` 将其固化 ——
   否则日后升级 `sqlite-net-pcl` 若行为变化，将无任何机制发现，
   而后果是用户数据丢失这一最高级别故障。

5. **计划中的工作项若在执行中判定为不该做，须显式记录理由而非默默勾掉。**
   本段原计划装配项目仓储，实际未做（`MainViewModel` 尚不消费项目数据，
   提前注入无人使用的依赖属于为未落地功能搭骨架）。
   这个决定本身是对的，但如果只是把复选框一勾了事，
   下一段接手者会误以为已装配完成。
   已在 §4 与 §5 双处记录该偏离及其后果。

6. **分段实施必须如实声明「已实现但无界面可达」。**
   本段的编辑命令与项目仓储有测试覆盖但无 UI 入口。
   若在验证记录中含糊为「功能完成」，会让下一段接手者误判进度，
   也会让用户以为可以立即试用。
   「进程存活」只证明装配无误，绝不等于功能可用。

7. **默认值的移除会把「不可能」变成「可能」，须补兜底断言。**
   `CreatedAt` 原有字段初始化器兜底，改为显式赋值后
   「忘记赋值」成为一类新的可能故障。
   因此在仓储这一唯一写入漏斗上加了快速失败断言 ——
   移除隐式行为时，必须同时建立显式的违约检测。

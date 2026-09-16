# spec-mvvm-infrastructure: FlowTask (跨平台待办与快速捕捉应用) 核心骨架与基础设施

## Metadata

- **ID**: spec-mvvm-infrastructure
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-14
- **Last Updated**: 2026-09-14

---

## 1. Why (Problem & Context)

现代桌面用户在工作学习中常常产生瞬间的突发灵感或临时待办，传统任务管理应用唤醒缓慢、启动沉重，打断思考心智。
本项目旨在打造一个以 **“快速捕捉（Quick Capture）+ 沉浸管理（Main Dashboard）”** 为核心哲学的现代化极简跨平台任务管理客户端（命名候选：**FlowTask** / **ZenTask**）。

- **Core Motivation**:
  - 本地优先（Local-First），微秒级响应；
  - 极简轻巧，支持全局快速捕捉小窗与双向无缝数据同步；
  - 跨平台支持（Windows 优先兼顾 macOS 本地开发与体验）。
- **Current Limitation**:
  - 刚完成环境初始化，尚无工程源码与构建基线。

---

## 2. What (Scope & Boundaries)

- **Goals**:
  1. 搭建 .NET 8 + Avalonia 11 多工程架构：
     - `FlowTask.Core`：领域实体（`TodoTask`、`TaskCategory`、`TaskPriority`）、仓储接口、领域事件；
     - `FlowTask.Infrastructure`：SQLite 数据引擎（`sqlite-net-pcl`）、平台服务；
     - `FlowTask.Desktop`：Avalonia 11 MVVM 客户端（包含主管理窗口与快捷捕捉小窗原型）；
     - `FlowTask.Tests`：xUnit 单元测试工程。
  2. 实现基于 `sqlite-net-pcl` 的基础增删改查仓储与事务支持；
  3. 实现基于 `WeakReferenceMessenger` 的双视窗数据事件流转模型；
  4. 验证 macOS 本地构建与自动化单元测试全部通过。
- **Non-Goals (Out of Scope)**:
  - 复杂的离线语音 Whisper 识别模型集成（延后至后续 SPEC）；
  - 云端多设备同步协议与账号体系（聚焦 Local-first）。
- **Impacted Files & Modules**:
  - `FlowTask.sln`
  - `src/FlowTask.Core/`
  - `src/FlowTask.Infrastructure/`
  - `src/FlowTask.Desktop/`
  - `tests/FlowTask.Tests/`

---

## 3. Phased Implementation Plan (分阶段实施计划)

- [ ] **Phase 1: 脚手架工程矩阵初始化与构建基线建立**
  - [ ] 创建 `FlowTask.sln`，新建 `FlowTask.Core`、`FlowTask.Infrastructure`、`FlowTask.Desktop`、`FlowTask.Tests`；
  - [ ] 接入 `CommunityToolkit.Mvvm`、`sqlite-net-pcl`、`SQLitePCLRaw.bundle_green`、`Avalonia` 基础依赖；
  - [ ] 验证解决方案编译零警告零报错。
- [ ] **Phase 2: 领域层与 SQLite 持久化引擎实现**
  - [ ] 设计核心实体（`TodoTask`、`TaskPriority`、`TaskCategory`）；
  - [ ] 编写 `ITaskRepository` 接口与 `SqliteTaskRepository` 异步实现（包含自建目录与连接初始化）；
  - [ ] 编写 xUnit 单元测试验证任务创建、持久化读取、修改与软删除。
- [ ] **Phase 3: 桌面端视窗与 MVVM 响应式响应链路**
  - [ ] 配置 Avalonia 11 Fluent 主题与窗口样式；
  - [ ] 构建 `MainViewModel` 与 `MainWindow.axaml`（任务列表呈现、添加交互）；
  - [ ] 构建 `QuickCaptureViewModel` 与 `QuickCaptureWindow.axaml`（极简悬浮输入框、回车提交）；
  - [ ] 验证弱引用消息总线 `TaskCreatedMessage` 跨窗口无感数据同步。
- [ ] **Phase 4: 门禁验证与交付基线检查**
  - [ ] 运行 `dotnet test` 确保 100% 通过；
  - [ ] 验证 Windows 交叉编译命令（`dotnet publish -r win-x64`）；
  - [ ] 更新 SPEC 状态与执行跟踪记录。

---

## 4. Progress & Subagent Traceability (执行记录与上下文追踪)

### Subagent Log

| Timestamp | Subagent Type | Task Dispatched | Task ID | Key Outcome / Findings |
| :--- | :--- | :--- | :--- | :--- |
| 2026-09-14 00:40 | general | 脚手架与方案调研 | N/A | 确定 FlowTask 作为基准命名，确定 SQLite + MVVM Toolkit 组合 |

### Key Decisions & State Deltas

- **[2026-09-14]**: 遵照用户要求，将 `AI_CONSTITUTION.md`、`docs/`、`agent/`、`claude.md` 全部加入 `.gitignore` 保护私有工作流，Author 设置为 `aisdwf`。

---

## 5. Verification Chain (验证矩阵)

- **Machine Gate (Required First)**:
  - [ ] Type Check & Build: `dotnet build FlowTask.sln`
  - [ ] Unit Tests: `dotnet test tests/FlowTask.Tests/FlowTask.Tests.csproj`
  - [ ] Cross-Platform Build Check: `dotnet publish src/FlowTask.Desktop/FlowTask.Desktop.csproj -r win-x64 --self-contained false`
- **Human Verification (After Machine Pass)**:
  - [ ] 本地启动运行 `dotnet run --project src/FlowTask.Desktop`，主窗口与浮窗界面加载正常；
  - [ ] 浮窗键入任务后，主窗口即时响应刷新。

---

## 6. Commit Attribution & Lessons Learned

- **Attribution**: N/A (Feature)
- **Root Cause & Lessons Learned**:
  - 初次构建多工程时，明确将公共库与平台相关库隔离，保持 `FlowTask.Core` 为无任何 UI 依赖的净室纯代码，最大化可测性。

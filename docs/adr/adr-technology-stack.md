# adr-technology-stack: 全局架构设计与技术栈选型

## Metadata

- **ADR ID**: adr-technology-stack
- **Status**: accepted
- **Decider(s)**: 用户, opencode
- **Date**: 2026-09-14
- **Supersedes / Superseded By**: N/A

---

## 1. Context & Problem Statement

需要构建一个现代化、轻量、响应迅速的跨平台桌面待办事项应用（TodoList）。
核心目标平台为 Windows 10/11 (x64)，同时支持在 macOS (Apple Silicon arm64) 环境下进行本地开发、调试与无缝构建。

### 关键约束与需求：
1. **跨平台原生级体验**：Windows 上能无缝打包为独立自包含单文件（`win-x64` self-contained），且本地 macOS 环境可即时调试。
2. **多视窗交互模式**：包含主任务管理视窗（SplitView 大本营）和全局快捷唤醒的“随手记”极速捕捉浮窗（Quick Capture Mini Window）。
3. **数据安全性与并发一致性**：多窗口同时写入或系统异常退出时，数据必须绝对安全，杜绝文件损坏或截断清空。
4. **扩展性**：未来平滑支持系统级快捷键捕获、本地离线语音转写（Whisper.net）及 NLP 智能日期分词。

---

## 2. Decision Outcome

**Chosen Option**: 
- **基础运行环境**: .NET 8.0 LTS
- **UI 与呈现框架**: Avalonia UI 11.2+ (Fluent Theme, 支持 Mica / Acrylic 现代透明材质)
- **MVVM 框架**: CommunityToolkit.Mvvm 8.3+ (官方推荐，基于 Source Generator 的强类型响应式绑定与 WeakReferenceMessenger 事件解耦)
- **本地数据持久化**: SQLite 嵌入式存储 (采用轻量高效的 `sqlite-net-pcl` + `SQLitePCLRaw.bundle_green`)
- **多窗口进程模型**: 单进程多窗口（Single-Process, Multi-Window）+ 弱引用消息总线解耦
- **分发与打包机制**: 本地脚本 + GitHub Actions CI 矩阵构建自包含免安装包（Self-Contained Single File）

### Detailed Rationale

1. **为什么选择 Avalonia 11 而非 MAUI / WPF / Electron**:
   - WPF 仅限 Windows；MAUI 在桌面端（尤其是 macOS/Linux）体验和跨平台统一渲染一致性弱于 Avalonia。
   - Electron 内存与安装包体积过于庞大（150MB+），Avalonia 编译后独立包仅 30~40MB 且冷启动 < 100ms。
   - Avalonia 11 在 Windows 上原生支持 Mica/Acrylic 亚克力特效，在 macOS 上支持原生毛玻璃窗口，视觉高级。
2. **为什么选择 SQLite (`sqlite-net-pcl`) 而非单 JSON 文件**:
   - JSON 存储在任务数少时开销低，但在多窗口并发写入或突发断电时，全量覆写存在数据损坏清空（Truncate 截断）的巨大风险。
   - SQLite 原生具备 ACID 事务和 WAL（Write-Ahead Logging）日志机制，微秒级增量写入，彻底杜绝数据损坏。
   - `sqlite-net-pcl` 极轻量（单动态库几十 KB），零复杂配置，免除 EF Core 的笨重脚手架。
3. **为什么选择 `CommunityToolkit.Mvvm`**:
   - 利用 C# 源码生成器（Source Generators）自动生成 `[ObservableProperty]` 和 `[RelayCommand]`，大幅度精简样板代码。
   - 原生提供 `WeakReferenceMessenger`，使主窗口与随手记小窗在数据写入时无需持有对方引用，彻底防止多窗口内存泄漏。

---

## 3. Considered Alternatives

### Option A: 单 JSON 文件落盘 (System.Text.Json)
- **Pros**: 无需引入外部数据库依赖，文件可直接用文本编辑器查看和编辑。
- **Cons**: 无法安全应对主窗口与捕捉浮窗的并发写入；任何非正常关机可能破坏 JSON 完整性；历史记录累积后全量序列化带来性能损耗。已否决。

### Option B: Entity Framework Core + SQLite
- **Pros**: 功能极其完备，支持复杂的 LINQ 查询与自动迁移。
- **Cons**: 对轻量 TodoList 而言过于臃肿，冷启动损耗增加，反射与动态代码对 Native AOT 不友好。已否决。

---

## 4. Consequences & Impact

- **Positive Impact**:
  - 本地 macOS 与目标 Windows 拥有高度统一的 UI 渲染效果与行为逻辑。
  - 数据读写具备工业级安全性，支持未来数十万条待办记录依然极速响应。
  - 主窗口与随手记浮窗完全解耦，架构清晰。
- **Negative Impact & Trade-offs**:
  - 需针对各操作系统打包 SQLite 原生 C 运行库（由 `SQLitePCLRaw.bundle_green` 自动跨平台处理）。
- **Follow-up Actions**:
  - [x] 确立现代化 C# / XAML 编码与注释规范（见 `docs/rules/rule-code-standards.md`）。
  - [x] 建立项目核心 SPEC（`docs/specs/SPEC-001-mvvm-infrastructure.md`）。

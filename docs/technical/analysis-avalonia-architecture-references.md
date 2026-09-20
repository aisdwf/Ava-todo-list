# 分析：Avalonia 大型应用架构参考调研

## Metadata

- **Category**: 技术调研（与功能开发进度无关，长期保留供查阅）
- **Status**: 归档（调研结论，非执行依据）
- **Created Date**: 2026-09-17
- **触发原因**：`MainViewModel.cs` 已膨胀至 1183 行，同时承担任务 CRUD、项目管理、
  标签管理、到期日编辑、外观设置五个正交职责。用户判断根因是「没有好的参考」，
  要求调研市面上优秀的 Avalonia 架构或真实工程落地案例。
- **提炼出的强制规则**：本文档 §3 的拆分模式已提炼为强制规则，
  见 [`docs/rules/technical-rules.md`](../rules/technical-rules.md)。
  本文档保留调研原文与案例细节，规则卡不重复引用原文内容，只作规范化表述。

---

## 1. 调研方法与限制

- 通过 GitHub REST API（`api.github.com`）检索，因本机环境下 `github.com` 网页直连超时，
  改用 API 端点获取仓库结构与源码文件内容。
- 筛选口径：语言为 C#、明确使用 Avalonia、star 数或社区认可度较高、
  且能定位到源码目录结构（而非仅示例片段）。
- 本次为**技术调研**，不构成设计决策。是否采纳、何时采纳、采纳到什么程度，
  留待用户判断后另行写 SPEC。

---

## 2. 核心参考案例

### 2.1 SourceGit（`sourcegit-scm/sourcegit`）—— 最具参考价值

- **规模**：约 6000 star，MIT 协议，跨平台 Git GUI 客户端，技术栈高度对齐本项目：
  Avalonia 11 + `CommunityToolkit.Mvvm` 8.x + .NET（其 csproj 显示 `net10.0`，
  `CommunityToolkit.Mvvm` 8.4.2）。
- **目录结构**（`src/`）：
  ```
  src/
    ViewModels/     ← 一百余个独立 .cs 文件
    Views/
    Models/
    Commands/       ← 封装每条 git 命令的执行逻辑
    Converters/
    Native/         ← 平台相关代码
  ```
- **关键模式：一个操作 = 一个独立 ViewModel 类**。
  实测抽样文件名（均为 `src/ViewModels/` 下的独立顶层类，无嵌套在主 VM 内部）：
  `CreateBranch.cs`、`DeleteBranch.cs`、`DeleteMultipleBranches.cs`、`Checkout.cs`、
  `CheckoutAndFastForward.cs`、`CherryPick.cs`、`Clone.cs`、`CreateTag.cs`、
  `DeleteTag.cs`、`Discard.cs`、`Apply.cs`、`ApplyStash.cs`、`ClearStashes.cs`、
  `AddRemote.cs`、`DeleteRemote.cs`、`AddSubmodule.cs`、`DeleteSubmodule.cs` ……
  每个文件通常只有几十到几百行，只负责一个用户发起的操作（通常对应一个弹窗或一条菜单命令）。
- **分层职责**：
  - `Launcher.cs`（约 17KB）：应用级状态——多标签页（`LauncherPage`）、
    工作区切换、标签页开关与排序。**只管"页面容器"这一件事**，
    不掺入任何仓库内部操作的业务逻辑。
  - `Repository.cs`（约 64KB，单文件确实很大）：单个 Git 仓库标签页内的状态——
    分支树、提交历史、变更列表、当前 HEAD 等。这是**领域聚合根级别**的状态容器，
    但注意：所有「用户发起的具体操作」（建分支、删标签、cherry-pick……）
    并未写在这个文件里，而是各自在独立文件中实现，`Repository.cs` 只负责
    **持有状态 + 实例化并弹出对应的操作 ViewModel**。
  - `Preferences.cs`（约 26KB）：全局设置的单例（`Preferences.Instance`），
    自行负责 JSON 序列化落盘，不与 `Launcher`/`Repository` 混在一起。
- **对本项目的启示**：即使是成熟的大型开源项目，「一个聚合根状态容器较大」
  仍是现实（`Repository.cs` 64KB），**但不可接受的是把所有操作的实现细节
  也塞进这一个文件**。SourceGit 的做法是把"状态持有"和"操作执行"拆成两层：
  聚合根 VM 只留必要的可观察状态与派生属性，每个用户操作单独成类，
  通过聚合根的方法或消息机制触发。

### 2.2 PicView（`Ruben2776/PicView`）—— 分层参考

- **规模**：约 3500 star，跨平台图片查看器。
- **目录结构**（顶层 `src/`）：
  ```
  PicView.Core            ← 平台无关的核心逻辑
  PicView.Core.Linux / .MacOS / .WindowsNT   ← 平台特定的 Core 扩展
  PicView.Avalonia         ← Avalonia UI 层
  PicView.Avalonia.Linux / .MacOS / .Win32   ← 平台特定的 UI 层
  PicView.Benchmarks
  PicView.Tests
  ```
- **对本项目的启示**：本项目已有 `FlowTask.Core` / `FlowTask.Infrastructure` /
  `FlowTask.Desktop` 三层分离，这与 PicView 的 `Core` / `Avalonia` 分层理念一致，
  说明**当前的项目级分层是合理的**，问题不在跨项目分层，而在
  `FlowTask.Desktop` 内部 `ViewModels/` 一层缺少进一步的职责拆分。

### 2.3 Avalonia 官方文档立场

- 官方 [The MVVM Pattern](https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/)
  明确指出：MVVM 的价值在应用**从简单变复杂**之后才体现，官方建议两种路径之一——
  「先用 code-behind，复杂到难维护时再迁移到 MVVM」或「预期会变复杂，从一开始就用 MVVM」，
  但**文档本身未给出"大型 ViewModel 如何拆分"的具体规范**，
  这与本项目遇到的问题（缺少权威拆分范式）一致——**官方文档层面确实没有现成答案**，
  只能从社区真实工程实践中总结模式。
- 官方文档结构里有独立的 `Services` 概念页（`/docs/services/`），
  暗示官方鼓励把「非状态、非展示」的横切能力（剪贴板、文件对话框等）
  抽成独立 Service 而非塞进 ViewModel，这与本项目 `Class1.cs` 残留、
  尚未建立 Service 层的现状形成对照。

---

## 3. 可提炼的拆分模式（供后续设计参考，非立即执行）

结合 SourceGit 的实际结构，可归纳出三种和本项目现状直接相关的拆分方向：

1. **按「操作」拆分命令逻辑**（SourceGit 主模式）
   `MainViewModel` 里凡是 `[RelayCommand]` 标注、且内部逻辑超过几行判空与转发的方法，
   都是候选对象。例如项目的创建/重命名/改色/删除/归档，标签的创建/重命名/改色/删除，
   到期日编辑弹层的开关与提交——这些目前都是 `MainViewModel` 的私有方法，
   可参照 `CreateBranch.cs` / `DeleteTag.cs` 的模式，拆成
   `CreateProjectViewModel` / `RenameProjectViewModel` 等独立类，
   由 `MainViewModel` 持有并触发，而不是自己实现全部细节。

2. **按「子领域」拆分状态容器**（介于 SourceGit 的 `Repository.cs` 与
   进一步拆分之间的折中）
   把 `MainViewModel` 现有的"项目管理"（`_projects` / `ProjectChoices` / 相关方法）
   与"标签管理"（`_tags` / `NewTagChoices` / 相关方法）分别收拢进
   `ProjectListViewModel` / `TagListViewModel` 这类子 ViewModel，
   `MainViewModel` 通过属性持有它们，XAML 绑定路径相应加一层
   （如 `{Binding Projects.Items}`）。这比逐操作拆分改动面更大，
   但能从根本上让 `MainViewModel` 缩小到「任务流 + 视图筛选」本身。

3. **抽出与 UI 无关的横切能力为 Service**（官方文档倡导的方向）
   本项目里 `AppearanceCoordinator` 已经是这种模式的正确示范
   （静态协调器，不依赖 ViewModel 生命周期）。同理，颜色轮转逻辑
   （`PickNextProjectColor` / `PickNextTagColor` 及改色时的"找下一色"逻辑，
   当前在 `MainViewModel.cs:836-840` 与 `:1154-1158` 重复两次）
   适合收进 `AppearanceCoordinator`，而非留在 ViewModel 里。

> 以上三个方向不互斥，可组合使用。具体选哪个、拆到什么粒度、
> 是否需要引入导航/消息机制协调子 ViewModel 之间的通信，
> 应在用户确认要动手时另立 SPEC 讨论，本文档不代为决策。

---

## 4. 结论

- 市面上确实存在与本项目技术栈高度一致、且规模验证过的参考（SourceGit），
  可以作为「一个大 ViewModel 如何拆」的具体范式来源，不必自己摸索发明新范式。
- Avalonia 官方文档在这一点上没有给出规范性指导，说明该问题在社区中
  确实主要靠工程实践沉淀解决，而非存在一个「官方标准答案」被本项目遗漏。
- `MainViewModel` 当前 1183 行本身不是孤例（SourceGit 的 `Repository.cs` 64KB
  更大），**真正的风险不是行数，而是"状态持有"与"操作实现细节"未分离**。
  下一步若要动手重构，建议方向是 §3 的模式 1（按操作拆分命令逻辑），
  改动面最小、最贴近本项目当前 `[RelayCommand]` 密集分布的现状。

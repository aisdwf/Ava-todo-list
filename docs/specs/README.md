# 功能规格文档（SPEC）

本目录用于维护系统的功能规格文档与当前工作记忆。目录结构、命名格式、状态机与新鲜度阈值的
唯一权威定义见 [`docs/rules/docs-conventions.md`](../rules/docs-conventions.md)；
design vs spec 的类型边界见 [`docs/rules/rule-doc-boundary.md`](../rules/rule-doc-boundary.md)。
本文件只维护**当前索引**，机制细节不在此重复。

## 一、Area 子目录

| Area | 职责边界 |
| :--- | :--- |
| [`infrastructure/`](./infrastructure/) | MVVM 基础设施、SQLite 持久化骨架 |
| [`visual-theme/`](./visual-theme/) | 视觉语言、主题切换、外观个性化 |
| [`task-domain/`](./task-domain/) | 任务/项目/标签数据契约、时钟整改、归档语义 |
| [`main-window/`](./main-window/) | 主窗口布局、侧边栏、分类交互 |
| [`quick-capture/`](./quick-capture/) | 快捷键显隐小窗、捕捉补全 |
| [`docs-system/`](./docs-system/) | 文档体系自身的重构 |
| [`packaging/`](./packaging/) | 发布产物形态、CI 构建与分发流程 |

新增 area 前必须满足 `docs-conventions.md` 的三条判据（无现成归属 / 稳定域身份 / 一句话可描述边界）。

## 二、SPEC 七种合法状态

| 文件名后缀 | 正文 Status | 含义 |
| :--- | :--- | :--- |
| `[DRAFT]` | `draft` | 草稿/提议中，未正式开工 |
| `[IN-PROGRESS]` | `in-progress` | 执行中。**铁律：一旦开工不留在 draft** |
| `[DONE]` | `done` | 已完成，代码/测试/验证三位一体闭环 |
| `[DONE-REFACTORED]` | `done-refactored` | 已完成功能且后续经历架构重构治理 |
| `[SUPERSEDED]` | `superseded` | 已被新 SPEC 取代（必须注明新 SPEC 链接） |
| `[ARCHIVED]` | `archived` | 归档沉淀，指向研究/分析文档，不可执行 |
| `[OBSOLETE]` | `obsolete` | 作废废弃，方案未落地或不再适用 |

## 三、索引维护规则

本项目（.NET）无文档索引生成脚本，`docs/specs/README.md` 为**人工维护索引**。
凡涉及 SPEC 的新增、改名、area 移动或状态变化，**必须在同一改动中**更新本文件的
索引表与「新会话接手入口」。索引与物理文件不一致时视为违规（等同于 stale SPEC）。

机器检查仍由 `dotnet build` / `dotnet test` 承担（见 `docs/rules/rule-code-standards.md`），
命名与类型边界检查见 `docs/rules/rule-doc-boundary.md` §4。

## 四、⚠ 新会话接手入口

**当前阶段：`visual-theme/spec-unified-svg-icons` 已完成（描边图标 + 移除更换颜色，所有者已看预览包）。其余进行中的 SPEC 为：
`visual-theme/spec-settings-master-detail-and-theme-presets`（代码已提交，人工验证按例外推迟）；
三份 quick-capture 相关 SPEC（`spec-quick-window-hotkey-capture` /
`task-domain/spec-task-complete-before-archive` / `spec-quick-window-single-project-list`，
均源自 `feature/quick-window-standalone` 分支，机器验证 195 测试已通过，
统一等待用户在 Windows 端做一次性人工验收，见各 SPEC §4 人工验证表）。
上一完成项为 `packaging/spec-windows-single-file-release`（Windows win-x64 单文件发布
+ GitHub Actions Release 流水线；`v0.1.0` 已发布，zip + 裸 exe 双资产；人工验证通过）。**
跨 SPEC 未实现项见下方「待办事项索引」。

### 接手顺序

1. 读 `AI_CONSTITUTION.md`（最高约束）
2. 读 [`AGENTS.md`](../../AGENTS.md) 的 Gate 1-5
3. 读 [`docs/rules/workflow-methodology.md`](../rules/workflow-methodology.md)：任务分类与 Step 0-6
4. 读 [`docs/rules/docs-conventions.md`](../rules/docs-conventions.md)：SPEC 目录/命名/状态权威定义
5. 读 [rule-spec-review-gate](../rules/rule-spec-review-gate.md)：**SPEC 必须经用户确认方可开工**的事故记录
6. 读 [rule-no-invented-user-behavior](../rules/rule-no-invented-user-behavior.md)：**交互设计严禁凭推理产出用户行为假设**
7. 读 [rule-doc-boundary](../rules/rule-doc-boundary.md)：文档类型边界与命名规范
8. 读三份 design 建立设计约束认知：
   [视觉语言](../design/design-visual-language.md) ·
   [领域契约](../design/design-domain-contract.md) ·
   [交互原则](../design/design-interaction-principles.md)
9. 从下方「待办事项索引」与各 SPEC §6 Deferred Items 接手下一项；
   新功能须先写 SPEC、经用户审核后再开工

### 背景（必读）

`task-domain/spec-task-contract-and-clock` 与 `main-window/spec-classification-ui` 均**跳过审核直接开工**，
交付后用户实测发现主窗口大量反直觉交互：顶部添加栏等同随手记、
项目上下文被丢弃、标签与 deadline 依赖手工输入、编辑入口不可发现、
侧边栏跳转紊乱。

**163 个单元测试全部通过，却一个都没拦住** ——
测试只能证明代码符合设计，而错的是设计本身。
唯一能拦住这类缺陷的关卡就是被跳过的那道人工审核。

### 职责边界（用户明确要求）

| 角色 | 职责 |
| :--- | :--- |
| AI | 写 SPEC、编码、基础测试（构建 + 单测） |
| 用户 | **审核 SPEC**、**执行功能验证**、**指示提交时机** |

严禁以「构建通过 + 测试全绿」宣称功能可用；
严禁以「进程存活」冒充功能验证。

### 常用命令（macOS）

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
dotnet build FlowTask.sln -v q --nologo     # 基线：0 警告 0 错误
dotnet test  FlowTask.sln --nologo -v q     # 基线：163 通过
./run.sh                                     # 启动（不得当作功能验证）
```

---

## 五、SPEC 索引

| SPEC | Area | 主题 | 状态 | 备注 |
| :--- | :--- | :--- | :--- | :--- |
| [spec-mvvm-infrastructure](./infrastructure/spec-mvvm-infrastructure[DONE].md) | infrastructure | MVVM 基础设施与 SQLite 持久化 | `done` | — |
| [spec-fluent-ui](./visual-theme/spec-fluent-ui[SUPERSEDED].md) | visual-theme | Windows 11 Fluent 2 界面 | `superseded` | 已由 spec-editorial-and-ripple-theme 取代，含重定向 |
| [spec-editorial-and-ripple-theme](./visual-theme/spec-editorial-and-ripple-theme[DONE].md) | visual-theme | Editorial 排版、水波纹昼夜切换、外观个性化 | `done` | 含 3 项显式推迟事项，见其 §6 |
| [spec-task-contract-and-clock](./task-domain/spec-task-contract-and-clock[DONE].md) | task-domain | 任务数据契约扩展、IClock 整改与编辑闭环 | `done` | DESIGN 三段实施的第 1 段；产出无 UI 入口，见其 §5 |
| [spec-tag-entity](./task-domain/spec-tag-entity[DONE].md) | task-domain | 标签实体化与设置页管理 | `done` | 直接切换实体模型，不兼容旧字符串标签；含 TODO(tag-filter) |
| [spec-due-date-calendar](./task-domain/spec-due-date-calendar[DONE].md) | task-domain | 到期日三来源录入、日历、偏移设置；主窗移除今日聚焦 | `done` | 基础初版已验收；创建不自动写；快捷启用/清除；AppSettings N∈[1,30]；行上点击改期；日历按需展开；主窗删今日聚焦；含 TODO(quick-capture-today) |
| [spec-task-complete-before-archive](./task-domain/spec-task-complete-before-archive[IN-PROGRESS].md) | task-domain | 完成≠归档；手动归档；勾选容错 | `in-progress` | **第 2 份**；归档保留项目来源；D3 采用全局「归档全部已完成」入口，不做逐项目/多选 |
| [spec-classification-ui](./main-window/spec-classification-ui[DONE].md) | main-window | 主窗口分类交互与校验值对象 | `done` | **其交互设计已被用户实测证伪**，由 design-interaction-principles 重做 |
| [spec-sidebar-selection-consolidation](./main-window/spec-sidebar-selection-consolidation[DONE].md) | main-window | 侧边栏选中机制收敛（结构整改） | `done` | 机器验证（167 测试通过）与人工验证均已完成，行为零变化 |
| [spec-viewmodel-command-decomposition](./main-window/spec-viewmodel-command-decomposition[DONE].md) | main-window | MainViewModel TR-1 命令拆分（操作类抽取） | `done` | 人工验证通过；薄命令保留 XAML 绑定；`MainViewModel` 1183→912 行 |
| [spec-quick-window-hotkey-capture](./quick-capture/spec-quick-window-hotkey-capture[IN-PROGRESS].md) | quick-capture | 近似全局热键显隐 + `@项目` `#标签` 捕捉补全 | `in-progress` | **第 1 份**；未知不创建；无 @ → Default |
| [spec-quick-window-single-project-list](./quick-capture/spec-quick-window-single-project-list[IN-PROGRESS].md) | quick-capture | 小窗单项目列表 + 勾选 | `in-progress` | **第 3 份**；依赖前两份（均已机器验证通过）；记忆上次项目；D1 用下拉切换、D2 未完成在上已完成置底 |
| [spec-doc-restructure](./docs-system/spec-doc-restructure[DONE].md) | docs-system | 文档体系重构：类型边界归位 + 全库编号清理 | `done` | 拆分 5 份职责混杂的 design，清理 434 处编号引用 |
| [spec-settings-master-detail-and-theme-presets](./visual-theme/spec-settings-master-detail-and-theme-presets[IN-PROGRESS].md) | visual-theme | 设置页改为主从式独立页面 + 可扩展命名主题预设 | `in-progress` | 命名预设色值已从 dogapi.cc / linkapi.ai 采集并写入；人工核对视觉差异仍待做 |
| [spec-unified-svg-icons](./visual-theme/spec-unified-svg-icons[DONE].md) | visual-theme | 操作图标统一为描边 SVG；移除更换颜色入口 | `done` | 保留项目 `ColorHex` 的色点与色条；图表与品牌图标不在范围；所有者已看预览包 |
| [spec-windows-single-file-release](./packaging/spec-windows-single-file-release[DONE].md) | packaging | Windows 单文件发布 + GitHub Actions 自动构建/发布 Release | `done` | 新建 `packaging` area；44 文件→1 exe；`v0.1.0` 已正式发布，zip + 裸 exe 双资产；人工验证通过 |
| [spec-create-task-inherits-selected-project](./main-window/spec-create-task-inherits-selected-project[IN-PROGRESS].md) | main-window | 新建任务继承当前选中项目 | `in-progress` | 用户已确认开工 |
| [spec-remove-tag-feature](./task-domain/spec-remove-tag-feature[IN-PROGRESS].md) | task-domain | 完全移除标签功能 | `in-progress` | 用户已确认开工；推翻 `spec-tag-entity` 既有裁决，联动修订 `spec-quick-window-hotkey-capture[IN-PROGRESS]` 与 `REQUIREMENTS.md` |

### 待办事项索引（跨 SPEC 汇总）

以下事项已在关闭的 SPEC 中显式登记，**不属于隐形债务**（Article 4）。
新会话接手时应从此处进入：

| 事项 | 期限 | 前置条件 | 来源 |
| :--- | :--- | :--- | :--- |
| ~~「今日聚焦」视图恒为空；`DateTime.Today` 违反 Article 9~~ | ~~2026-09-21~~ | **已核销** —— 由 spec-task-contract-and-clock 完成整改：引入 `IClock`、修正时区区间、补齐 `DueDate` 写入路径 | [spec-task-contract-and-clock](./task-domain/spec-task-contract-and-clock[DONE].md) |
| 移除 `Class1.cs` 模板残留空类（Core / Infrastructure） | 2026-09-21 | 无 | [spec-editorial-and-ripple-theme 的推迟事项](./visual-theme/spec-editorial-and-ripple-theme[DONE].md) |
| 外观偏好持久化（主题 / 强调色 / 材质，重启后回退默认） | 2026-09-28 | 需决策配置存储位置与格式 | [spec-editorial-and-ripple-theme 的推迟事项](./visual-theme/spec-editorial-and-ripple-theme[DONE].md) |
| `Description` 仍为死字段（无 UI 读写路径） | 2026-10-05 | 无（编辑态已有 5 字段，加备注需多行框、显著增高面板） | [spec-classification-ui 的推迟事项](./main-window/spec-classification-ui[DONE].md) |
| 软删除任务无恢复入口，`PermanentDeleteAsync` 无调用方 | 2026-10-05 | 需先决定回收站是否作为需求纳入 REQUIREMENTS | [spec-task-contract-and-clock 的推迟事项](./task-domain/spec-task-contract-and-clock[DONE].md) |
| **项目归档命令已实现但无 UI 入口**（只能删除，不能归档） | 2026-10-12 | 需决策承载方式（右键菜单 / 项目详情弹层） | [spec-classification-ui 的推迟事项](./main-window/spec-classification-ui[DONE].md) |
| ~~Anthropic/暗夜/海风等命名主题预设的具体色值未定义~~ | 已采集 | dogapi.cc 与 linkapi.ai 样式表色值已写入 `ThemePresets`；未收入「超大字体简易」；字体/圆角/布局未改 | [spec-settings-master-detail-and-theme-presets](./visual-theme/spec-settings-master-detail-and-theme-presets[IN-PROGRESS].md) |
| **`ProjectId` 实际仍存在 `null`，与 design-domain-contract §2.3「不可为 null，须挂 Default」的契约不一致**（创建路径、删除项目后的回退路径均写 `null`） | 待定 | 需先确认是否要把「全部任务」视图与 Default 项目的信息架构合并（用户本轮明确表示不想现在做这个抉择，见 spec-create-task-inherits-selected-project §6） | [spec-create-task-inherits-selected-project 的推迟事项](./main-window/spec-create-task-inherits-selected-project[IN-PROGRESS].md) |

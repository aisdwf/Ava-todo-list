# Project Documentation System (项目文档系统)

<!-- 
NEVER EDIT DIRECTORY COUNTS OR FILE LISTS CASUALLY.
This document serves as the master navigation map for developers and AI agents.
-->

本目录用于集中维护项目开发全生命周期的规则、决策、规格、设计、分析、排查指南与文档状态。

---

## 1. 顶层目录索引与职责 (Directory Master Index)

| 目录 | 职责与定位 | 维护模式 |
| :--- | :--- | :--- |
| [`ai-workflow/`](./ai-workflow/) | AI 驱动工作流的深层方法论（人类可读的长文，解释「为什么这样设计」）。**已 gitignore，不随仓库分发**；机器权威规则见 `rules/` | 核心理论基座（私有） |
| [`requirements/`](./requirements/) | **需求层单一真源**：产品定位、目标用户、核心需求、验收标准与非目标 | 需求基线，设计的上游 |
| [`rules/`](./rules/) | 机器权威规则卡（英文）+ 事故驱动的防御型规则（中文） | 事故驱动递增 |
| [`specs/`](./specs/) | 功能规格与任务工作记忆（Working Memory），支持任务断点无损恢复 | 任务级动态流转 |
| [`templates/`](./templates/) | 标准工程化脚手架模板库（SPEC、规则、ADR、排查手册等骨架） | 脚手架沉淀 |
| [`adr/`](./adr/) | 架构决策记录（Architecture Decision Records），记录重大架构演进背景与取舍 | 架构决策留痕 |
| [`trobleshooting/`](./trobleshooting/) | 线上/线下重大故障与调试排查手册（Runbook 与 5 Whys 根因分析） | 事故排查手册 |
| [`analysis/`](./analysis/) | 技术调研、可行性对比、性能压测与选型分析报告 | 调研结论归档 |
| [`design/`](./design/) | **长期有效的设计约束**：视觉语言、领域契约、交互原则。仅 active/superseded 两态 | 约束层，长期有效 |
| [`refenence/`](./refenence/) | 外部依赖协议、平台 SDK 参考手册与编码规范参考 | 参考与外部接口 |
| [`archived/`](./archived/) | 历史归档与废弃文档（指向研究/分析，不可直接作为执行依据；必须包含重定向） | 软归档保留 |
| [`pending-delete/`](./pending-delete/) | 待物理删除的临时文档缓冲区（软删除暂存，防止误删） | 缓冲清理区 |

> **注**：`backend/` 为可选目录。当前项目为前端工程，暂不启用；后续若涉及全栈联调需要独立维护后端需求时再行开启。

---

## 2. 目录详细文件清单与作用简述 (Detailed File Tables)

### 2.1 `ai-workflow/` (AI 编码工作流深层方法论，**已 gitignore**)

> 本目录**不随仓库分发**（见 `.gitignore`）。它是人类可读的长文方法论，
> 解释机制背后的「为什么」。AI 会话据以行动的**权威机制定义**在 `docs/rules/*.md`
> （见 §2.2），不依赖本目录也能完整执行工作流。

| 文件名 | 作用简述 |
| :--- | :--- |
| `README.md` | 工作流总览与章节导读导航 |
| `01-理念和宪法.md` | AI 协作工程哲学、三大支柱与宪法公理 |
| `02-SPEC驱动工作流.md` | SPEC 定位、7 种状态机、复杂任务 6 步 SOP 及防腐化规则 |
| `03-规则体系.md` | 事故博物馆武装化、三层规则分类、防呆清单与保鲜机制 |
| `04-工程化门禁.md` | 三道 Git 防线、软硬分层拦截哲学、核心脚本设计原理与落地节奏 |
| `05-AI协作实操.md` | 权限模式管控、改动前 30 秒自查防呆、Subagent 纪律与提交规范 |
| `06-落地指南.md` | 体系渐进生长原则、落地坑位与演进时间线 |

### 2.1b `rules/` 中的机器权威规则卡（英文，取代 ai-workflow 的机制正文）

| 文件名 | 作用简述 |
| :--- | :--- |
| [`workflow-methodology.md`](./rules/workflow-methodology.md) | 任务分类、Complex Step 0-6、执行节奏硬规则、错误处理对照表 |
| [`docs-conventions.md`](./rules/docs-conventions.md) | SPEC 目录（area 子目录）、命名、状态机、新鲜度阈值的唯一权威 |
| [`commit-conventions.md`](./rules/commit-conventions.md) | `Why:`/`What:` 强制结构、归因分类、`TEMP_PATCH` 约定 |
| [`project-rules.md`](./rules/project-rules.md) | FlowTask 项目专属业务/技术/架构规则（宪法 Article 7） |

### 2.1b `requirements/` (需求基线)

| 文件名 | 作用简述 | 核心内容 |
| :--- | :--- | :--- |
| [`REQUIREMENTS.md`](./requirements/REQUIREMENTS.md) | 产品需求单一真源 | 项目性质、目标用户、随手记小窗立足点、核心需求 R-1/R-2/R-3、验收标准、非目标 |

> **为什么新增此目录**：原文档体系有 `design/`（怎么做）却无需求层（要什么），
> 导致 design-domain-contract 在需求缺位下撰写，以项目自述文件的宣传语作为设计依据，
> 构成循环论证而被阻塞。需求必须先于设计存在。

### 2.2 `templates/` (标准工程模板库)

| 文件名 | 作用简述 | 适用场景 |
| :--- | :--- | :--- |
| [`README.md`](./templates/README.md) | 模板目录索引与使用指南 | 模板使用指引 |
| [`spec-template.md`](./templates/spec-template.md) | 任务工作记忆标准模板（含 7 状态、分阶段计划、Subagent 追踪表） | 复杂开发任务立项 |
| [`rule-template.md`](./templates/rule-template.md) | 武装化事故规则模板（含 Why、祈使句 Mandates、反例/正例、机器命令） | 故障复盘转为自动化规则 |
| [`adr-template.md`](./templates/adr-template.md) | 架构决策记录模板（含决策背景、多方案优缺点对比、后续影响） | 技术选型与重大架构重构 |
| [`troubleshooting-template.md`](./templates/troubleshooting-template.md) | 故障排查与根因分析模板（含症状、现场日志、5 Whys、防范措施） | 疑难 Bug 与线上事故分析 |

### 2.3 `rules/` (开发规则与防呆)

| 文件名 | 作用简述 | 严重级 |
| :--- | :--- | :--- |
| [`README.md`](./rules/README.md) | 规则库索引、**改动前 30 秒防呆清单** | — |
| `workflow-methodology.md` | 任务分类、Complex Step 0-6、执行节奏硬规则、错误处理对照表 | BLOCK |
| `docs-conventions.md` | SPEC 目录（area 子目录）/命名/状态机/新鲜度阈值的唯一权威 | BLOCK |
| `commit-conventions.md` | `Why:`/`What:` 强制结构、归因分类、`TEMP_PATCH` | BLOCK |
| `project-rules.md` | FlowTask 项目专属业务/技术/架构规则 | — |
| `rule-code-standards.md` | C# 12 / Avalonia 11 编码与注释规范 | BLOCK |
| `rule-spec-review-gate.md` | SPEC 必须经用户审核方可开工；严禁预填未发生的事实（事故记录） | BLOCK |
| `rule-no-invented-user-behavior.md` | 交互设计严禁凭推理产出用户行为假设（事故记录） | BLOCK |
| `rule-doc-boundary.md` | 文档类型边界（design vs spec）与命名规范（事故记录） | BLOCK |

### 2.4 `specs/` (功能规格与工作记忆，按 area 子目录组织)

| 文件名 | Area | 作用简述 | 状态 |
| :--- | :--- | :--- | :--- |
| [`README.md`](./specs/README.md) | — | SPEC 管理规范、7 种状态、**新会话接手入口**、索引与跨 SPEC 待办 | — |
| `spec-mvvm-infrastructure[DONE].md` | infrastructure | MVVM 基础设施与 SQLite 持久化 | `done` |
| `spec-fluent-ui[SUPERSEDED].md` | visual-theme | Windows 11 Fluent 2 界面 | `superseded` |
| `spec-editorial-and-ripple-theme[DONE].md` | visual-theme | Editorial 排版、水波纹昼夜切换、外观个性化 | `done` |
| `spec-task-contract-and-clock[DONE].md` | task-domain | 任务数据契约扩展、IClock 时区整改、编辑闭环 | `done` |
| `spec-tag-entity[DONE].md` | task-domain | 标签实体化与设置页管理（含数据迁移） | `done` |
| `spec-due-date-calendar[DONE].md` | task-domain | 到期日三来源录入、日历、偏移设置 | `done` |
| `spec-task-complete-before-archive[DRAFT].md` | task-domain | 完成≠归档；手动归档；勾选容错 | `draft` |
| `spec-classification-ui[DONE].md` | main-window | 项目侧边栏、任务行分类呈现、校验值对象 | `done`（交互设计已被实测证伪） |
| `spec-sidebar-selection-consolidation[DONE].md` | main-window | 侧边栏选中机制收敛（纯结构整改） | `done` |
| `spec-quick-window-hotkey-capture[IN-PROGRESS].md` | quick-capture | 全局热键显隐 + `@项目` `#标签` 捕捉补全 | `in-progress` |
| `spec-quick-window-single-project-list[DRAFT].md` | quick-capture | 小窗单项目列表 + 勾选 | `draft` |
| `spec-doc-restructure[DONE].md` | docs-system | 文档体系重构：类型边界归位 + 编号清理 | `done` |

### 2.5 `adr/` (架构决策记录)

| 文件名 | 作用简述 | 核心内容 |
| :--- | :--- | :--- |
| [`README.md`](./adr/README.md) | ADR 目录职责说明与沉淀指引 | 架构决策的生命周期与追溯 |
| `adr-technology-stack.md` | 全局架构与技术栈选型 | .NET 8 + Avalonia 11 + SQLite + CommunityToolkit.Mvvm |

### 2.6 `trobleshooting/` (调试排查指南)

| 文件名 | 作用简述 | 核心内容 |
| :--- | :--- | :--- |
| [`README.md`](./trobleshooting/README.md) | 调试与问题排查目录职责说明 | 沉淀排查路径与应急经验 |

### 2.7 `analysis/` (分析调研报告)

| 文件名 | 作用简述 | 核心内容 |
| :--- | :--- | :--- |
| [`README.md`](./analysis/README.md) | 技术分析与方案调研目录指引 | 方案调研与可行性报告归档 |

### 2.8 `design/` (设计约束)

| 文件名 | 作用简述 | 状态 |
| :--- | :--- | :--- |
| [`README.md`](./design/README.md) | 设计目录索引与类型边界说明 | — |
| `design-visual-language.md` | 视觉语言：Editorial 基调、色彩令牌、形状间距、材质、动效 | `active` |
| `design-domain-contract.md` | 领域契约：字段语义、时间语义（UTC vs 日历日）、校验规则、存储约束 | `active` |
| `design-interaction-principles.md` | 交互原则：零手动输入、常驻配置、上下文继承、就地编辑、双窗口分工 | `active` |

> **类型边界**（[rule-doc-boundary](./rules/rule-doc-boundary.md)）：
> design 只含**实施前后都不变**的约束，严禁复选框、分阶段计划、验证记录。
> 「做到哪了」一律查 `specs/`。

### 2.9 `refenence/` (规范参考)

| 文件名 | 作用简述 | 核心内容 |
| :--- | :--- | :--- |
| [`README.md`](./refenence/README.md) | 外部接口与协议参考手册指引 | 第三方规范与协议对齐 |

### 2.10 `archived/` (历史归档)

| 文件名 | 作用简述 | 核心内容 |
| :--- | :--- | :--- |
| [`README.md`](./archived/README.md) | 归档文档目录规范（不可执行属性与重定向要求） | 沉淀历史背景，防止死链 |
| `design-visual-ux-overhaul-superseded.md` | 原 DESIGN-0001，视觉部分已并入 design-visual-language | 含重定向 |
| `design-bold-editorial-superseded.md` | 原 DESIGN-0002，已并入 design-visual-language | 含重定向 |
| `design-task-domain-superseded.md` | 原 DESIGN-0003，需求缺位下撰写（反面案例） | 含重定向 |
| `design-task-classification-superseded.md` | 原 DESIGN-0004，交互决策被实测证伪（反面案例） | 含重定向 |
| `design-main-window-rework-superseded.md` | 原 DESIGN-0005，违反类型边界已拆分 | 含重定向 |

### 2.11 `pending-delete/` (待删除缓冲区)

| 文件名 | 作用简述 | 核心内容 |
| :--- | :--- | :--- |
| [`README.md`](./pending-delete/README.md) | 软删除缓冲机制说明与定时清理规范 | 物理清理前缓冲，防止误删不可逆 |

---

## 3. 文档体系文件数量统计 (Documentation Metrics)

<!--
校验命令（新增/删除文档后必须重跑并同步下表）：
  find docs -name '*.md' | wc -l
本表此前长期停留在 22 篇，未随 SPEC / DESIGN / RULE / ADR 的新增而更新。
计数与实际不符时，必须核对实际文件后修正本表，而非调整数字掩盖差异（Article 10）。
-->

- **顶层受控目录数**：`12` 个
- **Markdown 文档总数**：`52` 篇（截至 2026-09-17，ai-workflow 深度接入后重计）

| 目录 | 篇数 | 构成 |
| :--- | :--- | :--- |
| `specs/` | 13 | README + 12 份 SPEC（6 个 area 子目录） |
| `rules/` | 9 | README + 8 条规则（4 张机器权威规则卡 + 4 条事故记录） |
| `ai-workflow/` | 7 | 总览 + 6 篇体系文章（已 gitignore，不计入版本库） |
| `archived/` | 6 | README + 5 份已归档 design（均含重定向） |
| `templates/` | 5 | README + 4 个标准工程模板 |
| `design/` | 4 | README + 3 份设计约束（视觉 / 契约 / 交互） |
| `adr/` | 2 | README + 技术栈选型 |
| `requirements/` | 1 | REQUIREMENTS（需求基线，无 README） |
| `trobleshooting/` | 1 | README（尚无故障手册） |
| `analysis/` | 1 | README（尚无调研报告） |
| `refenence/` | 1 | README（尚无外部参考） |
| `pending-delete/` | 1 | README（缓冲区为空） |
| `docs/` 根索引 | 1 | 本文件 |

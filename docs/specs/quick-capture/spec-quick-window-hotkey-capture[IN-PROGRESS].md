# spec-quick-window-hotkey-capture: 快捷键显隐 + 小窗捕捉（@项目 #标签补全）

## Metadata

- **ID**: spec-quick-window-hotkey-capture
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf
- **Created Date**: 2026-09-16
- **Last Updated**: 2026-09-16

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-16）按 1→2→3 顺序开工。本 SPEC 为第 1 份，状态 `in-progress`。**

**上游依据**：
- 需求：[REQUIREMENTS](../../requirements/REQUIREMENTS.md) R-1.3 / R-1.4 / R-1.5 / R-1.6（R-1.5、R-1.6 为本轮增补，见 §1）
- 设计：[design-interaction-principles](../../design/design-interaction-principles.md) §6 / §8（§8「排除项目/标签配置」被本轮用户原话部分推翻，见 §1.2）
- 领域：[design-domain-contract](../../design/design-domain-contract.md) §6（标题保留 `#`/`@`；**前缀语义以本 SPEC 用户裁决为准**：`@项目` `#标签`）

**本轮用户原话（2026-09-16）**：

> 「将目前的随手记进化为通过快捷键显示隐藏的快捷小窗，支持目前的随手记功能（带有@ #的快捷标签项目功能，最好带有补全）。」
>
> 「我目前不知道是否由于 macos 系统前台应用的特性，预览不在前台的情况下使用快捷键是没有反应的。至少在 windows 我希望可以做到近似全局的效果。」
>
> 裁决：`@项目` `#标签`（非早期预留的 `#项目` `@标签`）。

**建议开工顺序中的第 1 份**（三份拆法已确认）。依赖：无。被依赖：`spec-quick-window-single-project-list`。

---

## 0. 新会话接手须知

1. 现状：`QuickCaptureWindow` 620×150 纯输入胶囊；`MainWindow` 在**自身 KeyDown**上监听 Alt/Meta+Space，**仅主窗前台时有效**；保存走 `TaskItemFactory.Create` + 标题全文，**不解析** `@`/`#`。
2. 本 SPEC **只做**：近似全局显隐 + 输入解析/`@` `#` 补全 + 落库归属。**不做**列表与勾选（下一份 SPEC）。
3. macOS 全局热键若受系统限制，必须如实记录能力边界，不得假装「已全局」。

---

## 1. Why

### 1.1 现状缺陷

| 现状 | 问题 |
| :--- | :--- |
| 热键挂在 `MainWindow.OnWindowKeyDown` | 主窗非前台时无响应；与 R-1.4「不打开主窗口」冲突 |
| 小窗仅按钮/`OpenQuickCapture` 唤起 | 打断成本高（REQUIREMENTS §2） |
| 输入无 `@`/`#` | 用户明确要求小窗捕捉带项目/标签快捷语法 |

### 1.2 对既有设计的推翻（须同步改 design / REQUIREMENTS）

| 既有表述 | 本轮裁决 |
| :--- | :--- |
| design §8「小窗不引入项目/标签/日期配置」 | **部分推翻**：允许输入行内 `@项目` `#标签`（零摩擦语法），仍不引入独立配置面板/日期 |
| domain 预留 `#项目` `@标签` | **推翻前缀**：用户裁决为 `@项目` `#标签` |
| 「撤销/重做」非目标 | **不冲突**：本 SPEC 不引入命令栈 |

### 1.3 Attribution

`Design Incomplete` / `Code Incomplete` —— R-1 热键与语法未落地；热键绑定层级选错导致非前台失效。

---

## 2. What

### 2.1 需求增补（须先写入 REQUIREMENTS，再实现）

| ID | 需求 | 说明 |
| :--- | :--- | :--- |
| R-1.5 | **近似全局**快捷键显示/隐藏小窗 | **Windows 为验收重点**（进程在、无需主窗前台）。macOS：尽力而为，若系统拦截则文档化边界 |
| R-1.6 | 小窗输入支持 `@项目` `#标签`，并提供补全 | 解析后写入 `ProjectId` / `TaskTags`；标题去掉 token；未知名 **[待裁决]** 见 §2.5 |

### 2.2 热键行为

- 默认手势：**Windows** `Alt+Space`；**macOS** `Meta+Space`（与现注释一致）。若与系统冲突 → **[待裁决]** 可改默认或可配置（本 SPEC 默认先固定，冲突时登记 Deferred）。
- Toggle：隐藏↔显示；显示时聚焦输入框（复用现有 `Hide` 复用实例，禁止每次 `Close` 重建）。
- 进程未启动：不要求（非开机常驻服务；R-3 交付范围内「启动后」即可）。

### 2.3 输入语法

- `@项目名`：解析为项目；多候选时补全列表选择。
- `#标签名`：解析为标签；可多次；补全来自标签实体表。
- 其余文本为 `Title`（经 `TaskTitle.Normalize`，内部 `#`/`@` 仅当 token 边界匹配时剥离）。
- **[推断]** token 以空白分隔；未闭合的 trailing `@`/`#` 触发补全弹层。
- **[推断]** 优先级 P1/P2/P3 分段控件本轮保留（现有能力）。

### 2.4 落库

- 与主窗共用创建入口（`TaskItemFactory` / 仓储漏斗），禁止小窗另写一套不变量。
- 项目：匹配已有项目名（`ProjectName.Normalize`）。**无 `@` 或未匹配 → 归属 Default 项目**（R-2.6）。未知 `@` **不创建**，片段留在标题。
- 标签：匹配已有标签；未知 `#` **不创建**，片段留在标题（R-1.8）。

### 2.5 已裁决（2026-09-16）

| # | 议题 | 裁决 |
| :--- | :--- | :--- |
| D1 | 未知 `@项目` | **不创建**；当普通标题字符（B） |
| D2 | 未知 `#标签` | **不创建**；当普通标题字符（B） |
| D3 | 热键与系统冲突 | 本版本固定 Alt+Space（Win）/ Meta+Space（Mac）；冲突则 Deferred 可配置 |
| D4 | 无项目 token | 落入 **Default** 项目（废除未归属 null） |

### 2.6 非本 SPEC

- 小窗任务列表、勾选、单项目切换 → `spec-quick-window-single-project-list`
- 完成≠归档 → `spec-task-complete-before-archive`
- 主窗创建栏 `@`/`#` 语法 → 显式推迟

---

## 3. How（实施计划，确认前不执行）

1. REQUIREMENTS 增补 R-1.5 / R-1.6；修订 design §8 与 domain `@`/`#` 前缀说明。
2. 引入平台热键抽象（Windows `RegisterHotKey` P/Invoke 优先；macOS 单独实现或明确降级）。
3. 应用级注册/注销热键；从 `MainWindow` 局部 KeyDown 升为进程级（保留窗内快捷作回退可选）。
4. 实现纯函数解析器 + 单测（标题、项目、标签、多标签、未知 token 按 D1/D2）。
5. `QuickCaptureViewModel`：注入项目/标签仓储；补全 UI；保存时写关联。
6. `dotnet build` / `dotnet test`；人工：Win 非前台 toggle；输入补全与落库。

---

## 4. 验证

### 机器（AI 已跑；你可复跑）

```bash
./run.sh   # 或：dotnet run --project src/FlowTask.Desktop/FlowTask.Desktop.csproj
# 门禁（本轮已绿时可跳过复跑）：
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
dotnet build FlowTask.sln -v q --nologo
dotnet test  FlowTask.sln --nologo -v q
```

- [x] `dotnet build` 零警告零错误（本轮 AI 跑过）
- [x] `dotnet test` 全绿（本轮 AI 跑过，184）
- [x] 解析器单测覆盖 §2.3 用例

### 人工（你执行；AI 不得代勾）

**前置**：侧边栏至少有一个自建项目 + 一个标签（设置里建好）。系统应已有 **Default** 项目。

**启动**：项目根目录执行 `./run.sh`，等主窗出现。

| # | 操作 | 应看到的现象 | 通过？ |
| :--- | :--- | :--- | :--- |
| H1 | 主窗**在前台**时按 `Alt+Space`（macOS 可试 `Cmd+Space`；若被 Spotlight 抢走，改用主窗内同组合或点「随手记」按钮） | 小窗出现；输入框有焦点，可直接打字 | [ ] |
| H2 | 小窗打开时再按同一热键，或按 `Esc` | 小窗隐藏（进程仍在，不是退出应用） | [ ] |
| H3 | **Windows**：点一下别的应用（浏览器等）让 FlowTask 主窗到后台，再按 `Alt+Space`（若被系统占用则试 `Win+Alt+Space`） | 小窗仍能弹出。若完全无反应，在下方记「全局失败，仅前台可用」 | [ ] |
| H4 | **macOS**：主窗到后台后再按热键 | 记下真实结果：□ 全局可用 / □ 仅前台 / □ 与系统冲突。不要求本轮必须全局 | [ ] |
| H5 | 小窗输入：`买菜` → `Enter` | 主窗 **Default**（或等价默认项目）下出现标题为「买菜」的任务；无项目/标签 | [ ] |
| H6 | 输入：`修登录 @你的项目名`（打 `@` 后应出补全；↑↓ 选、`Tab`/`Enter` 接受）→ 保存 | 任务标题为「修登录」；归属为该项目（不是 Default） | [ ] |
| H7 | 输入：`读文档 #你的标签`（`#` 补全同上）→ 保存 | 标题「读文档」；带该标签；项目为 Default（未写 `@`） | [ ] |
| H8 | 输入：`灵感 @不存在的项目 #不存在的标签` → 保存 | **没有**新建项目/标签；标题里仍带那段未知 `@`/`#` 文本；任务落在 Default | [ ] |
| H9 | Esc 关小窗 → 再热键打开 | 输入框空、已聚焦，可直接输入 | [ ] |

**本轮失败时请写明**：哪一格、实际看到什么、Win 还是 Mac。通过后回复「SPEC-1 人工验收通过」再开 SPEC-2。

---

## 5. Deferred Items

| 事项 | 期限 | 触发条件 |
| :--- | :--- | :--- |
| 热键用户可配置 | 2026-10-20 | D3 选 B 或用户反馈冲突 |
| 主窗添加栏同样支持 `@`/`#` | 2026-10-31 | 小窗语法稳定后 |
| macOS 真正全局（若本轮降级） | 视调研 | 找到无辅助功能权限方案或用户接受权限提示 |

---

## 6. Lessons Learned

（仅在事件真实发生后追加；禁止预填。）

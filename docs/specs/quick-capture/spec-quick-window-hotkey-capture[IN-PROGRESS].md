# spec-quick-window-hotkey-capture: 快捷键显隐 + 小窗捕捉（@项目 #标签补全）

## Metadata

- **ID**: spec-quick-window-hotkey-capture
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf
- **Created Date**: 2026-09-16
- **Last Updated**: 2026-09-18

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
- 项目：匹配已有项目名（`ProjectName.Normalize`）。**无 `@` → 归属 Default**（R-2.6）。有 `@` 未知名 → **保存时创建**项目并归属（R-1.8，2026-09-18）。
- 标签：匹配已有标签；未知 `#` → **保存时创建**标签并绑定（R-1.8）。
- 补全：候选列表支持 **Tab / Enter 接受**与**鼠标点击接受**（不得只显示不可选）。

### 2.5 已裁决

| # | 议题 | 裁决 |
| :--- | :--- | :--- |
| D1 | 未知 `@项目` | **保存时创建**（2026-09-18 推翻「留在标题」） |
| D2 | 未知 `#标签` | **保存时创建**（同上） |
| D3 | 热键与系统冲突 | 本版本固定 Alt+Space（Win）/ Option(Alt)+Space 或 Meta+Space（Mac）；冲突则 Deferred 可配置 |
| D4 | 无项目 token | 落入 **Default** 项目（废除未归属 null） |
| D5 | 小窗前台二次热键 | 必须 **toggle 隐藏**（不得只能 Esc）；小窗自身须处理同一手势 |

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
| H2 | 小窗打开且**小窗前台**时再按同一热键（Mac：`Option+Space`），或按 `Esc` | 小窗隐藏（进程仍在）。**不得**只能 Esc | [ ] |
| H3 | **Windows**：点一下别的应用（浏览器等）让 FlowTask 主窗到后台，再按 `Alt+Space`（若被系统占用则试 `Win+Alt+Space`） | 小窗仍能弹出。若完全无反应，在下方记「全局失败，仅前台可用」 | [ ] |
| H4 | **macOS**：主窗到后台后再按热键 | 本轮可跳过；记下真实结果可选 | [ ] |
| H5 | 小窗输入：`买菜` → `Enter` | 主窗 **Default** 下出现「买菜」；无新建项目/标签 | [ ] |
| H6 | 输入：`修登录 @你的项目名`（打 `@` 后出补全；↑↓、`Tab`/`Enter`、**鼠标点选**均可接受）→ 保存 | 标题「修登录」；归属该项目 | [ ] |
| H7 | 输入：`读文档 #你的标签`（补全同上）→ 保存 | 标题「读文档」；带该标签；项目 Default | [ ] |
| H8 | 输入：`灵感 @新项目甲 #新标签乙` → 保存 | **新建**项目「新项目甲」与标签「新标签乙」；标题「灵感」；任务归属新项目 | [ ] |
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

### 2026-09-24 Bug 修复：Windows 双开小窗 + 两平台方形纯色边框

**Attribution**：`code wrong`（重入未防护 + 事件路径未去重）；边框问题为 `design wrong`（依赖平台透明合成协商结果，未提供确定性兜底，与 `MainWindow` 用 `AppearanceCoordinator` 显式构造背景画刷的既有模式不一致）。

**Bug A：Windows 上快捷键可能叫出第二个窗口**

- 根因：Windows 同时存在两条独立监听 `Alt+Space` 的路径——进程级 `GlobalHotkeyService`（`RegisterHotKey`，任意前台窗口都会响应）与 `MainWindow.OnWindowKeyDown`（仅当主窗口有键盘焦点时响应）。同一次物理按键可能被两条路径先后触发 `ToggleQuickCaptureWindowAsync()`；该方法内部 `await PrepareAsync()` 让出 UI 线程，且原实现没有重入保护，第二次调用会在第一次的 `Show()`/`Hide()` 判定完成前抢先执行，表现为小窗被连续 `Show()` 两次或开关状态错乱。macOS 因为 `GlobalHotkeyService.TryStart()` 仅在 Windows 平台真正注册系统热键，只有窗内单一路径，预览环境复现不出来。
- 修复：
  1. `MainWindow.ToggleQuickCaptureWindowAsync` 加 `_isTogglingQuickCapture` 重入锁，同一时刻只跑一次 toggle 逻辑，多余调用直接忽略。
  2. 新增 `MainWindow.SetSystemHotkeyActive(bool)`；`App.axaml.cs` 把 `GlobalHotkeyService.TryStart()` 的返回值传入。系统级热键注册成功后，`MainWindow.OnWindowKeyDown`、主题按钮上的 Tunnel 处理器、`QuickCaptureWindow.OnPreviewKeyDown` 里对应的窗内 `Alt+Space` 分支全部跳过，避免同一次按键被多路径重复触发；注册失败（含 macOS）时保留窗内监听作为唯一回退。
- **验证边界（如实记录）**：本次改动在 macOS 上完成了编译（`dotnet build`）与既有单测（`dotnet test`，195 项全部通过）验证，但 Windows 上系统级热键与窗内路径并发触发的真实场景，macOS 环境无法复现，也没有 Windows 机器可用，**此项修复的人工验证需等 owner 在 Windows 上实测**。

**Bug B：小窗外围一圈方形纯色边框（两平台均可见）**

- 根因：`QuickCaptureWindow.axaml` 原设计用 `Background="Transparent"` + `TransparencyLevelHint="AcrylicBlur, Blur"`，寄望平台透明合成协商成功，让圆角胶囊 `Border` 外多留的 14px `Panel Margin`（专门给 `BoxShadow` 投影用）保持真正透明。一旦协商失败（Windows 多数环境默认关闭透明特效、远程桌面、部分显卡驱动；macOS 上 Avalonia 对 `Blur`/`AcrylicBlur` 系列材质的支持同样不稳定），Avalonia/OS 会用不透明默认色填满整个窗口矩形，这圈 14px 留白就在圆角胶囊外露出方形纯色边框。这与主窗口 `MainWindow` 的既有模式不一致：`MainWindow` 从不依赖透明合成协商结果，而是用 `AppearanceCoordinator.BuildWindowBackground` 显式构造确定的背景画刷。
- 修复：`QuickCaptureWindow.axaml` 去掉 `Panel Margin="14"` 结构，让窗口矩形与圆角胶囊 `Border` 直接重合（`TransparencyLevelHint` 收窄为 `Transparent, None`），代价是舍弃了投影效果。
- **验证边界（如实记录）**：本次改动只完成了 XAML 结构调整与编译验证，**未做任何视觉验证**——执行环境的模型不支持读取截图，且辅助功能权限未开放导致无法用 AppleScript 模拟按键触发小窗弹出后截图。当前判断（窗口矩形与胶囊重合可消除方形边框）基于对 Avalonia 透明合成失败行为的代码级推理，**尚未在任何真实平台上目测确认**，需 owner 在 macOS 与 Windows 上分别实测。若圆角处仍有可见的小三角残留区域，需要进一步处理（例如改用支持真正透明通道的合成路径，或用 clip 遮罩圆角外区域）。

**教训**：
- 涉及窗口透明合成/系统级热键这类平台行为差异较大的功能，视觉与交互层的验证不能仅凭代码推理替代——本次因执行环境限制（无法截图、无 Windows 机器）只能做到编译级验证，属于验证覆盖不完整的已知缺口，如实记录而非假装已验证。
- 新增窗口级功能（如 `QuickCaptureWindow`）时应优先复用既有的确定性模式（`AppearanceCoordinator` 式显式背景画刷），而非引入新的、依赖平台协商结果的路径，减少两套背景处理逻辑并存带来的一致性风险。

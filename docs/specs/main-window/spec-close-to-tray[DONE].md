# spec-close-to-tray: 主窗关闭策略 + 系统托盘保活

## Metadata

- **ID**: spec-close-to-tray
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **2026-09-27 Step 0 已确认**（所有者原话「确认」）。
> **2026-09-27 Step 2 已确认**：计划总体没问题；单例改为后来者关掉旧进程再启动（含不同 preview exe）。

**上游依据**：

- 需求：本轮将增补 `REQUIREMENTS.md` R-5（关窗策略 / 托盘 / 单实例）；R-1.5 仍只约束「进程已启动后热键可用」，本 SPEC 让主窗 X 之后进程仍可处于该状态
- 设计：`design-interaction-principles` 不覆盖关窗；归档文档曾把「托盘常驻」列为非目标，本轮所有者原话推翻为要做
- 既有实现：`QuickCaptureWindow.Closing` 一律 `Cancel + Hide`；Avalonia 默认 `OnLastWindowClose` → 主窗 X 后小窗与进程残留、主窗已销毁、无入口

**本轮用户原话（2026-09-27）**：

> 「感觉托到右下角更合理一点。」
>
> 「按 x 时在没有选择默认值时提示彻底关闭或置于托盘，可以通过是否设为默认直接修改配置，也可以类似图片那样出一个设置项，可以放在 general 通用设置中。」
>
> 「左键是呼出主窗口，右键是可以选择的功能。我希望右键提供呼出/关闭小窗的能力，以及彻底的退出的功能选项。」
>
> 「可以考虑设置页中也提供一个彻底退出的按钮……不应当在每个设置项都存在……目前可能放在通用比较合理。」
>
> Step 0 复述确认：「确认」。拍板：未设默认则每次询问；设为默认写入通用设置；未选过时两个单选项均不选中；托盘标推荐；彻底退出无二次确认；右键只有显隐小窗 + 退出，打开主窗只靠左键。
>
> Step 2 修订（2026-09-27）：「本机目前多 preview 的情况下，再开不同版本的 exe 怎么处理？我的建议是直接关旧的开新的。」单例不是唤回旧实例，而是新进程取代旧进程。

---

## Why

主窗点 X 后出现无入口残留：主窗被真正 `Close`，小窗因 `Closing` 被取消而继续活着，进程与全局热键仍在，任务栏与托盘都找不到主窗。这不是保活，是半截退出。

根因是生命周期合同缺失（design wrong + code wrong）：

1. 产品从未定义 X 的语义，代码走 Avalonia 默认「关到最后一个窗口」；
2. 小窗为热键复用故意永不 `Close`，于是最后一个窗口永远关不掉；
3. 没有托盘作为找回主窗的入口。

全局热键（R-1.5）要求进程活着才有意义。所有者裁决保活路径是系统托盘，而不是静默僵尸进程。

## What

主窗系统关闭（标题栏 X / Alt+F4 / 任务栏关闭）走可配置策略；进程用托盘保活；真正退出只有显式「彻底退出」。

### 关闭策略（单一真源）

`AppSettings` 键（Desktop 层常量，不进 Core 仓储接口，对齐 `AppearanceCoordinator`）：

| 存值 | 含义 |
| :--- | :--- |
| 缺键 / 空 / 无法识别 | **未设默认**：每次主窗关闭请求都弹出选择层 |
| `tray` | 隐藏主窗到托盘，不销毁，不退出进程 |
| `exit` | 彻底退出（与托盘「退出」、通用页按钮同一条路径） |

设置页两个单选项选中其一即立刻写入（无单独「保存」按钮）。一旦写入，不能回到「未设默认」；要换策略只能改选另一个单选项。最小化按钮（任务栏缩成按钮）**不**走本策略，仍是普通最小化。

### 未设默认时的选择层

拦截 `MainWindow.Closing`（先 `Cancel`），在主窗内弹出覆盖层（不另开 Window，避免再占一条生命周期）：

- 标题：关闭窗口
- 圆点选项（在「设为默认」上方）：最小化到托盘 / 直接退出；打开时预选托盘（与设置页推荐一致）
- 勾选：设为默认
- 按钮：取消（不关闭）/ 确认（执行当前圆点选项）。勾选设为默认则把该选项写入上述键
- 点遮罩等同取消

### 托盘

进程在期间托盘图标常驻（主窗可见时也在，避免「藏起来才出现图标」造成找不到）。

| 操作 | 行为 |
| :--- | :--- |
| 左键 | 显示并激活主窗 |
| 右键 · 显示小窗 / 隐藏小窗 | 文案随小窗当前可见性切换；调用现有 toggle，不重建窗口 |
| 右键 · 退出 | 彻底退出，无二次确认 |

图标用现有 `flowtask-icon.ico`。提示文本：`FlowTask`。

### 彻底退出

唯一允许进程结束的路径。置退出旗标后：

1. 小窗 `Closing` **不再** Cancel（现有「关改藏」仅在非退出时生效）；
2. 注销全局热键（沿用 `desktop.Exit`）；
3. `ShutdownMode.OnExplicitShutdown` + `Shutdown()`，主窗与小窗一并关掉。

入口：选择层「彻底退出」、设置「彻底退出」、托盘右键「退出」、以及默认策略为 `exit` 时的主窗 X。

### 通用设置

只在「通用」一页，顺序：

1. 现有「默认到期偏移」
2. 「关闭窗口」：两个单选项；「最小化到系统托盘」带推荐标记；未设默认时两个都不选中
3. 页底「彻底退出」按钮（非主操作样式，不出现在外观 / 关于）

### 单实例（后来者取代）

本机所有 FlowTask 进程共用一把互斥（含 `preview/dev`、`preview/feature/<name>`、`dotnet run` 的 `FlowTask.Desktop`）。再启动任一 exe：

1. 新进程请旧进程走彻底退出（热键注销、小窗真正 Close、进程结束）；
2. 旧进程退出后，新进程接管并显示自己的主窗；
3. 若旧进程在时限内未退出，新进程强杀其它 `FlowTask` / `FlowTask.Desktop` 后仍启动，避免 exe 打不开。

同一路径连点两次同样关旧开新，不保留「激活已有窗口」。Windows 为验收平台。发布时文件是否被运行中的 exe 锁住不在本 SPEC（发布仍写 `_tmp` 再替换）。

### 需求增补（须先写入 REQUIREMENTS，再实现）

| ID | 需求 |
| :--- | :--- |
| R-5.1 | 主窗关闭可配置为托盘隐藏或彻底退出；未设默认时每次询问并可「设为默认」 |
| R-5.2 | Windows 托盘：左键主窗，右键显隐小窗 + 退出 |
| R-5.3 | 单实例；再启动任一副本时关掉旧进程、由新进程接管 |

## Non-goals

- 开机自启
- 设置里提供「每次询问」第三项（写入后不能回到未设默认）
- 彻底退出前的二次确认
- 托盘右键再放「打开主窗口」（左键已承担）
- 独立托盘图标资源 / 动画
- Linux 托盘
- 主窗几何（位置/尺寸）记忆
- 把小窗自身 Esc / 热键显隐改成退出（小窗仍只 Hide）

## Constraints and decisions

| # | 议题 | 裁决 | 来源 |
| :--- | :--- | :--- | :--- |
| D1 | X 的产品语义 | 可配置托盘或退出，未设默认则问 | 所有者 2026-09-27 |
| D2 | 推荐项 | 最小化到系统托盘标「推荐」 | 所有者附图 + 确认 |
| D3 | 设为默认 | 选择层勾选，或通用设置点选，写同一键 | 所有者原话 + Step 0 |
| D4 | 未设默认的设置展示 | 两个单选项均不选中 | Step 0 确认 |
| D5 | 二次启动 | 关旧开新（不同 preview 路径亦然）；优雅退出失败则强杀其它 FlowTask 进程 | Step 2 所有者原话 |
| D6 | 彻底退出确认 | 不再问 | Step 0 确认 |
| D7 | 右键菜单 | 仅显隐小窗 + 退出 | Step 0 确认 |
| D8 | 最小化按钮 | 仍进任务栏，不进托盘 | **[推断]** 所有者只定义了 X；Windows 最小化与关闭是不同系统按钮 |
| D9 | 选择层形态 | 主窗内覆盖层，不另开 Window | **[推断]** 另开 Window 会再踩 `OnLastWindowClose`；对齐到期日弹出层 |
| D10 | 键名与解析 | Desktop 常量 + 纯函数；坏值当未设默认 | 对齐 spec-appearance-persist |
| D11 | 退出与小窗 Hide | 退出旗标绕过 Cancel | 修当前残留的直接原因 |
| D12 | TR-1 | 关闭决策 / 退出不写进 `MainViewModel` 长方法，抽协调器或 Action VM | technical-rules TR-1 |
| D13 | 跨窗引用 | 托盘不持有小窗 ViewModel 强引用；显隐走主窗已有 toggle | project-rules |

## Acceptance criteria

- [x] 从未设过关闭默认：主窗 X 弹出选择层，主窗仍在；点遮罩取消后主窗仍在、进程仍在。
- [x] 选择层圆点选「最小化到托盘」且不勾设为默认，点确认：主窗隐藏、托盘在、热键仍能唤小窗；再开主窗点 X 仍弹出选择层。
- [x] 选择层圆点选「直接退出」且不勾设为默认，点确认：进程结束（任务管理器无 FlowTask）；下次启动仍无默认、再点 X 仍问。
- [x] 勾选设为默认后点确认，选择写入通用设置对应项；之后主窗 X 不再弹出选择层，按该默认执行。
- [x] 选择层点取消或点遮罩：主窗仍在，不退出、不进托盘。
- [x] 通用设置两个单选项：点选立刻生效；「最小化到系统托盘」有推荐标记；从未写入时两个都不选中。
- [x] 默认 `tray`：X 后任务栏无主窗按钮，托盘在；左键主窗回到前台。
- [x] 默认 `exit`：X 后进程结束，无小窗残留。
- [x] 托盘右键可显示/隐藏小窗；「退出」结束进程。
- [x] 通用页底部「彻底退出」结束进程；外观页与关于页没有该按钮。
- [x] 真正退出后无 FlowTask 进程，全局热键失效。
- [x] 进程已在时再开另一份（或同一份）exe：旧进程结束，新进程成为唯一实例并显示主窗。
- [x] 标题栏最小化仍把主窗留在任务栏，不进托盘策略。
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不回退。

## Staged plan

1. [x] REQUIREMENTS 增补 R-5；关闭策略纯函数 + `AppSettings` 读写单测。
2. [x] 主窗 `Closing` 拦截 + 未设默认选择层（含设为默认）。
3. [x] 通用设置：关闭窗口单选项 + 页底彻底退出。
4. [x] 托盘图标与菜单；`OnExplicitShutdown` 真退出（绕过小窗 Closing Cancel）。
5. [x] Windows 单实例（后来者请旧进程彻底退出后接管；超时则强杀）。
6. [x] 全量构建/测试；发布 `preview/feature/close-to-tray/FlowTask.exe`。

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md`：R-5.1 / R-5.2 / R-5.3
- [x] Desktop 关闭策略键名常量 + 解析/序列化纯函数
- [x] 主窗 Closing 拦截、选择层、Hide vs Shutdown
- [x] 小窗 Closing：仅非退出时 Cancel
- [x] `ShutdownMode.OnExplicitShutdown`
- [x] Avalonia `TrayIcon`：左键 / 右键菜单
- [x] 通用设置 UI + 页底退出
- [x] 单实例：关旧开新 + 超时强杀其它 FlowTask 进程
- [x] 单测：策略解析、缺键/坏值、round-trip
- [x] 本 SPEC 状态与 `docs/specs/README.md` 索引

## Progress log

### 2026-09-27

- Completed: 所有者预览通过（2026-09-27「该功能没有问题了」）。选择层改为圆点选项 + 确认/取消。SPEC 收为 `[DONE]`，合入 `dev`。
- Decisions: D5 后来者取代；选择层圆点在「设为默认」上方，按钮为确认/取消。
- Current resume point: 无（已关闭）。合入 `dev` 后本分支工作树删除，git 分支保留。

## Verification

- Automated: `dotnet build FlowTask.sln` 0 警告 0 错误；`dotnet test FlowTask.sln` 217 通过 / 0 失败（2026-09-27）。
- Manual: 所有者 2026-09-27 用 `preview/feature/close-to-tray/FlowTask.exe` 验收，原话「该功能没有问题了」。
- Not run or not covered: macOS 菜单栏托盘与单实例（本轮 Windows 验收；macOS 若 Avalonia `TrayIcon` 可用则一并挂上，行为差异记入本 SPEC，不阻塞 Windows）。

## Risks and open questions

- Owner: 无待裁决项。
- Blocker or trigger: Windows 单实例若命名管道/互斥体在无管理员权限下失败，必须仍能启动（降级为多实例并记入 Lessons），不得让 exe 打不开。
- Avalonia `TrayIcon` 左键在部分 Windows 版本需 `Clicked` 与菜单并存，实施时按平台实测接线，不在此预先发明第二套手势。

## Lessons learned

主窗 X 与小窗「关改藏」不能各管各的生命周期。小窗 `Closing` 一律 Cancel 时，Avalonia 默认 `OnLastWindowClose` 会把进程钉在无主窗、无托盘、无入口的状态。退出必须有独立旗标绕过 Cancel，Hide 只能作为托盘保活路径。

## Related documents

- SPECs: [spec-quick-window-hotkey-capture[IN-PROGRESS]](../quick-capture/spec-quick-window-hotkey-capture[IN-PROGRESS].md)（进程级热键依赖进程存活）；[spec-appearance-persist[DONE]](../visual-theme/spec-appearance-persist[DONE].md)（AppSettings 键在 Desktop）；[spec-settings-master-detail-and-theme-presets[DONE]](../visual-theme/spec-settings-master-detail-and-theme-presets[DONE].md)（通用页）
- Requirements: `REQUIREMENTS.md` R-1.5；本轮增补 R-5
- Rules: `rule-no-invented-user-behavior`；`rule-spec-review-gate`；`technical-rules.md` TR-1
- Archived: `design-task-domain-superseded.md` §1.4 曾排除「托盘常驻」，本 SPEC 按所有者原话覆盖该排除（不回写已归档文）

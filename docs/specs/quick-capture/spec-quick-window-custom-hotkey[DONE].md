# spec-quick-window-custom-hotkey: 快捷小窗自定义快捷键与占用处理

## Metadata

- **ID**: spec-quick-window-custom-hotkey
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-10-01
- **Last Updated**: 2026-10-01

> **开工许可**：2026-10-01 owner 确认计划并裁决 Q1–Q5，原话
> 「1.如果可以留接口不实现，说明这是需要mac适配的，方便后续在mac继续开发 2，需要 3.拒绝 4.启动提示 5.可以」。

## Why

2026-10-01 owner 发现快捷小窗快捷键从 Alt+Space 变成了 Win+Alt+Space，问是谁改的。

排查结论：没有任何程序改过 FlowTask 的设置。`GlobalHotkeyService.MessageLoop` 每次启动先注册
Alt+Space，失败就静默改注册 Win+Alt+Space。本机 PowerToys Run（`open_powerlauncher` = Alt+Space）
13:07 启动，preview 15:44 启动，Alt+Space 已经被 PowerToys 先占了。

问题有三个：

1. 回退是静默的。用户只看到键帽变了，不知道为什么。
2. 用户无法自己选组合，只能去改其他程序。
3. Windows 注册成功或失败都不写日志，事后查不到当时注册了什么、为什么失败。

Attribution：design wrong —— spec-quick-window-hotkey-capture 把唤起键定死为 Alt+Space，
a5bf5d5 又加了用户看不见原因的自动回退。

用户原话：

> 「我的快捷键为什么被改了？是你检查到与本机快捷键冲突改的，还是被外部强制改的？另外，我觉得可以考虑新增自定义快捷键的功能」
>
> 对方案逐条裁决：「1.正确 2.快捷键不多，暂时收录进通用 3.没问题 4.占用的话应该提示并且临时对本项目禁用快捷键，弹窗提示一次 5.同意 6.同意」

## What

| # | 行为 | 来源 |
| --- | --- | --- |
| W1 | 只有「呼出/隐藏快捷小窗」这一个全局快捷键可自定义；窗内键位（Enter、Esc、Ctrl+Tab、Ctrl+D）不变 | 方案 1，owner「正确」 |
| W2 | 设置 → **通用** 新增一行「快捷小窗快捷键」：显示当前组合，点「修改」进入录制，按下组合键即提交 | owner「暂时收录进通用」 |
| W3 | 新组合**立即生效**，不需要重启；注册失败时保留旧组合，并在该行提示「该组合已被其他程序占用」 | 方案 3，owner「没问题」 |
| W4 | 启动时已保存的组合注册失败：**不回退到任何备选组合**，本次运行停用 FlowTask 的快捷小窗快捷键，并弹窗提示一次 | owner「占用的话应该提示并且临时对本项目禁用快捷键，弹窗提示一次」 |
| W5 | Windows 注册结果（组合、成功/失败、Win32 错误码）写入 `AppLog` | 方案 5，owner「同意」 |
| W6 | 同步操作指南；补齐组合解析、文案、冲突处理的单测 | 方案 6，owner「同意」 |

细化（标注 `[推断]` 的条目待 owner 确认，见 Risks Q1–Q5）：

- 存储：`AppSettings` 键 `QuickWindow.Hotkey`，值为规范字符串，如 `Ctrl+Alt+K`。缺键或无法解析时用默认 Alt+Space。
- 录制规则：至少带一个 Ctrl / Alt / Win；主键只能是 A–Z、0–9、F1–F12、Space。录制中按 Esc 取消。
- 「临时停用」只限本次运行。下次启动按已保存组合重新注册，**不改写**已保存值。
- 停用期间：窗内回退监听同样关闭；侧栏键帽显示「已停用」；托盘「显示小窗」和侧栏「快捷小窗」按钮仍然可用。
- 停用期间在设置里改成可用组合：注册成功即恢复，并保存。
- 删除 Win+Alt+Space 自动回退和 `RegisteredHotkey` 枚举。键帽文案改为由「当前组合 + 是否生效」推出。
- `[推断]` 弹窗是居中覆盖层，做法沿用删除项目确认框。文案：「快捷键 X 已被其他程序占用，本次运行已停用 FlowTask 的快捷小窗快捷键。可以在 设置 → 通用 换一个组合。」按钮为「去设置」和「知道了」。
- `[推断]` 录制提示加一句「按下没反应，说明该组合已被其他程序占用」。理由：被其他程序注册的组合会在系统层被拦截，FlowTask 的窗口收不到按键，录制框不会有任何反应。

## Non-goals

- 窗内键位自定义。
- 多个全局快捷键（例如「直接新建任务」）。
- 识别占用者是哪个程序。Windows 的 `RegisterHotKey` 只返回失败，不会告诉调用方是谁占用的。
- 导入或导出快捷键配置。

## Constraints and decisions

- Article 6：组合的解析、规范字符串、显示文案、Win32 修饰位/虚拟键、窗内匹配，统一由 `QuickWindowHotkey`（替换 `QuickCaptureHotkey`）一处定义。
- TR-1：修改组合是一个用户动作，抽成 `ChangeQuickWindowHotkeyViewModel`。`MainViewModel` 只保留状态和薄委托。
- 可测性：注册能力通过 `IQuickWindowHotkeyRegistrar` 注入，单测用假实现模拟占用。
- 线程：`RegisterHotKey` 绑定在调用线程上。运行时换键必须投递到热键消息线程执行，再等待结果。等待沿用 `HotkeyRegistrationWait`，不使用 Sleep（Article 9）。
- 先注册新组合（使用第二个 id），成功后再注销旧组合，保证失败时旧组合仍然生效（W3）。
- BR-1：会更新指南。快捷小窗条目是首启引导步骤，但气泡文字（Summary）和锚点不变，只在指南页 Points 里加一条，所以不提升 `OnboardingProgress.CurrentVersion`（Q5）。

## Acceptance criteria

- [x] 设置 → 通用显示当前组合；点「修改」后按下 Ctrl+Alt+K，侧栏键帽立刻变为 Ctrl+Alt+K，新组合可以呼出小窗，旧组合失效。
- [x] 重启后组合仍是 Ctrl+Alt+K。
- [x] 录制一个被占用的组合（例如 PowerToys 开着时按 Alt+Space）：旧组合继续生效，该行出现占用提示。
- [x] 录制不合规的组合（无修饰键、主键不在允许范围内）：给出原因，不保存。录制中按 Esc 取消，不做任何改动。
- [x] 已保存组合在启动时被占用：只弹窗一次；键帽显示「已停用」；按该组合不会触发 FlowTask；托盘和侧栏按钮仍能打开小窗；不再出现 Win+Alt+Space。
- [x] 停用状态下在设置里改成可用组合：立即恢复生效，键帽更新。
- [x] `flowtask-YYYYMMDD.log` 中有 Windows 注册成功/失败记录（组合和错误码）。
- [x] 操作指南的快捷小窗条目写明可在 设置 → 通用 修改，以及被占用时的表现。
- [x] build 0 warning / 0 error；测试不低于基线 315（388）。

## Staged plan

1. REQUIREMENTS 增补 R-1.11（自定义 + 占用停用），SPEC 转 `[IN-PROGRESS]`。
2. `QuickWindowHotkey` 模型：解析、规范化、校验、文案、Win32 映射、窗内匹配，并补单测。
3. `GlobalHotkeyService`：可配置组合、运行时换键（先注册后注销）、注册日志；去掉 Win+Alt+Space 回退。
4. `ChangeQuickWindowHotkeyViewModel` + `MainViewModel` 状态（当前组合 / 生效与否 / 占用弹窗 / 录制态），并补单测。
5. 视图：通用页的录制行、启动占用弹窗、键帽「已停用」；`MainWindow` 与 `QuickCaptureWindow` 的窗内匹配改用当前组合。
6. `App` 启动流程：读设置 → 注册 → 失败则停用并弹窗。
7. 指南文字同步；build + test + preview。

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md`：R-1.11
- [x] `Services/QuickWindowHotkey.cs`（新增，取代已删除的 `QuickCaptureHotkey.cs` 与 `RegisteredHotkey`）
- [x] `Services/IQuickWindowHotkeyRegistrar.cs`（新增，含 `HotkeyRegistrationOutcome`、`NoSystemHotkeyRegistrar`）
- [x] `Services/GlobalHotkeyService.cs`：实现注册器接口；`TryApplyAsync` 投递到消息线程、双 id 先注册后注销；注册日志带 Win32 错误码；删除 Win+Alt+Space 回退；macOS 保留 `TryStartFixedAsync`，`TODO(macos-custom-hotkey)` 标在 `TryApplyAsync`
- [x] `Services/HotkeyRegistrationWait.cs`：改为 `WaitAsync`，超时抢占结果，迟到的成功由消息线程注销
- [x] `ViewModels/Actions/ChangeQuickWindowHotkeyViewModel.cs`（新增）
- [x] `ViewModels/QuickWindowHotkeyViewModel.cs`（新增：运行状态、录制、恢复默认、占用弹窗；从 MainViewModel 拆出，TR-1）
- [x] `ViewModels/MainViewModel.cs`：持有 `Hotkey`；`IsBlockingOverlayOpen` 计入占用弹窗；离开设置 / 切走通用时取消录制；删除 `QuickCaptureHotkeyLabel` / `SetRegisteredHotkey`
- [x] `Views/MainWindow.axaml(.cs)`：通用页录制行、占用弹窗、键帽绑定 `Hotkey.Label`；窗内匹配改为 `ShouldHandleInWindow`；删除 `SetSystemHotkeyActive` / `IsQuickCaptureModifier`；托盘走 `ToggleQuickCaptureFromTray`
- [x] `Views/QuickCaptureWindow.axaml.cs`：窗内匹配改用当前组合
- [x] `Styles/EditorialStyles.axaml`：`CaptionText.HotkeyError`
- [x] `App.axaml.cs`：先建 `GlobalHotkeyService` 注入 `MainViewModel`，主窗构造后 `Hotkey.StartAsync()`
- [x] 测试：新增 `QuickWindowHotkeyTests`、`QuickWindowHotkeyViewModelTests`；改写 `HotkeyRegistrationWaitTests`、`QuickCaptureHotkeyTests`、`OnboardingTests`、`StartupAppearanceOrderTests` 中的热键用例
- [x] `docs/specs/infrastructure/spec-macos-initial-support[IN-PROGRESS].md`：登记 `TODO(macos-custom-hotkey)`
- [x] Guide: updated 快捷小窗（Points 增加修改入口与占用停用说明；Summary 与锚点不变，不提升引导版本，owner 裁决 Q5）

## Progress log

### 2026-10-01

- Completed：排查快捷键变化原因（见 Why）；owner 对方案 1–6 给出裁决；建立 worktree 并写本 SPEC。
- Decisions：见 What 表 W1–W6。
- Owner 确认计划并裁决 Q1–Q5（见 Risks），SPEC 转 `[IN-PROGRESS]`。
- Completed：Staged plan 1–7 全部实施（见 Change checklist）。
- Decisions（实施中）：
  - 运行状态拆为 `QuickWindowHotkeyViewModel`，做法同 `OnboardingViewModel`，避免 `MainViewModel` 再膨胀（TR-1）。
  - 录制时按下当前组合：系统级热键会先于窗口收到它，`ToggleQuickCaptureFromHotkey` 把它当作「未修改」并结束录制，不切换小窗。
  - 同一组合再次注册直接视为成功：否则 `RegisterHotKey` 会因「自己已注册」失败，被误报成被其他程序占用。
  - 托盘菜单改走 `ToggleQuickCaptureFromTray`，不受录制或停用影响。
- Owner 预览验收通过，原话「预览通过」；SPEC 转 `[DONE]`。
- Current resume point：无（已关闭）。macOS 自定义见 `TODO(macos-custom-hotkey)`，归 spec-macos-initial-support。

## Verification

- Automated：
  - `dotnet build FlowTask.sln`：0 警告、0 错误。
  - `dotnet test FlowTask.sln --nologo -v q`：388/388 通过（基线 315）。
- Manual：owner 在 `preview/feature/custom-quick-window-hotkey/FlowTask.exe` 上验收通过（2026-10-01，「预览通过」）。
- Not run or not covered：
  - 真实 `RegisterHotKey` 换键、占用失败和日志内容，单测里用假注册器替代。
  - macOS 实机（只读路径；自定义未实现，见 Q1）。

## Risks and open questions

- Owner: aisdwf
- Q1 macOS 范围 —— **已裁决（2026-10-01）**：「如果可以留接口不实现，说明这是需要mac适配的，方便后续在mac继续开发」。
  `IQuickWindowHotkeyRegistrar.SupportsCustomHotkey` 在 macOS 返回 false；macOS 仍按原逻辑注册 Option+Space（含 Command+Option+Space 兜底）；
  通用行在 macOS 只读并写明「macOS 暂不支持修改（待适配）」。待适配点用 `TODO(macos-custom-hotkey)` 标在
  `GlobalHotkeyService.TryApplyAsync` 上，同时登记到 [spec-macos-initial-support](../infrastructure/spec-macos-initial-support[IN-PROGRESS].md)。
- Q2 恢复默认 —— **已裁决**：「需要」。通用行提供「恢复默认」，目标组合为 Alt+Space，与修改走同一条注册路径（被占用时同样提示）。
- Q3 禁用组合 —— **已裁决**：「拒绝」。录制时拒绝 Ctrl+C/V/X/Z/Y/A/S、Ctrl+Tab、Ctrl+D、Alt+Tab、Alt+F4，并说明原因。
- Q4 弹窗频率 —— **已裁决**：「启动提示」。每次启动只要已保存组合注册失败就弹一次；同一次运行内不再重复弹。
- Q5 引导版本 —— **已裁决**：「可以」。不提升 `OnboardingProgress.CurrentVersion`。
- 风险：首启时如果出现占用弹窗，它属于阻断层，首启引导会让到下次启动再播（`ArmFirstRunOnboarding` 现有逻辑）。

## Lessons learned

- 根因：组合没有唯一来源。注册器写死两个常量，界面文案另写一份；a5bf5d5 只修了文案，没有收回「静默换键」这个决策。
  现在组合只在 `QuickWindowHotkey` 定义，失败如实上报，不由服务层替用户做选择。
- `RegisterHotKey` 不区分「被别人占用」和「被自己占用」，两者都返回 1409。换成同一组合时必须先短路，否则会误报占用。
- 超时等待不能只返回失败：消息线程随后成功注册，系统里就多挂了一个热键。等待方必须抢占结果，迟到方据此注销。

## Related documents

- SPECs：[spec-quick-window-hotkey-capture](./spec-quick-window-hotkey-capture[DONE].md)、[spec-onboarding-guide](../main-window/spec-onboarding-guide[DONE].md)（Q3 键帽文案）、[spec-macos-initial-support](../infrastructure/spec-macos-initial-support[IN-PROGRESS].md)
- Rules：`docs/rules/project-rules.md` BR-1、`docs/rules/technical-rules.md` TR-1、`docs/rules/rule-no-invented-user-behavior.md`
- Requirements：REQUIREMENTS R-1.5、R-1.11

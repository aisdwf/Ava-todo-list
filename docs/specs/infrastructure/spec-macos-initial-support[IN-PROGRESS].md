# spec-macos-initial-support: macOS 初步支持

## Metadata

- **ID**: spec-macos-initial-support
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf
- **Created Date**: 2026-09-30
- **Last Updated**: 2026-10-01

## Why

当前 `main` 已包含 Windows 侧的项目管理、快捷小窗、跨窗口刷新、外观持久化和单实例等更新。业务层已经基本跨平台，但桌面启动、托盘、全局快捷键、单实例替换、图标和发布链路仍带有 Windows 假设。macOS 当前可以通过 `run.sh` 构建和测试，但缺少对这些运行时边界的系统化处理与验证。

本 SPEC 的目标是先让现有共享功能在 macOS 上形成一个可验证的初步支持闭环，同时保持 Windows 现有行为和用户数据兼容。数据同步不属于本阶段；未来多端数据只考虑云端同步或导入导出，不共享同一个 SQLite 文件。

## What

首个 macOS 支持阶段围绕现有 `FlowTask.Desktop` 桌面应用完成：

- 明确并隔离 Windows 专用能力与 macOS 回退能力；
- 验证 macOS 上的启动、Dock 图标、窗口关闭策略、快捷小窗、项目绑定和跨窗口刷新；
- 修复已确认的 macOS 平台风险，尤其是单实例替换路径中对 `kernel32.dll` 的依赖；
- 让平台服务失败时有可诊断的日志和可接受的用户体验；
- 增加必要的回归测试，确保 Windows 分支仍由 Windows 专用实现承担；
- 记录 macOS 当前支持边界。

当前阶段不拆分 Android 项目，也不建立云同步或导入导出协议。

## Non-goals

- 不实现 Android 入口项目或移动端 UI；
- 不实现 Windows/macOS 之间的数据同步；
- 不让两个平台直接读取同一个 SQLite 文件；
- 不重构全部桌面 UI；
- 不承诺 macOS 全局快捷键达到 Windows `RegisterHotKey` 的能力；
- 不实现 `.app`、`.dmg` 或 Mac App Store 发布；
- 不改变现有任务、项目、设置的数据契约，除非验证发现 macOS 初步支持必须修复兼容性问题。

## Constraints and decisions

- 遵守 `AI_CONSTITUTION.md` 的文档与代码共同维护、根因说明和单一真源要求。
- 业务逻辑仍放在 `FlowTask.Core` / 共享 ViewModel 中；平台差异放在桌面平台服务或明确的抽象后面。
- Windows 全局快捷键继续由 `GlobalHotkeyService` 负责；macOS 通过 Carbon Event Hot Key 注册
  `Option+Space`，注册失败时回退到窗口级快捷键。
- 数据库默认路径由 `DatabaseLocation` 统一解析；本阶段不引入跨设备数据库访问。
- 现有主窗口启动路径会把未归属任务迁移到 `Default` 项目；该行为属于共享数据迁移，任何平台验证都必须明确记录其影响。
- `docs/specs/README.md` 是生成文件，不能手工编辑或提交。
- 新 worktree 的 SPEC 索引生成命令需要 PowerShell；当前环境没有 `powershell` / `pwsh`，需在继续实施前解决或由所有者明确接受该本地验证缺口。

## Acceptance criteria

- [ ] macOS 可通过项目规定的开发启动方式启动主窗口，且启动失败路径可从本地日志诊断。
- [ ] macOS Dock 图标、窗口图标和快捷小窗图标在实际运行中可见且不因 ICO/PNG 资源路径失败。
- [ ] macOS 的窗口关闭策略行为明确：托盘能力可用时有验证；不可用时有记录的回退行为。
- [ ] macOS 可使用当前支持范围内的快捷键唤起/隐藏快捷小窗，且不会重复触发。
- [ ] 快捷小窗的项目选择、新建任务归属、任务勾选和跨窗口刷新在 macOS 上可验证。
- [ ] 单实例在 macOS 上不会因 Windows 专用 API 导致崩溃；替换行为要么可用，要么明确降级为安全退出并记录边界。
- [ ] Windows 专用代码仍由平台判断保护；现有 Windows CI、构建和测试契约不被削弱。
- [ ] 现有 Windows 数据不被 macOS 支持改动隐式覆盖；涉及启动迁移或破坏性删除的行为在验证记录中单独说明。
- [ ] `dotnet build` 0 警告、`dotnet test` 全绿；SPEC、代码和验证记录保持同步。

## Staged plan

1. **平台边界确认**：盘点当前启动、图标、托盘、热键、单实例、日志和数据库路径，并标出 Windows-only、macOS fallback 与共享逻辑。
2. **方案与最小修复**：在不改变共享数据契约的前提下，为 macOS 初步支持选择最小的平台适配方案，优先处理单实例替换和托盘/图标失败路径。
3. **自动化保护**：补充平台策略和资源路径的回归测试，确保 Windows 专用 API 不在 macOS 路径执行。
4. **macOS 实机验证**：运行应用，验证启动、Dock、关闭、快捷小窗、项目同步和单实例行为，并记录无法自动化覆盖的边界。
5. **Windows 回归验证**：执行完整构建和测试，检查 Windows CI 相关配置与 Windows 分支行为没有被 macOS 适配改变。
6. **文档收尾**：根据实际结果同步需求/设计边界，关闭本 SPEC 或记录明确的未完成项；生成本地 SPEC 索引。

## Change checklist

- [ ] 平台服务和启动路径完成 macOS 风险审查。
- [x] 单实例替换路径不再无条件依赖 Windows 专用 API。
- [ ] 托盘、图标和关闭策略的 macOS 行为有明确实现或降级记录。
- [ ] macOS 快捷小窗路径保留现有共享任务/项目行为。
- [ ] 共享数据路径和启动迁移行为完成兼容性验证。
- [ ] 添加必要的单元/结构回归测试。
- [ ] 完成 macOS 手动验证记录。
- [ ] 完成 Windows 构建与测试回归。
- [ ] 更新 SPEC 状态、进度日志和验证证据。
- [ ] Guide: 待定 — 指南文字只引用侧栏键帽，macOS 下键帽由 `QuickWindowHotkeyViewModel.Label` 显示 `⌥ Space`，与 Carbon 注册的 Option+Space 一致；「藏进托盘」与 Dock 重开主窗在 macOS 上的指南表述，待实机验收托盘行为后由 owner 裁定是否需要改。

## Progress log

### 2026-09-30

- Completed: 从 `origin/main@7087fb3` 创建 `feature/macos-initial-support` worktree。
- Completed: 初始化 CodeGraph，索引 99 个文件。
- Completed: 在当前 macOS 环境执行 `dotnet build`，0 警告、0 错误；执行 `dotnet test`，272/272 通过。
- Completed: 所有者确认本 SPEC 的实施范围和 6 步计划。
- Completed: 单实例服务在 macOS 上保留互斥并安全失败重复实例，Windows 才启用旧进程替换和 `kernel32.dll` 身份校验。
- Completed: 新增单实例平台能力回归测试。
- Completed: macOS `./run.sh --no-build` 启动后进程保持运行，停止前未出现新的启动异常；运行时已复制 `Assets/Brand/flowtask-icon.png`。
- Completed: 同一 shell 启动两个 macOS 实例，第二实例约 9 秒后退出码为 1，并记录“FlowTask 已在运行且无法接管”，没有触发进程强杀。
- Completed: `dotnet publish -c Release -r win-x64 --self-contained true` 交叉发布成功，产物仍为单个 `FlowTask.Desktop.exe`。
- Decisions: macOS 未打包的 `dotnet run` 进程不会出现在 Computer Use 的应用列表中，因此本轮未宣称 Dock、托盘和窗口视觉效果已通过人工验收。
- Decisions: 本阶段只同步代码能力，不做多端数据同步；Android 仅保留为后续架构方向。
- Decisions: 先处理桌面平台边界，不立即拆分共享 Presentation/Application 项目。
- Decisions: macOS 保留单实例互斥，但不执行 Windows 专用的旧进程替换/强杀流程；第二个实例安全失败退出。
- Blocker: 当前环境没有 `powershell` / `pwsh`，无法运行仓库规定的 SPEC 索引生成命令。
- Current resume point: 修改单实例服务和相关回归测试，随后验证 macOS 启动、快捷小窗和共享业务流程。

### 2026-10-01

- Completed: 修复 macOS Option 修饰键映射。Avalonia 在 macOS 将 Option 映射为
  `KeyModifiers.Alt`；此前使用 `Meta` 会导致 `Option+Space` 没有命中小窗逻辑。
- Completed: 为 macOS 增加 Carbon Event Hot Key 注册，优先注册 `Option+Space`，
  注册失败时尝试 `Command+Option+Space`，并在退出时注销事件处理器和热键。
- Completed: 注册日志确认当前临时 `.app` 启动时为
  `GlobalHotkey macOS registered: Option+Space`。
- Completed: 通过临时 `.app` 验证 macOS 能识别应用、显示主窗口、打开快捷小窗、
  将焦点放入输入框，并用 `Esc` 隐藏小窗。
- Completed: 修复后台唤起快捷小窗后关闭会激活主窗口的问题。显示小窗前记录主窗口
  是否已激活；若 FlowTask 原本在后台，关闭小窗后调用 macOS 应用级隐藏，把焦点交还
  给之前的应用。`Esc`、Option+Space 和窗口关闭事件统一经过该路径。
- Revised: 上述“关闭后再隐藏应用”的方案被实测否定：主窗口仍保持可见窗口时，
  macOS 后续解除后台状态会把主窗口和小窗一起展示，且关闭瞬间会出现主窗口闪烁。
  根因修复改为：后台呼出前先隐藏主窗口；关闭小窗后通过 Avalonia
  `IActivatableLifetime.TryEnterBackground()` 让应用回到后台；用户点击 Dock
  触发 `ActivationKind.Reopen` 时再恢复主窗口。这样主窗口不参与快捷小窗的显示生命周期。
- Blocked for owner verification: 当前 UI 自动化通道不能发送系统级全局组合键；
  尝试用 `System Events` 注入真实 `Option+Space` 时被 macOS 拒绝，因为当前终端
  没有辅助功能权限。因此仍需所有者在实际桌面上确认失焦后呼出和再次按键隐藏。
- Reconciled（合入本地 `dev` 时）：本分支从 `origin/main@7087fb3` 切出，早于 BR-1 指南同步规则；
  合入后 `GuideMaintenanceTests.NewSpecsWithChecklist_DeclareGuideImpact` 因缺 `Guide:` 行失败。
  已补待定的 `Guide:` 行（未作结论），并把已关闭的 `spec-quick-window-hotkey-capture` 链接改为 `[DONE]`。

## Verification

- Automated:
  - `dotnet build FlowTask.sln -v q --nologo`: 已通过，0 警告、0 错误。
  - `dotnet test FlowTask.sln --nologo -v q`: 已通过，277/277。
  - `dotnet publish src/FlowTask.Desktop/FlowTask.Desktop.csproj -c Release -r win-x64 --self-contained true`: 已通过；产物 1 个 `FlowTask.Desktop.exe`。
  - CodeGraph 初始化：已完成，99 个文件、1,606 个节点、4,036 条边。
- Manual:
  - macOS 开发启动已执行；进程保持运行，停止前无新的启动日志异常。
  - macOS 双实例互斥已执行；第二实例安全退出。
  - 临时 `.app` 已被 macOS 识别并显示主窗口；快捷小窗打开、输入框焦点和 `Esc` 隐藏已验证。
  - 注册日志已确认 macOS 成功注册 `Option+Space`。
  - 尚未完成所有者对 Dock 图标、菜单栏托盘、主窗关闭策略和失焦状态下
    `Option+Space` 的人工验证。
  - 尚未执行 Windows 真实运行验证。
- Not run or not covered:
  - `powershell -File scripts/build-spec-index.ps1`：当前环境缺少 `powershell` / `pwsh`。
  - macOS 菜单栏托盘、全局快捷键触发和 Dock 视觉效果的所有者人工验收。
  - 当前 UI 自动化通道未获 macOS 辅助功能权限，无法代发全局 `Option+Space`。
  - Windows 真实运行验证；仅完成交叉发布结构验证。

## Risks and open questions

- **Owner**: aisdwf — macOS 关闭主窗口时，若系统托盘行为与 Windows 不同，首个版本采用退出、隐藏到 Dock 还是保留当前托盘策略？
- **Owner**: aisdwf — 当前环境缺少 PowerShell，是否在实施前补齐工具链，还是把 SPEC 索引生成作为外部环境步骤？
- **Owner**: aisdwf — 需要在可识别的 macOS `.app` 或 Rider 启动环境中完成 Dock、
  菜单栏托盘、主窗关闭策略和失焦后 `Option+Space` 的人工验收。
- **Owner**: aisdwf — **TODO(macos-custom-hotkey): [2026-10-15]** 快捷小窗自定义快捷键在 macOS 留接口未实现
  （[spec-quick-window-custom-hotkey](../quick-capture/spec-quick-window-custom-hotkey[DONE].md) Q1，owner 原话
  「如果可以留接口不实现，说明这是需要mac适配的，方便后续在mac继续开发」）。适配点：
  `GlobalHotkeyService.TryApplyAsync` 把 `QuickWindowHotkey` 映射为 Carbon 键码与修饰位并按「先注册新、再注销旧」换键，
  然后令 `SupportsCustomHotkey` 在 macOS 返回 true；设置 → 通用里的只读说明随之消失。需 macOS 实机验收。
- 单实例替换中 `GetNamedPipeClientProcessId` 使用 `kernel32.dll`，在 macOS 上可能导致替换授权失败；需要用跨平台身份校验替代或安全降级。
- 当前 CI 仅在 Windows runner 上执行，无法自动证明 macOS 原生运行时行为。

## Lessons learned

当前代码能在 macOS 上通过构建和 272 个测试，说明共享业务层已经具备跨平台基础；平台适配风险集中在启动和系统集成层。后续实现应优先保持数据契约不变，用小型平台服务抽象隔离差异，避免把 Windows 行为直接复制为 macOS 行为。

## Related documents

- SPECs: [spec-close-to-tray](../main-window/spec-close-to-tray[DONE].md), [spec-quick-window-hotkey-capture](../quick-capture/spec-quick-window-hotkey-capture[DONE].md), [spec-quick-window-project-sync](../quick-capture/spec-quick-window-project-sync[DONE].md)
- ADRs: [adr-technology-stack](../../adr/adr-technology-stack.md)
- Rules: [workflow-methodology](../../rules/workflow-methodology.md), [docs-conventions](../../rules/docs-conventions.md), [rule-spec-complete-before-merge](../../rules/rule-spec-complete-before-merge.md)
- Analysis: [analysis-codegraph-code-audit](../../analysis/analysis-codegraph-code-audit.md)

# spec-windows-single-file-release: Windows 单文件发布与 GitHub Actions 自动构建

## Metadata

- **ID**: spec-windows-single-file-release
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf (via AI agent)
- **Created Date**: 2026-09-23
- **Last Updated**: 2026-09-23

> ## ✅ 开工确认记录（rule-spec-review-gate）
>
> 用户已于 2026-09-23 显式确认 Staged plan（原话：「可以开始」），状态由 `draft` 转为
> `in-progress`，允许开始改动 `src/`、`.github/` 与 csproj 文件。

---

## Why

当前 `dotnet publish` 对 `FlowTask.Desktop`（win-x64）产出 44 个文件散落在同一目录
（`Avalonia.*.dll`、`SkiaSharp.dll`、`HarfBuzzSharp.dll`、`e_sqlite3.dll`、`FlowTask.*.dll`、
`FlowTask.Desktop.exe` 等，见 `publish/win-x64/`）。这是因为发布方式是默认的
**framework-dependent、非单文件**发布：运行库、Avalonia 渲染后端、原生互操作库都以
独立文件形式旁置在 exe 同级目录。

对最终使用者（尤其是内网环境）而言，这带来两个实际问题：

1. 分发体验差：需要整目录打包/解压，不能像绿色单文件工具那样"下载一个 exe 就能跑"。
2. 内网约束更严格：内网机器**不能**执行 `dotnet restore`/`dotnet build`（即不能联网下载 NuGet
   包），只能 `git clone`（走 GitHub 直连或类似 ghproxy 的镜像代理）或下载 release 包的 zip。
   这意味着**内网侧不能有任何构建步骤**，产物必须在公网侧（GitHub Actions）一次性构建完成，
   内网只做"下载 + 解压 + 双击运行"。

当前仓库没有任何 `.github/workflows/`，也没有任何单文件发布配置，这两件事目前都没有解决方案。

## What

1. 修改 `src/FlowTask.Desktop/FlowTask.Desktop.csproj`，为 Windows（`win-x64`）目标增加
   **self-contained + PublishSingleFile** 发布配置，使 `dotnet publish` 产出尽可能少的文件
   （目标：1 个 exe + 尽量收纳所有依赖；受 Avalonia/Skia 原生库限制，若个别 native dll
   无法收进单文件，会在验证阶段明确记录哪些文件仍然旁置，不承诺凭空达成"零残留"）。
2. 新增 `.github/workflows/release-windows.yml`：在 push 形如 `v*` 的 tag 时，使用
   `windows-latest` runner 执行 `dotnet publish`（win-x64，Release，单文件、self-contained），
   打包为 zip，创建/更新对应 tag 的 GitHub Release 并上传该 zip 作为 Release Asset。
   同时支持 `workflow_dispatch` 手动触发，用于验证构建是否成功（不强制要求手动触发也发布
   Release，具体行为在 Staged plan 中给出可选项供用户裁决）。
3. 版本号来源统一为 git tag（如 `v1.2.0` → 产物版本 `1.2.0`），通过 workflow 里的
   `dotnet publish -p:Version=... -p:AssemblyVersion=...` 等参数注入，不需要手工同步
   csproj 里的版本号（csproj 不预置固定版本号，作为 CI 的可选覆盖参数存在，遵循
   Constitution Article 6 单一权威来源——权威来源是 tag，不在 csproj 内维护第二份版本号）。
4. 本 SPEC 自身的验证记录随实施推进更新（不涉及 README 改动——用户明确此需求背景仅供
   参考，不要求交付内网获取说明文档）。

## Non-goals

- 不做 MSI/安装程序、不做代码签名、不做自动更新机制。
- 不覆盖 macOS/Linux 的打包配置（本 SPEC 只聚焦 Windows win-x64）。
- 不改变应用业务逻辑、UI 或数据层行为。
- 不新增强制性的内网侧构建脚本（明确要求内网不能做构建）。

## Constraints and decisions

- Constitution Article 6（单一配置权威）：版本号唯一权威来源是 git tag，csproj 不维护
  第二份版本号真源。
- Constitution Article 9（禁止基于非确定性外部状态分支）：不适用于本 SPEC（CI 触发条件
  基于 git ref，不是业务逻辑分支）。
- `AGENTS.md` Commands 表定义的 build/test 基线（0 警告 0 错误，163 测试通过）在本次改动后
  不得退化 —— 本次改动不涉及业务代码，预期基线不变，仍需实测确认。
- 已确认决策（用户裁决，见对话记录）：
  1. 打包模式：self-contained 单 exe（体积增大到位，接受）。
  2. CI 触发方式：push tag 自动构建并发布 GitHub Release；`workflow_dispatch` 手动触发仅用于
     验证构建是否成功，不产生 Release（避免非预期版本号的发布）。
  3. 版本号来源：从 git tag 提取。
  4. 仓库 `aisdwf/Ava-todo-list` 为 **public** 仓库（已通过 GitHub API 核实
     `"private": false` / `"visibility": "public"`），GitHub Actions 分钟数对 public 仓库
     完全免费、无限额度；本 SPEC 的触发条件（tag push）与 PR/push 到 `main` 无关，不会随
     PR 频率产生额外消耗，此前顾虑的"免费额度是否够用"在当前仓库可见性下不构成约束。
- 未决风险：Avalonia 的原生渲染/文本布局库（`libSkiaSharp.dll`、`libHarfBuzzSharp.dll`、
  `av_libglesv2.dll`）与 SQLite 原生库（`e_sqlite3.dll`）在 .NET 8 `PublishSingleFile` +
  `IncludeNativeLibrariesForSelfExtract=true` 下能否被完全收进单个 exe，**当前开发环境是
  macOS，无法在本机验证 win-x64 实际产物**，必须依赖 GitHub Actions 的 `windows-latest`
  runner 实测。若无法全部收纳，SPEC 会如实记录实际残留文件清单，而不是编造"仅 1 个文件"
  的验收结果。

## Acceptance criteria

- [ ] `src/FlowTask.Desktop/FlowTask.Desktop.csproj` 新增 win-x64 专属的发布配置属性组，
      不影响非 Windows 平台的现有发布行为。
- [ ] 本机（macOS，用于验证配置语法与非 Windows 路径不受影响）执行
      `dotnet build FlowTask.sln -v q --nologo` 保持 0 警告 0 错误。
- [ ] 本机执行 `dotnet test FlowTask.sln --nologo -v q` 保持 163 通过（不回归）。
- [ ] `.github/workflows/release-windows.yml` 新增，YAML 语法有效（通过
      `actionlint` 或等价手段静态检查，若环境不可用则至少人工核对结构 + 触发一次真实
      workflow run 验证）。
- [ ] 推送一个测试 tag 后，GitHub Actions 在 `windows-latest` 上成功产出 zip 包，
      Release Asset 生成，且记录实际产物文件清单（用于确认精简效果与 native dll 残留情况）。
- [ ] 下载该 zip 解压后，在一台 Windows 机器（或用户可验证的环境）上双击 exe 能正常启动
      应用主窗口（此项为用户手动验证，AI 不得以"进程存活"或"构建成功"代替）。

## Staged plan

1. **csproj 改造**：在 `FlowTask.Desktop.csproj` 中为 `win-x64` 增加
   `SelfContained`、`PublishSingleFile`、`RuntimeIdentifier`、
   `IncludeNativeLibrariesForSelfExtract`、`EnableCompressionInSingleFile` 等属性
   （具体是否用 `RuntimeIdentifiers`/条件属性组按平台区分，实现时确定），本机跑一次
   `dotnet build` 确认现有基线不受影响。
2. **Workflow 编写**：新增 `.github/workflows/release-windows.yml`，触发条件
   `push: tags: ['v*']` + `workflow_dispatch`；步骤：checkout → setup-dotnet 8.x →
   `dotnet publish` win-x64 Release 单文件 → 压缩 zip → 上传为 Release Asset
   （使用 `softprops/action-gh-release` 或 GitHub 官方 CLI，实现时选定并说明理由）。
3. **本机验证**：`dotnet build` / `dotnet test` 跑通，不回归现有基线。
4. **CI 首次实测**：推送测试 tag（如 `v0.0.0-test1`），观察 Actions 运行结果，记录实际产物
   文件列表（确认单文件效果、记录残留 native dll 情况）。
5. **SPEC 记录同步**：本 SPEC 补齐 Progress log / Verification 章节的真实记录。
6. **收尾**：向用户交付验证入口（Release 链接/tag），等待用户在 Windows 环境手动验证
   exe 可运行后再讨论提交与合并时机。

## Change checklist

- [ ] `src/FlowTask.Desktop/FlowTask.Desktop.csproj`：新增 win-x64 单文件发布属性组
- [ ] `.github/workflows/release-windows.yml`：新增 Windows 构建 + Release 发布 workflow
- [ ] 本 SPEC 的 Progress log / Verification / Lessons learned 随实施推进更新
- [ ] `docs/specs/README.md`：登记本 SPEC 到索引表与新增 `packaging` area

## Progress log

### 2026-09-23

- Completed: 完成 Gate 2 必读文档阅读；创建 `feature/win-single-exe-packaging` worktree；
  与用户确认三项关键裁决（self-contained 单 exe / push tag 自动发布 Release / 版本号取自
  git tag）；写出本 SPEC `[DRAFT]` 初稿。用户反馈后二次修订：移除 README 内网说明相关范围
  （用户澄清该背景仅为后续可能需求，非本次交付要求）；通过 GitHub API 核实仓库为 public，
  确认 Actions 免费无额度顾虑，且触发条件为 tag push（与 main 的 PR/push 无关）。
- Decisions: 见上方 Constraints and decisions。
- Current resume point: 等待用户审核 Staged plan，确认后方可切换为 `[IN-PROGRESS]` 并开始
  Step 1（csproj 改造）。
- Subagent/task references: 无。

## Verification

- Automated: 未执行（`draft` 阶段不得改动代码）。
- Manual: 未执行。
- Not run or not covered: 全部验收项均待 Step 1-6 执行后填充真实结果，禁止预填。

## Risks and open questions

- Owner: aisdwf — Blocker: 本机 macOS 无法验证 win-x64 单文件产物的 native dll 收纳效果，
  必须依赖 GitHub Actions 的首次真实运行结果，可能出现"仍有 2-3 个 native dll 旁置"这类
  非完全单文件的情况，届时需要用户确认是否接受该折中结果。
（`workflow_dispatch` 是否发 Release、README 是否补充内网说明两项开放问题已在
2026-09-23 与用户确认后关闭，见 Constraints and decisions 与 Progress log。）

## Lessons learned

（`draft` 阶段无历史记录；本节留待实施与验证阶段回填真实发现。）

## Related documents

- SPECs: 无直接前置 SPEC（`packaging` 为新建 area）。
- ADRs: 无。
- Rules: `docs/rules/workflow-methodology.md`、`docs/rules/docs-conventions.md`、
  `docs/rules/commit-conventions.md`、`AI_CONSTITUTION.md` Article 6 / Article 9。
- Analysis: 无（如需要记录 GitHub Actions 单文件发布残留文件的调研细节，将新增
  `docs/analysis/` 报告并在此处补充链接）。

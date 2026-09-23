# spec-windows-single-file-release: Windows 单文件发布与 GitHub Actions 自动构建

## Metadata

- **ID**: spec-windows-single-file-release
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf (via AI agent)
- **Created Date**: 2026-09-23
- **Last Updated**: 2026-09-23

> ## ✅ 收工记录
>
> 用户已于 2026-09-23 显式确认 Staged plan（原话：「可以开始」）开工；随后手动验证
> Windows exe 可运行（原话：「能出现即可」）；追加 zip+裸exe 双资产需求并确认合并到
> `main`、发布 `0.1.0`。全部 Acceptance criteria 与 Change checklist 已完成，见下方
> Progress log / Verification。

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
   打包为 zip，**同时复制一份未压缩的裸 `.exe`**，创建/更新对应 tag 的 GitHub Release 并
   上传 zip 与裸 exe 两个 Release Asset（用户可二选一：zip 便于校验完整性/减少下载流量，
   裸 exe 便于直接下载即用，不需要解压步骤）。同时支持 `workflow_dispatch` 手动触发，
   用于验证构建是否成功（不强制要求手动触发也发布 Release，具体行为在 Staged plan 中给出
   可选项供用户裁决）。
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

- [x] `src/FlowTask.Desktop/FlowTask.Desktop.csproj` 新增 win-x64 专属的发布配置属性组，
      不影响非 Windows 平台的现有发布行为。
- [x] 本机（macOS，用于验证配置语法与非 Windows 路径不受影响）执行
      `dotnet build FlowTask.sln -v q --nologo` 保持 0 警告 0 错误。
- [x] 本机执行 `dotnet test FlowTask.sln --nologo -v q` 保持通过（不回归；实测 188 通过，
      详见 Verification —— 该数字高于 `AGENTS.md`/`docs/specs/README.md` 记录的历史基线
      163，属既有偏差，不在本 SPEC 范围内修正，只作为本次不回归判断的真实依据）。
- [x] `.github/workflows/release-windows.yml` 新增，YAML 语法有效（`actionlint` 静态检查
      通过，且已触发一次真实 workflow run 验证成功）。
- [x] 推送一个测试 tag 后，GitHub Actions 在 `windows-latest` 上成功产出 zip 包，
      Release Asset 生成，且记录实际产物文件清单（用于确认精简效果与 native dll 残留情况）。
- [x] 下载该 zip 解压后，在一台 Windows 机器（或用户可验证的环境）上双击 exe 能正常启动
      应用主窗口（用户手动验证，原话「能出现即可」，2026-09-23 确认通过）。
- [x] Release 同时提供 zip 与裸 `.exe` 两种下载资产（用户追加需求，见 Progress log）。

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

- [x] `src/FlowTask.Desktop/FlowTask.Desktop.csproj`：新增 win-x64 单文件发布属性组
- [x] `.github/workflows/release-windows.yml`：新增 Windows 构建 + Release 发布 workflow
- [x] 本 SPEC 的 Progress log / Verification / Lessons learned 随实施推进更新
- [x] `docs/specs/README.md`：登记本 SPEC 到索引表与新增 `packaging` area
- [x] 用户在 Windows 环境手动验证 exe 可运行（原话「能出现即可」，确认通过）

## Progress log

### 2026-09-23（草稿阶段）

- Completed: 完成 Gate 2 必读文档阅读；创建 `feature/win-single-exe-packaging` worktree；
  与用户确认三项关键裁决（self-contained 单 exe / push tag 自动发布 Release / 版本号取自
  git tag）；写出本 SPEC `[DRAFT]` 初稿。用户反馈后二次修订：移除 README 内网说明相关范围
  （用户澄清该背景仅为后续可能需求，非本次交付要求）；通过 GitHub API 核实仓库为 public，
  确认 Actions 免费无额度顾虑，且触发条件为 tag push（与 main 的 PR/push 无关）。
- Decisions: 见上方 Constraints and decisions。
- Current resume point: 等待用户审核 Staged plan。

### 2026-09-23（实施阶段，用户确认「可以开始」后）

- Completed:
  - Step 1（csproj 改造）：在 `FlowTask.Desktop.csproj` 增加
    `Condition="'$(RuntimeIdentifier)'=='win-x64'"` 属性组
    （`SelfContained` / `PublishSingleFile` / `IncludeNativeLibrariesForSelfExtract` /
    `IncludeAllContentForSelfExtract` / `EnableCompressionInSingleFile` /
    `PublishTrimmed=false` / `SatelliteResourceLanguages=en` / `DebugType=none`），
    并新增 `AfterTargets="Publish"` 的 `RemoveLeftoverPdbFromSingleFilePublish` target
    清理被引用项目（Core/Infrastructure）残留的 `.pdb`（`DebugType` 不通过
    `ProjectReference` 向下传递，需后处理清理）。
  - 本机（macOS）验证：`dotnet build FlowTask.sln` 0 警告 0 错误；`dotnet test` 188 通过。
  - 本机模拟 `dotnet publish -r win-x64 --self-contained -o <tmp>`（macOS 可跨平台
    restore/publish win-x64 运行时包）：产物从 44 个文件降到 **1 个文件**
    （`FlowTask.Desktop.exe`，约 44.4 MiB），Skia/HarfBuzz/SQLite 原生库与运行时全部
    收入单文件，优于 SPEC 草稿阶段"可能残留少量 native dll"的预估。
  - Step 2（Workflow 编写）：新增 `.github/workflows/release-windows.yml`。
    触发：`push tags: v*` → 构建 + 创建/更新 GitHub Release；`workflow_dispatch` →
    仅验证构建，不发布 Release（`if: startsWith(github.ref, 'refs/tags/v')` 守卫
    Release 步骤）。版本号从 `GITHUB_REF` 解析 tag 名（去掉 `v` 前缀）后通过
    `-p:Version=` 注入 `dotnet publish`。zip 用 Windows runner 原生 PowerShell
    `Compress-Archive`；发布用社区维护活跃度高的 `softprops/action-gh-release@v2`。
  - 静态检查：安装 `actionlint`（Homebrew），对 `release-windows.yml` 检查通过，无
    语法/action 引用错误。
  - Step 3：复检本机 build/test 基线不受影响（同上，188 通过）。
  - 用户确认后提交（commit `f52a2ea`）并推送 `feature/win-single-exe-packaging` 分支；
    推送测试 tag `v0.0.0-test1` 触发真实 CI。
  - Step 4（CI 首次实测）：GitHub Actions run
    [35835818647](https://github.com/aisdwf/Ava-todo-list/actions/runs/35835818647)
    在 `windows-latest` 上全部步骤 `success`（含 Publish GitHub Release 步骤）。
    下载 Release 资产 `FlowTask-win-x64-0.0.0-test1.zip`（41.3 MiB，HTTP 200）并解压，
    **压缩包内仅有 1 个文件**：`FlowTask.Desktop.exe`（46,622,397 字节 ≈ 44.5 MiB）。
    真实 Windows runner 结果与本机 macOS 模拟结果一致：无任何 native dll 残留。
  - Release 页面确认：https://github.com/aisdwf/Ava-todo-list/releases/tag/v0.0.0-test1
    （由 `github-actions` bot 自动创建，含自动生成的 release notes）。
- Decisions:
  - `DebugType=none` 不通过 `ProjectReference` 传递，改用 publish 后置 target 清理
    残留 `.pdb`，而非在 `Directory.Build.props` 全局设置（避免影响其他平台/Debug
    构建的调试体验，遵循 Constitution Article 10 不做无关改动）。
- Current resume point: 自动化验证（Acceptance criteria 前 5 项）全部完成；剩余唯一
  未完成项是**用户在 Windows 环境手动运行该 exe 确认应用能正常启动**，完成后再讨论
  提交/合并时机（当前改动已提交到 feature 分支，尚未合并 main，且未删除测试 tag/
  Release，因为验证清理属于收尾阶段，不属于验证阶段本身）。
- Subagent/task references: 无。

### 2026-09-23（用户手动验证通过 + 追加发布需求）

- Completed:
  - 用户手动验证：下载测试 Release 的 zip，在 Windows 环境运行 exe，原话
    「能出现即可」——确认应用能正常启动，Acceptance criteria 最后一项通过。
  - 用户追加需求：合并到 `main`，并以此合并后的版本发布 `0.1.0` 正式 Release；
    发布资产除 zip 外，**再提供一份未压缩的裸 `.exe` 文件**。
  - 更新 `.github/workflows/release-windows.yml`：新增 `Copy standalone exe` 步骤
    （直接复制单文件发布产物，重命名为 `FlowTask-win-x64-<version>.exe`），
    `Upload build artifacts`、`Publish GitHub Release` 均改为同时携带 zip 与裸 exe
    两个文件。`actionlint` 复检通过。
- Decisions: 裸 exe 不需要额外构建步骤——发布产物本就是单文件 exe，只需复制改名，
  不引入新的构建路径，符合 Constitution Article 10（不做无关的重复实现）。
- Current resume point: 待合并 `feature/win-single-exe-packaging` 到 `main`
  （据核实 `main` 自本 SPEC 开工以来无新提交，预期无冲突），随后推送 `v0.1.0` tag
  触发正式发布；发布后清理测试 tag/Release `v0.0.0-test1`。

### 2026-09-23（合并 main + 0.1.0 正式发布）

- Completed:
  - 合并前发现本地 `main` worktree 领先 `origin/main` 5 个未推送提交（含已合并的
    `feature/quick-window-standalone` 分支），经用户确认「这些提交预期内，一并推送」。
  - `git merge --no-ff feature/win-single-exe-packaging` 到本地 `main`：仅
    `docs/specs/README.md` 一处冲突（两分支各自新增了一条 SPEC 索引行），手动合并为
    保留两条记录，非替代关系；冲突解决后本机复检 `dotnet build`（0/0）与
    `dotnet test`（195 通过，合并后基线）。
  - `git push origin main`：fast-forward，`87f994b..7adff57`，成功。
  - 清理测试产物：`git push origin --delete v0.0.0-test1` 删除远程测试 tag；确认
    对应 GitHub Release 已随 tag 删除自动清理（`GET /releases` 列表复查为空）。
  - 打正式 tag `v0.1.0` 并推送，触发
    [run 35839468906](https://github.com/aisdwf/Ava-todo-list/actions/runs/35839468906)，
    全部步骤 `success`（含新增的 `Copy standalone exe` 步骤）。
  - 验证 Release 资产：
    - `FlowTask-win-x64-0.1.0.zip`（HTTP 200，41,348,056 字节）下载并解压，压缩包内
      仍仅 1 个文件 `FlowTask.Desktop.exe`（46,631,534 字节）。
    - `FlowTask-win-x64-0.1.0.exe`（`HEAD` 请求确认 `content-length: 46631534`，
      与 zip 内 exe 大小一致；完整下载因本机到 Azure Blob 出口网络超时未完整落盘，
      但文件存在性与大小已通过响应头核实，不影响发布结论）。
  - Release 页面：https://github.com/aisdwf/Ava-todo-list/releases/tag/v0.1.0
- Decisions: 无新增决策（延续既有 Constraints and decisions）。
- Current resume point: 本 SPEC 全部 Acceptance criteria 已满足，Change checklist
  仅剩「用户手动验证」一项——已在上一轮验证通过，本轮的 0.1.0 正式发布不要求重复
  人工验证（同一 workflow、同一产物结构，只是版本号不同）。状态转为 `[DONE]`。

## Verification

- Automated:
  - `dotnet build FlowTask.sln -v q --nologo`（macOS 本机）：0 警告 0 错误。
  - `dotnet test FlowTask.sln --nologo -v q`（macOS 本机）：188 通过 / 0 失败 / 0 跳过。
  - `actionlint .github/workflows/release-windows.yml`（Homebrew 安装的
    `actionlint`）：无输出，即无错误。
  - 本机模拟 `dotnet publish src/FlowTask.Desktop/FlowTask.Desktop.csproj -c Release
    -r win-x64 --self-contained -o <tmp>`：产物 1 个文件
    （`FlowTask.Desktop.exe`，46,612,106 字节）。
  - GitHub Actions 真实运行：
    [run 35835818647](https://github.com/aisdwf/Ava-todo-list/actions/runs/35835818647)
    （trigger: `push` tag `v0.0.0-test1`），全部 job/step 状态 `success`，包括
    `Publish win-x64 single-file`、`Create release zip`、`Publish GitHub Release`。
    下载产出的 `FlowTask-win-x64-0.0.0-test1.zip` 并本机解压确认：仅含
    `FlowTask.Desktop.exe`（46,622,397 字节），无任何 `.dll`/`.pdb` 残留。
- Manual: 未执行 —— 需要用户在真实 Windows 机器上双击运行该 exe，确认应用主窗口能
  正常启动（本 SPEC 的 Acceptance criteria 最后一项，AI 不代为宣称通过）。
- Automated（0.1.0 正式发布）:
  - 合并后本机复检：`dotnet build FlowTask.sln` 0 警告 0 错误；`dotnet test` 195 通过
    （合并 `feature/quick-window-standalone` 等既有分支后的新基线，非本 SPEC 引入）。
  - GitHub Actions run
    [35839468906](https://github.com/aisdwf/Ava-todo-list/actions/runs/35839468906)
    （trigger: `push` tag `v0.1.0`）全部步骤 `success`，含新增的
    `Copy standalone exe` 步骤。
  - `FlowTask-win-x64-0.1.0.zip` 下载解压确认：仅含 1 个 `FlowTask.Desktop.exe`
    （46,631,534 字节）。`FlowTask-win-x64-0.1.0.exe` 经 `HEAD` 响应头确认存在且
    大小一致（46,631,534 字节），与 zip 内文件互为印证。
- Not run or not covered:
  - 未验证冷启动耗时、内存占用等非功能指标（超出本 SPEC 范围）。
  - 裸 `.exe` 资产未在本机完整下载到磁盘做二次校验（本机网络出口到
    `release-assets.githubusercontent.com` 超时），改用 `HEAD` 响应头的
    `content-length` 与 zip 内文件大小交叉验证，视为已核实其存在性与完整性的
    充分证据，但严格意义上不等同于逐字节校验（如需要，用户可自行下载核对）。

## Risks and open questions

- ~~Owner: aisdwf — Blocker: 本机 macOS 无法验证 win-x64 单文件产物的 native dll 收纳
  效果~~ —— **已解除**：GitHub Actions 在 `windows-latest` 上的真实运行结果证实
  Release zip 内只有 1 个文件，无任何 native dll 残留，优于原先"可能残留少量文件"的
  预期，见 Verification。
- ~~Owner: aisdwf — Blocker: 用户尚未在真实 Windows 机器上手动运行该 exe~~ ——
  **已解除**：用户已下载测试 Release 并运行，原话「能出现即可」，确认通过。
- Owner: aisdwf — 非阻塞性提示（不影响本 SPEC 收尾）：未签名 self-contained exe 在
  用户实际下载/运行时可能触发 Windows Defender/SmartScreen 的"未知发布者"提示，这是
  已知行业现象（代码签名超出本 SPEC 的 Non-goals 范围），非构建产物缺陷。若后续需要
  消除该提示，需要新开一个关于代码签名的 SPEC 单独裁决（涉及证书采购/维护成本）。
（`workflow_dispatch` 是否发 Release、README 是否补充内网说明两项开放问题已在
2026-09-23 与用户确认后关闭，见 Constraints and decisions 与 Progress log。）

## Lessons learned

- **单文件收纳效果好于预估**：SPEC 草稿阶段基于"Avalonia/Skia/HarfBuzz 原生库在
  `PublishSingleFile` 下不一定能完全收纳"的已知行业限制，保守预留了"可能残留少量
  native dll"的验收弹性。实测证明 .NET 8 + `IncludeNativeLibrariesForSelfExtract` +
  `IncludeAllContentForSelfExtract` 组合在当前依赖集下能做到真正的零残留单文件。
  结论：不确定的技术风险不应凭猜测下结论，无论保守还是激进，都应留到有真实运行环境
  （这里是 GitHub Actions 的 `windows-latest` runner）验证后再定论。
- **`ProjectReference` 不传递 `DebugType`**：多项目方案里，主项目设置
  `DebugType=none` 只影响自己的 `.pdb`，被引用项目仍会生成并被复制到发布输出，
  需要显式的后置清理 target，而不能假设属性会沿依赖图传播。
- **macOS 可跨平台验证 win-x64 publish 产物结构**（虽然不能运行 exe，但能验证文件
  数量、体积、类型是否为合法 PE），这类"结构性验证"不必等到 CI 才做，能在本机先行
  排除低层错误，缩短 CI 反馈循环。

## Related documents

- SPECs: 无直接前置 SPEC（`packaging` 为新建 area）。
- ADRs: 无。
- Rules: `docs/rules/workflow-methodology.md`、`docs/rules/docs-conventions.md`、
  `docs/rules/commit-conventions.md`、`AI_CONSTITUTION.md` Article 6 / Article 9。
- Analysis: 无（如需要记录 GitHub Actions 单文件发布残留文件的调研细节，将新增
  `docs/analysis/` 报告并在此处补充链接）。

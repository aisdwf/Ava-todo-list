# SPEC: 文档体系重构（类型边界归位与编号清理）

## Metadata

- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-15
- **Last Updated**: 2026-09-15

> ## ⛔ 开工前置条件（rule-spec-review-gate）
>
> **本 SPEC 状态为 `draft`，未获用户显式确认前严禁执行任何文档搬迁或重命名。**
> 须先呈现 §3 执行计划并停下等待确认。
>
> 本 SPEC **不改动 `src/` 与 `tests/` 的业务逻辑**，
> 但**会修改代码注释中的文档引用**（74 处），属低风险但需构建验证的改动。

**上游依据**：
- 规则：[rule-doc-boundary](../../rules/rule-doc-boundary.md)（文档类型边界与命名规范）
- 用户明示：「其实包括 spec，我也没有做序号的要求，名字里面应该明确的是具体负责的内容，
  序号是完全冗余的内容。能删去最好，直接按功能重排」
  「可以走 spec，规范工作流也是工作的重要一步」

**本 SPEC 是后续所有开发的前置** —— 用户明确要求
「内容我只能说在规定好工作流再开始，不然无限的反工足够拖死我了」。

---

## 0. 新会话接手须知

### 0.1 必读

1. `AI_CONSTITUTION.md`
2. [`docs/rules/README.md`](../../rules/README.md) 防呆清单
3. [rule-spec-review-gate](../../rules/rule-spec-review-gate.md)：SPEC 审核门禁
4. [rule-doc-boundary](../../rules/rule-doc-boundary.md)：**本 SPEC 的直接依据**
5. 本文件

### 0.2 项目状态

- git 基线：`a7f9a47`（62 文件）。`docs/` 与 `res/` 被 `.gitignore` 排除
- 构建：0 警告 0 错误；测试：**163 通过**（不得退化）
- 构建命令：
  ```bash
  export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
  dotnet build FlowTask.sln -v q --nologo && dotnet test FlowTask.sln --nologo -v q
  ```

### 0.3 已核实的改动规模

| 范围 | 数量 |
| :--- | :--- |
| `docs/` 内编号引用 | **360 处** |
| `src/` `tests/` 代码注释内编号引用 | **74 处** |
| 需重命名的文档 | 15 份（3 SPEC 带 `[DONE]` 后缀 + 5 SPEC + 5 DESIGN + 4 RULE + 1 ADR，见 §2.3） |
| 需拆分的 design | 3 份（design-domain-contract / 0004 / 0005） |

---

## 1. Why

### 1.1 design 被误用为 spec

`docs/README.md:24` 定义 `design/` 为「概念模型与边界草案」，
`ai-workflow/` 六篇方法论中 "design" 仅作为 commit 归因分类出现过一次 ——
**从未被定义为带状态机的文档类型**。`docs/templates/` 下也无 design 模板。

但 design-domain-contract / 0004 / 0005 中写入了：
自造的 4 种状态标记、带行号的现状代码诊断、分阶段实施路径、
待裁决问题清单、契约变更影响面 —— **全部属 SPEC 范畴**。

**用户实际后果**：查一件事要读四个地方，且状态互相矛盾
（design-domain-contract 一半有效一半失效，spec-classification-ui 却标 `done`）。

用户原话：「design 不应该是具体的风格约束等内容吗？
具体落地做了什么，还是应该在 spec 体现，而不是 design。
分开来管理，那么找的时候到底读哪里？看状态能一目了然吗？」

### 1.2 序号是冗余信息

用户明示序号「完全冗余」。已实际造成的代价：

- 434 处引用需维护；
- 编号不携带语义，`spec-task-contract-and-clock` 必须再查索引才知道讲什么；
- 制造虚假顺序感 —— `design-interaction-principles` 看似 `0004` 的续作，实为对其推翻；
- 阻碍按功能重组文档。

**根因**：违背 Article 5（命名反映身份而非历史）。
四位递增序号与被明令禁止的 `v2` / `new` / `old` 属同类问题。

### 1.3 文件名携带状态

`spec-fluent-ui[SUPERSEDED].md` 的实际状态是 `superseded`，
文件名却停留在 `[DONE]` —— 状态一变就得改名，改名就破坏所有引用。

**Attribution**: `Design Wrong` —— 文档体系的类型边界与命名规范
从未被明确定义，导致执行者按惯性套用 SPEC 模板。

---

## 2. What

### 2.1 目标

1. 建立 design / spec 的清晰边界（规则已立，本 SPEC 执行归位）
2. 全库移除文档编号，改为功能命名
3. 移除文件名中的状态标记
4. 拆分承载多主题的 design
5. 同步全部引用（含代码注释）

### 2.2 design 拆分方案

**[推断]** 依 rule-doc-boundary §2.2 的判据（会随进度变化的属 SPEC，
实施前后不变的属 design）拆分：

| 现有文档 | design 保留部分 | 移入 spec 的部分 |
| :--- | :--- | :--- |
| design-visual-language | 配色/圆角/间距/字号规范（§2） | §3 功能边界表（已被实现取代，移入归档） |
| design-visual-language | Editorial 排版原则、水波纹动效意图 | 无 |
| design-domain-contract | **无**（整体已被取代） | §11.1 技术缺陷清单 → 已由 spec 承接，本体移入 `archived/` |
| design-domain-contract | §2 数据契约（项目实体、标签存储、`DueDate` 时区语义） | §3.2/§4.2/§5 交互决策（已证伪）→ 移入归档并留重定向 |
| design-interaction-principles | §2~§6 的交互**原则**（常驻配置、零手动输入、合理默认态、上下文继承） | §1 现状诊断、§7 结构整改、§9 待裁决、§10 实施路径 → 移入对应 SPEC |

**[推断]** 拆分后的 design 文档（功能命名）：

| 新文件名 | 内容 | 来源 |
| :--- | :--- | :--- |
| `design-visual-language.md` | 配色、圆角、间距、字号、Editorial 排版、动效意图 | design-visual-language + design-visual-language |
| `design-domain-contract.md` | 任务/项目/标签的字段语义、时区规则、删除语义 | design-domain-contract §2 |
| `design-interaction-principles.md` | 零手动输入、常驻配置、合理默认态、上下文继承、渐进披露 | design-main-window-rework-superseded §2~§6 的原则部分 |

**需评审确认**：以上三分法是否合理，尤其「数据契约」归 design 是否符合你的预期
（用户已答「a. 可以写入 rule」，此处按「写入 design 层的约束文档」理解，
若你的意思是写入 `rules/`，则改为 `rule-domain-contract.md`）。

### 2.3 重命名清单

| 现名 | 新名 |
| :--- | :--- |
| `spec-mvvm-infrastructure[DONE].md` | `spec-mvvm-infrastructure[DONE].md` |
| `spec-fluent-ui[SUPERSEDED].md` | `spec-fluent-ui[SUPERSEDED].md` |
| `spec-editorial-and-ripple-theme[DONE].md` | `spec-editorial-and-ripple-theme[DONE].md` |
| `spec-task-contract-and-clock[DONE].md` | `spec-task-contract-and-clock[DONE].md` |
| `spec-classification-ui[DONE].md` | `spec-classification-ui[DONE].md` |
| `spec-sidebar-selection-consolidation[DONE].md` | `spec-sidebar-selection-consolidation[DONE].md` |
| `spec-tag-entity[DONE].md` | `spec-tag-entity[DONE].md` |
| `rule-code-standards.md` | `rule-code-standards.md` |
| `rule-spec-review-gate.md` | `rule-spec-review-gate.md` |
| `rule-no-invented-user-behavior.md` | `rule-no-invented-user-behavior.md` |
| `rule-doc-boundary.md` | `rule-doc-boundary.md` |
| `adr-technology-stack.md` | `adr-technology-stack.md` |
| design-visual-language / 0002 | → 合并为 `design-visual-language.md` |
| design-domain-contract | → `archived/design-task-domain-superseded.md` |
| design-domain-contract | → 拆为 `design-domain-contract.md` + 归档部分 |
| design-interaction-principles | → 拆为 `design-interaction-principles.md` + 移入 SPEC 部分 |

### 2.4 非目标

| 不做 | 理由 |
| :--- | :--- |
| 任何业务代码逻辑改动 | 本 SPEC 只改文档与代码**注释**中的引用 |
| 修改 REQUIREMENTS 的内容 | 需求本身不受文档形式影响（仅同步引用） |
| 修改 ai-workflow 六篇方法论 | 它们是外部引入的体系文档，非本项目产出 |
| 新增 pre-commit 钩子 | rule-doc-boundary §4 已记录建议，独立处理 |
| 推进 design-interaction-principles 的待裁决问题 | 属产品决策，与文档重构正交 |

---

## 3. 分阶段实施计划（须先呈现确认）

- [ ] **Phase 1: 建立新的 design 文档**
  - [ ] 创建 `design-visual-language.md`（合并 design-visual-language + design-visual-language）
  - [ ] 创建 `design-domain-contract.md`（承接 design-domain-contract §2）
  - [ ] 创建 `design-interaction-principles.md`（承接 design-interaction-principles 原则部分）
  - [ ] 三者均**不含**复选框、Phase、验证记录
- [x] **Phase 2: 移交实施内容至 SPEC**
  - [x] 现状诊断 / 待裁决 / 实施路径已在 `spec-sidebar-selection-consolidation[DONE].md`
        与 `spec-tag-entity[DONE].md` 中（此二 SPEC 先于本次重构建立，已含相应内容）
  - [x] 原 DESIGN-0005 完整保留于 `archived/`，其重定向表逐节标明去向，无内容丢失
- [ ] **Phase 3: 归档旧 design**
  - [ ] design-domain-contract 整体移入 `archived/`，头部加重定向（Article 8）
  - [ ] design-domain-contract 已证伪部分移入 `archived/`，加重定向
  - [ ] design-visual-language 功能边界表移入 `archived/`
- [x] **Phase 4: 全库重命名与引用同步**
  - [x] 重命名 12 份文档（7 SPEC + 4 RULE + 1 ADR），移除序号与 `[DONE]` 状态标记
  - [x] 同步 `docs/` 内引用（25 个文件）
  - [x] 同步 `src/` `tests/` 内代码注释引用（**29 个代码文件**）
  - [x] 修正因批量替换而失真的章节号引用（14 个文件）
  - [x] 重写 `docs/README.md`、`design/README.md`、`specs/README.md` 索引
- [x] **Phase 5: 验证**
  - [x] rule-doc-boundary §4 三条检查全部通过
  - [x] 死链检查：**154 条相对链接，0 死链**
  - [x] `dotnet build` 0 警告 0 错误；`dotnet test` **163 通过**
  - [x] 按 Article 1 提交

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| — | — | 暂未派发。Phase 4 的引用同步涉及大量文件，可考虑派发 | — | — |

### 关键决策与状态增量

- **[2026-09-15]** SPEC 建立，状态 `draft`，**等待用户审核**。
- **[2026-09-15]** 已核实改动规模：docs 360 处 + 代码 74 处编号引用。
- **[2026-09-15]** 用户裁决三项：
  1. 数据契约「可以写入 rule」；
  2. **序号完全冗余，按功能重排**；
  3. 本次重构走 SPEC 流程。

<!-- 以下按 Phase 实际完成情况追加，严禁提前填写（rule-spec-review-gate §2.3）-->

### Phase 执行记录

- **[执行前]** 已将 `docs/` 完整备份至临时目录（41 篇）——
  `docs/` 被 `.gitignore` 排除，git 无法恢复，故必须先备份。
- **[Phase 1]** 三份新 design 按「正交维度」划分：
  视觉语言（看起来什么样）/ 领域契约（数据是什么）/ 交互原则（怎么操作）。
  三者互不重叠，查任一主题只需读一处 —— 这直接回应用户
  「分开来管理，那么找的时候到底读哪里」的质疑。
- **[Phase 3]** 原 DESIGN-0003 / 0004 归档时注明为**反面案例**而非单纯过时：
  前者循环论证、后者自造用户行为假设，保留其归档原因有助后续规避。
- **[Phase 4 · 批量替换的反噬]** 用脚本做全库编号替换时，
  **规则文档自身的「反面示例」被一并替换**，导致
  `rule-doc-boundary` 的「❌ 错误命名示例」显示为正确的新命名 ——
  规则自己打了自己的脸。已修正。
  **教训**：批量文本替换会命中「刻意书写旧形式」的位置
  （示例、反面教材、历史记录），执行后必须回读被改动的规则与文档本身。
- **[Phase 4 · 章节号失真]** 批量替换把「DESIGN-0004 §2.1」变成
  「design-domain-contract §2.1」，但新文档的 §2.1 已是完全不同的内容 ——
  **链接对了、章节号错了**。已逐一修正 14 个文件。
  **教训**：文档合并重排时，跨文档的章节号引用必然失效，
  不能只改文件名不改章节号。
- **[Phase 5 · 规则自身的例外]** 检查 3 命中了索引中的「原 DESIGN-0001」映射表。
  该表的目的是让记得旧编号的人找到新文档，属**记录历史事实**而非指代文档。
  已在 `rule-doc-boundary` §3.1 补充此例外，并要求以「原 XXX」形式书写以便机器排除。

---

## 5. 验证记录

### 机器门禁

- [x] rule-doc-boundary §4 检查 1（文件名无序号/状态）→ **PASS**
- [x] rule-doc-boundary §4 检查 2（design 无 SPEC 结构）→ **PASS**
- [x] rule-doc-boundary §4 检查 3（无裸编号引用）→ **PASS**（仅保留映射表与规则示例）
- [x] 死链检查：**154 条链接，0 死链**
- [x] 构建：**0 警告 0 错误**
- [x] 测试：**163 通过 / 0 失败**（与重构前一致，未退化）

### 人工验证（由用户执行）

- [ ] 打开 `docs/README.md` → 能一眼看出每份文档负责什么
- [ ] 查「标签怎么存」→ 只需读一处
- [ ] 查「某功能做到哪了」→ 只需读对应 spec，状态一目了然
- [ ] design 目录下无任何「做到哪了」的进度信息

---

## 6. Deferred Items

承接自前序 SPEC，本段不处理：

- 项目归档无 UI 入口
- `Description` 死字段（已决定废弃，待确认是否物理删列）
- 软删除任务无恢复入口
- 外观偏好未持久化
- `Class1.cs` 模板残留
- rule-doc-boundary 的 `pre-commit` 钩子

---

## 7. Commit Attribution 与经验教训

- **Attribution**: `Design Wrong` —— 文档体系的类型边界与命名规范从未定义，
  导致执行者按惯性套用 SPEC 模板，产出了 5 份职责混杂的 design。
- **Root Cause**: 见 §1。深层原因是**在没有规范的地方按惯性行事，
  且未回查 docs 自身的定义**（`docs/README.md:24` 早已写明 design 的定位）。
- **Lessons Learned**: 见 §8。

---

## 8. Lessons Learned

1. **批量文本替换会命中「刻意书写旧形式」的位置。**
   本次替换把 `rule-doc-boundary` 自己的「❌ 错误命名示例」也改成了新命名，
   使规则展示出自相矛盾的内容。示例、反面教材、历史记录这三类文本
   天然需要保留「错误」形式，批量操作后**必须回读规则与文档自身**。

2. **文档合并重排会使跨文档章节号引用静默失效。**
   「DESIGN-0004 §2.1」替换为「design-domain-contract §2.1」后，
   **链接是对的、章节号是错的** —— 新文档的 §2.1 已是完全不同的内容。
   这类错误不会被死链检查发现（文件确实存在），只能靠逐条核对。
   **推论**：重排文档时应尽量减少对具体章节号的引用，
   改为引用小节标题或直接描述内容。

3. **规则需要为自己的边界情形留出例外，且例外必须机器可辨识。**
   「无裸编号引用」这条检查命中了索引里的历史映射表 ——
   而该表的存在正是为了帮助读者从旧编号找到新文档。
   解法不是放弃检查，而是**规定例外的书写形式**（统一为「原 XXX」），
   使机器能可靠地排除它。

4. **文档类型边界必须在体系建立时就定义，否则执行者会按惯性套用现成模板。**
   本项目有 spec / rule / adr / troubleshooting 四个模板，
   独缺 design 模板 —— 结果 design 被按 SPEC 的样子写成了带状态机、
   带分阶段计划、带待裁决清单的四不像。
   **缺少规范的地方，惯性会自动填充，且填的往往是错的。**

5. **序号是历史信息，不是身份信息。**
   四位递增编号与 Article 5 明令禁止的 `v2` / `new` / `old` 同属一类问题。
   它不携带语义（`SPEC-0004` 必须查索引才知道讲什么）、
   制造虚假顺序感（`DESIGN-0005` 看似 `0004` 的续作实为推翻）、
   且阻碍按功能重组。改为功能命名后，文件名本身就是索引。

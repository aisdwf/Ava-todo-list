# rule-doc-boundary: 文档类型边界与命名规范

> **权威分工**：design vs spec 的类型边界判据仍以本文件为唯一权威。
> SPEC 的目录结构（area 子目录）、文件名细则、状态机与新鲜度阈值，
> 权威定义已上移至 [`docs/rules/docs-conventions.md`](./docs-conventions.md)，
> 本文件不再重复维护这部分机制，只保留类型边界与命名的判据与事故记录。

## Metadata

- **Category**: process
- **Severity**: BLOCK (Hard Error)
- **Status**: active
- **Created Date**: 2026-09-15
- **Related Incident**: design-domain-contract / 0004 / 0005 误用 SPEC 形式；全库 434 处冗余编号引用

---

## 1. Why（付出的真实代价）

### 1.1 事故一：design 被写成了 spec

`docs/README.md:24` 对 `design/` 的定位是
**「系统设计、概念模型与领域驱动设计边界草案」，维护模式「方案前期草案」**。

而 `docs/ai-workflow/` 六篇方法论中，**"design" 仅出现过一次**，
且是作为 commit 归因分类 `design wrong` 出现 ——
**workflow 从未把 design 定义为带状态机的文档类型**。
7 种状态、`draft`→`in-progress`→`done`、索引门禁，全部是 SPEC 独有的机制。
`docs/templates/` 下也**没有 design 模板**，这本身就是信号。

但实际写出的 design-domain-contract / 0004 / 0005 中：

- 自造了 4 种状态标记：`Approved` / `Blocked` / `Partially Superseded` / `Draft`
  —— 全部不在 workflow 定义内，也无任何文档规定 design 允许有哪些状态；
- 写入了大量**实施内容**：带行号的现状代码诊断、分阶段实施路径拆分、
  待裁决问题清单、契约变更影响面 —— 这些全属 SPEC 范畴。

**用户的实际后果**：查「标签怎么存」读 design-domain-contract，
查「标签怎么改的」读 spec-classification-ui，查「标签以后怎么办」读 design-interaction-principles + spec-tag-entity。
**四处拼一件事，且状态互相矛盾**（design-domain-contract 一半有效一半失效，
spec-classification-ui 却标 `done`）。

用户评价：「分开来管理，那么找的时候到底读哪里？看状态能一目了然吗？」

### 1.2 事故二：序号是冗余信息

用户明示：「其实包括 spec，我也没有做序号的要求，
名字里面应该明确的是具体负责的内容，序号是完全冗余的内容。」

冗余的代价已经实际发生：

- 全库 **434 处**编号引用（`docs/` 360 处 + 代码注释 74 处）；
- 编号不携带任何语义 —— `spec-task-contract-and-clock` 无法告诉读者它讲什么，
  必须再查索引表才知道是「数据契约与时钟」；
- 编号制造虚假的顺序感 —— `design-interaction-principles` 看似是 `0004` 的续作，
  实际是对其交互决策的推翻；
- 编号一旦被引用就难以调整，导致文档无法按功能重组
  （如本轮需要拆分重排时，编号成为阻力而非帮助）。

### 1.3 根因

**违背了 Article 5（命名反映身份，而非历史）。**
序号是「创建顺序」这一历史信息，而非文档的身份。
Article 5 明令禁止 `v2` / `new` / `old` 这类时序命名，
四位递增序号是同一类问题的另一种形态。

---

## 2. Mandates：文档类型边界

### 2.1 design 与 spec 的职责划分

| 维度 | `design/` | `specs/` |
| :--- | :--- | :--- |
| **回答** | 是什么、什么风格、什么约束 | 做什么、怎么做、做到哪了 |
| **内容** | 视觉规范、交互原则、领域概念边界、数据契约、命名与结构约定 | 现状诊断、分阶段清单、执行记录、验证记录、Subagent 日志 |
| **状态** | **仅两态**：`active` / `superseded`（见 §2.3） | workflow 定义的 7 种状态 |
| **生命周期** | 长期有效，被反复引用，像宪法一样约束后续所有实现 | 任务闭环即归档 |
| **复选框** | **严禁** | 必需 |
| **分阶段计划** | **严禁** | 必需 |
| **带行号的代码诊断** | **严禁** | 必需 |

### 2.2 强制规则

1. **严禁**在 `design/` 文档中出现：分阶段实施计划、带状态复选框的清单、
   Subagent 执行记录、验证记录、待裁决问题清单。
   以上全部属 SPEC，应写入对应 SPEC。
2. **严禁**在 `design/` 文档中出现 SPEC 的 7 种状态标记。
3. **严禁**为 design 自造状态词汇。合法状态仅 §2.3 两种。
4. design 描述的是**约束与原则**；一旦开始描述「第一步做什么、第二步做什么」，
   即已越界，**必须**移入 SPEC。
5. **判据**：若某段内容会随着实施进度而变化（勾选、打钩、记录结果），
   它属 SPEC；若它在实施前后都不变（配色、间距、字段语义、交互原则），
   它属 design。

### 2.3 design 的合法状态

| 状态 | 含义 | 要求 |
| :--- | :--- | :--- |
| `active` | 当前有效的约束 | 无 |
| `superseded` | 已被新 design 取代 | **必须**在文档头部标注指向新文档的重定向（Article 8） |

**严禁**「部分有效」这类状态。若一份 design 中部分内容失效，
说明它承载了多个不相关的主题 —— **必须拆分**为多份，
各自独立标注状态，而非用一个模糊状态掩盖（Article 10）。

---

## 3. Mandates：命名规范

### 3.1 严禁序号

6. **严禁**在文档文件名中使用递增序号。
7. 文件名**必须**由「类型前缀 + 功能描述」构成，功能描述用小写连字符。
   **SPEC 另须追加生命周期状态后缀**（见 §3.1.8）。

   ```
   ✅ spec-task-contract-and-clock[DONE].md
   ✅ design-visual-language.md
   ✅ rule-spec-review-gate.md

   ❌ SPEC-0004-task-contract-and-clock[DONE].md  ← 序号冗余（状态后缀本身合法）
   ❌ DESIGN-0002-bold-editorial-ui.md            ← 序号冗余
   ❌ design-visual-language[DONE].md             ← design 严禁状态入名
   ❌ spec-v2-tags[DONE].md                       ← 时序命名，违反 Article 5
   ❌ spec-task-contract-and-clock[DONE].md              ← SPEC 缺少状态后缀
   ```

8. **状态入名：按文档类型分流（2026-09-15 用户裁决）**

   | 类型 | 文件名是否携带状态 | 理由 |
   | :--- | :--- | :--- |
   | `specs/` | **必须** | SPEC 是伴随工作实时更新的工作记忆；目录一览即可判断生命周期，减少无谓打开 |
   | `design/` / `rules/` / `adr/` | **严禁** | 防过期状态污染；这些文档不以七态工作流驱动日常读写 |

   **SPEC 合法格式**：`spec-<功能描述>[STATUS].md`

   - `STATUS` 必须是七态之一的**全大写**形式：
     `DRAFT` / `IN-PROGRESS` / `DONE` / `DONE-REFACTORED` /
     `SUPERSEDED` / `ARCHIVED` / `OBSOLETE`
   - 文件名中的 `[STATUS]`、文档内 Metadata 的 `Status`、以及
     `docs/specs/README.md` 索引表**三者必须同轮一致**
   - 状态变更 = **同轮**完成：改 Metadata → 改文件名 → 更新全部相对路径引用 →
     更新索引表。漏改任一项即违规

   **反模式（仍禁止）**：文件名状态与 Metadata 漂移。
   例：`spec-fluent-ui[DONE].md` 而 Metadata 已是 `superseded` ——
   根因不是「不该入名」，而是「改状态时没有同步改名」。

9. 文档间的相互引用**必须**使用相对路径链接，
   **严禁**以裸编号（如「见 SPEC-0004」这类四位数字编号）指代文档 ——
   裸编号不可点击、不携带语义、且重命名后无法被机器发现失效。

   **唯一例外：历史映射表**。索引文档中为帮助读者从旧编号定位新文档而列出的
   对照表（形如「原 DESIGN-0001 → design-visual-language」）允许保留旧编号，
   因其目的是**记录改名这一历史事实**，而非指代文档。
   此类引用必须以「原 XXX」形式出现在明确的映射语境内。

### 3.2 唯一性由「功能描述」保证

唯一性看功能描述段（`spec-` 与 `[STATUS]` 之间的部分），不看状态后缀。
若两份文档功能描述重复，说明它们该合并（Article 10：一个概念一处定义）。
同一功能在生命周期中只应存在一份物理文件；状态变化通过**改名**表达，
不得复制出 `spec-foo[DONE].md` 与 `spec-foo[SUPERSEDED].md` 并存。

---

## 4. 机器检查

```bash
# 1a. 检查是否含递增序号（全 docs）
find docs -name '*.md' | grep -E '(SPEC|DESIGN|RULE|ADR)-[0-9]{3,}'

# 1b. design / rules / adr 严禁状态入名
find docs/design docs/rules docs/adr -name '*\[*\]*.md' 2>/dev/null

# 1c. specs 必须带合法大写状态后缀；且不得缺少后缀（含 area 子目录）
find docs/specs -name 'spec-*.md' ! -name 'README.md' \
  | grep -vE '\[(DRAFT|IN-PROGRESS|DONE|DONE-REFACTORED|SUPERSEDED|ARCHIVED|OBSOLETE)\]\.md$'

# 1d. specs 下的 area 子目录名严禁携带状态/日期/负责人（见 docs-conventions.md）
find docs/specs -maxdepth 1 -type d ! -path docs/specs \
  | grep -iE 'draft|progress|done|archiv|obsolete|[0-9]{4}-[0-9]{2}|aisdwf'

# 2. 检查 design 中是否混入 SPEC 专属结构
grep -ln '^- \[[ x]\]' docs/design/*.md          # 复选框
grep -ln 'Phase [0-9]\|Subagent\|验证记录' docs/design/*.md

# 3. 检查是否残留裸编号引用（排除 §3.1 例外允许的历史映射表）
grep -rn 'SPEC-[0-9]\{4\}\|DESIGN-[0-9]\{4\}\|RULE-[0-9]\{4\}\|ADR-[0-9]\{4\}' \
  docs src tests --include='*.md' --include='*.cs' --include='*.axaml' \
  | grep -v '/obj/' | grep -v '原 \(SPEC\|DESIGN\|RULE\|ADR\)-'
```

以上命令**均应无输出**。任一有输出即为违规。

> **诚实声明**：这些检查目前需手动执行，尚无 `pre-commit` 钩子。
> 项目已有 git 基线，具备加装条件，建议随门禁建设一并落地。

---

## 5. 例外

无例外。历史遗留的编号命名文档**必须**完成重命名；
既有无状态后缀的 SPEC **必须**补齐 `[STATUS]` 并同步引用，
不得以「改动量大」为由保留。

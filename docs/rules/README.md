# Rules Repository (项目规则库)

本目录分两层：**机器权威规则卡**（英文，AI 会话行动的唯一依据）
与**事故驱动的防御型规则**（中文，记录真实代价，机制条款已上移至权威规则卡）。

> **为什么分两层**：此前的方法论文档（`docs/ai-workflow/`）只讲道理，
> 缺少可被 AI/脚本直接执行的权威定义，导致 SPEC 命名、状态机、审核门禁等
> 关键机制曾多次被跳过或用错格式。现在机制条款收敛到本目录的英文规则卡，
> 事故记录保留在中文规则文件中承载「为什么」。

---

## 机器权威规则卡（Authoritative Rule Cards，英文）

AI 会话开始任务前必须先读这四份（见 `AGENTS.md` Gate 2）：

| 规则卡 | 权威范围 |
| :--- | :--- |
| [workflow-methodology.md](./workflow-methodology.md) | 任务分类、Complex Step 0-6、执行节奏硬规则、错误处理对照表 |
| [docs-conventions.md](./docs-conventions.md) | SPEC 目录（area 子目录）/命名/状态机/新鲜度阈值的唯一权威 |
| [commit-conventions.md](./commit-conventions.md) | `Why:`/`What:` 强制结构、归因分类、`TEMP_PATCH` |
| [project-rules.md](./project-rules.md) | FlowTask 项目专属业务/技术/架构规则（宪法 Article 7） |
| [technical-rules.md](./technical-rules.md) | 强制性技术/架构分解规则（如 ViewModel 命令拆分），来自结构性代码质量调研 |

---

## 任何改动前30秒自查：防呆清单 (Pre-Flight Checklist)

在动手修改代码前的 30 秒内，必须逐条对照自查：

0. **SPEC 已获用户确认？**（[rule-spec-review-gate](./rule-spec-review-gate.md)，机制见 `workflow-methodology.md` Step 1-2）
   SPEC 仍为 `draft` 时严禁改动 `src/` 与 `tests/`。写完 SPEC 必须停下等审核，
   严禁在同一轮回复中「写完 SPEC 并开始编码」。此条已两次失效，代价是整轮返工。
1. **验证后断言**：先跑通验证手段并得到真实输出，在测试中建立明确断言，严禁凭假设"觉得改对了"。
   严禁预填未执行的测试结果或未完成的勾选。
2. **修根因层，而非症状层**：追溯源头状态、生命周期或数据源根治，严禁在报错处加空判断或 `try-catch` 掩盖症状。
3. **禁止静默三件套**：严禁空 catch 吞异常、默认值兜底掩盖错误、静默重定向无 trace，保留上下文与错误事实。
4. **搬移禁止重写**：重命名或迁移模块时纯搬移，严禁在一次变更中同时做"搬迁 + 重构"，物理分步。
5. **新命令/新映射先查权威定义**：引入新依赖 API、新配置项或字段映射前查权威源或官方文档，严禁概率臆造。
6. **交互决策有用户依据？**（[rule-no-invented-user-behavior](./rule-no-invented-user-behavior.md)）
   「用户通常会…」「这更直觉」等句式必须附用户原话或观察记录，否则标注 `[推断]` 并列为待裁决。
   录入方式（如日期怎么填、标签怎么选）属设计决策，严禁当作实现细节推迟。
7. **写对文档类型了吗？**（[rule-doc-boundary](./rule-doc-boundary.md)）
   会随进度变化的（勾选、记录、结果）→ `specs/`；实施前后都不变的
   （配色、字段语义、交互原则）→ `design/`。
   design 中严禁出现复选框、Phase、验证记录。文件名严禁含序号；design/rules/adr 严禁状态入名，SPEC 必须 `[STATUS]` 大写后缀，且置于对应 `docs/specs/<area>/` 子目录。
8. **一个提交只做一件事？**（[commit-conventions.md](./commit-conventions.md)）
   `Why:`/`What:` 缺一不可；不相关改动不得塞入同一提交。

---

## 规则分类索引

<!--
分类子目录（process/ architecture/ incidents/）此前以链接形式列出，
但从未创建，构成死链。按 Article 8 改为「规划中」说明，
待某一分类实际产生第 2 条规则时再建目录并恢复链接。
-->

事故驱动的防御型规则平铺于本目录，规模尚未到需要分目录的程度：

| 规则 | 分类 | 严重级 | 主题 |
| :--- | :--- | :--- | :--- |
| [rule-code-standards](./rule-code-standards.md) | architecture / process | BLOCK | C# 12 / Avalonia 11 编码与注释规范 |
| [rule-spec-review-gate](./rule-spec-review-gate.md) | process | BLOCK | SPEC 必须经用户审核方可开工；严禁预填未发生的事实（机制已上移至 `workflow-methodology.md`） |
| [rule-no-invented-user-behavior](./rule-no-invented-user-behavior.md) | process | BLOCK | 交互设计严禁凭推理产出用户行为假设 |
| [rule-doc-boundary](./rule-doc-boundary.md) | process | BLOCK | 文档类型边界（design vs spec）与命名规范（机制已上移至 `docs-conventions.md`） |

**规划中的分类**（目录待实际需要时创建，当前不存在）：

- `process/`：约束任务分类、六步法、提交规范、SPEC 与文档管理。
- `architecture/`：约束模块分层、依赖单向性、数据流与配置权威源。
- `incidents/`：记录生产故障、严重返工后的武装化规则。

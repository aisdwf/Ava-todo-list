# 概念性设计文档（Design）

本目录维护**长期有效的设计约束**：风格规范、领域契约、交互原则。

---

## 一、本目录的职责边界

依 [rule-doc-boundary](../rules/rule-doc-boundary.md)：

| | `design/`（本目录） | `specs/` |
| :--- | :--- | :--- |
| **回答** | 是什么、什么风格、什么约束 | 做什么、怎么做、做到哪了 |
| **状态** | **仅两态**：`active` / `superseded` | 7 种状态的完整生命周期 |
| **生命周期** | 长期有效，被反复引用 | 任务闭环即归档 |
| **复选框 / 分阶段计划** | **严禁** | 必需 |

**判据**：会随实施进度变化的内容（勾选、记录、结果）→ `specs/`；
实施前后都不变的内容（配色、字段语义、交互原则）→ 本目录。

---

## 二、现有设计

| 文档 | 主题 | 状态 |
| :--- | :--- | :--- |
| [design-visual-language](./design-visual-language.md) | 视觉语言：Editorial 基调、色彩令牌、形状间距、材质、动效 | `active` |
| [design-domain-contract](./design-domain-contract.md) | 领域契约：任务/项目/标签字段语义、时间语义、校验规则、存储约束 | `active` |
| [design-interaction-principles](./design-interaction-principles.md) | 交互原则：零手动输入、常驻配置、上下文继承、就地编辑、双窗口分工 | `active` |

三份文档覆盖「看起来什么样」「数据是什么」「怎么操作」三个正交维度，
互不重叠。查任一主题只需读一处。

---

## 三、需求先于设计

本目录全部文档必须可追溯至
[requirements](../requirements/REQUIREMENTS.md) 中的具体需求，
或用户的明确表述。

**此约束源于两次真实事故**：

1. 早期设计在需求缺位时撰写，以项目自述文件的宣传语作为裁决依据（循环论证）；
2. 修正后仍由撰写者**自行推理用户行为习惯**（如「用户会先记下、整理时再归类」），
   交付后被实测推翻，导致整轮返工。

两者同属把主观判断当事实依据。已沉淀为
[rule-no-invented-user-behavior](../rules/rule-no-invented-user-behavior.md)：
交互决策必须来自用户明示或实际观察，撰写者的补充一律标注 `[推断]`。

---

## 四、已归档的历史设计

以下文档已被上述三份取代，**严禁作为实现依据**。
保留于 [`archived/`](../archived/) 仅为追溯设计沿革，均含重定向说明。

| 归档文档 | 历史编号 | 归档原因 |
| :--- | :--- | :--- |
| [design-visual-ux-overhaul-superseded](../archived/design-visual-ux-overhaul-superseded.md) | 原 DESIGN-0001 | 视觉部分已并入 design-visual-language；功能边界部分被实现取代 |
| [design-bold-editorial-superseded](../archived/design-bold-editorial-superseded.md) | 原 DESIGN-0002 | 已并入 design-visual-language |
| [design-task-domain-superseded](../archived/design-task-domain-superseded.md) | 原 DESIGN-0003 | 需求缺位下撰写，循环论证。作为反面案例保留 |
| [design-task-classification-superseded](../archived/design-task-classification-superseded.md) | 原 DESIGN-0004 | 数据契约已并入 design-domain-contract；交互决策被实测证伪 |
| [design-main-window-rework-superseded](../archived/design-main-window-rework-superseded.md) | 原 DESIGN-0005 | 同时承载原则与实施内容，违反类型边界，已拆分 |

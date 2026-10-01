# adr-orm-on-core-entities: sqlite-net 特性标在 Core 实体上

## Metadata

- **ADR ID**: adr-orm-on-core-entities
- **Status**: accepted
- **Decider(s)**: aisdwf
- **Date**: 2026-09-27
- **Supersedes / Superseded By**: 补充 [adr-technology-stack](./adr-technology-stack.md)，不取代它

---

## 1. Context & Problem Statement

`project-rules.md` 规定 Core 是领域与接口层，持久化属于 Infrastructure。
实现里 `TaskItem`、`Project`、`AppSetting` 直接 `using SQLite;` 并标注
`[Table]` / `[PrimaryKey]` / `[Indexed]`，Core 因此引用 `sqlite-net-pcl`。

扫描 M5 给出两条路：把映射搬到 Infrastructure，或承认当前取舍并写成 ADR。
所有者 2026-09-27 裁定：**不搬家**，补 ADR，并去掉 Core 里未使用的 `CommunityToolkit.Mvvm`。

---

## 2. Decision Outcome

**Chosen Option**: 领域实体继续承载 sqlite-net 映射特性。Core 保留 `sqlite-net-pcl`
（及 `SQLitePCLRaw.bundle_green`，因为 pcl 包需要它），**不**引用 CommunityToolkit.Mvvm。

### Detailed Rationale

1. sqlite-net 没有独立的 Fluent 映射 API；把同一张表的列名再抄一份到 Infrastructure
   只会得到第二份会漂移的 schema（Article 6 / 10）。
2. 仓储已经是唯一写入漏斗。ORM 特性只描述表形状，不把 SQL 或连接泄漏进领域逻辑。
3. CommunityToolkit.Mvvm 从未在 Core 源码中使用。消息类型是普通 record，总线在 Desktop 注册。

---

## 3. Considered Alternatives

### Option A: 映射类放到 Infrastructure，Core 实体保持 POCO
- **Pros**: 严格分层。
- **Cons**: 每个字段写两次；sqlite-net 仍要求可 new 的映射类型，最终还是「实体的影子」。已否决。

### Option B: 换 EF Core 做 Fluent 映射
- **Pros**: 实体可保持无 ORM 特性。
- **Cons**: `adr-technology-stack` 已否决 EF Core。不在本决策重开。

---

## 4. Consequences & Impact

- Core 继续依赖 sqlite-net 程序集。新实体若要落库，映射特性仍标在 Core 类型上。
- 若 sqlite-net 日后提供不污染实体的映射方式，可以另开 ADR 再搬。

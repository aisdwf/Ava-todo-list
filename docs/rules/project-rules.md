# Project-Specific Rules — FlowTask

English rule card for AI agents. Fills Constitution Article 7 (project-specific directives) and the technical/architecture layer that sits below the universal `AI_CONSTITUTION.md`.

---

## Business rules

- The product is a cross-platform desktop todo app (FlowTask): main window + a global-hotkey quick-capture mini window.
- Product requirements are the single source of truth in `docs/requirements/REQUIREMENTS.md`. Do not derive requirements from this project's own README marketing copy (see `docs/rules/rule-no-invented-user-behavior.md` — this exact mistake already happened once).
- Interaction decisions (how a field is entered, when a control appears, what happens after an action) are design decisions, not implementation details to defer. See `docs/rules/rule-no-invented-user-behavior.md`.

---

## Technical rules

Full detail lives in `docs/rules/rule-code-standards.md`; this section is the quick-reference summary.

- .NET 8 + Avalonia 11 + CommunityToolkit.Mvvm (source generators) + `sqlite-net-pcl`. See `docs/adr/adr-technology-stack.md` for rationale.
- Nullable reference types must stay enabled; do not suppress warnings without a guard.
- ViewModels use `[ObservableProperty]` / `[RelayCommand]`; no hand-written `INotifyPropertyChanged` boilerplate.
- Cross-window communication uses `WeakReferenceMessenger`; no strong references between Window/ViewModel instances (memory-leak risk with multi-window).
- Interfaces prefixed `I`; async methods suffixed `Async` with `CancellationToken` support where meaningful.
- Comments explain **why**, not what; public APIs and domain entities carry XML doc comments.

---

## Architecture rules

- Layers: `FlowTask.Core` (domain/interfaces) → `FlowTask.Infrastructure` (SQLite persistence) → `FlowTask.Desktop` (Avalonia UI/ViewModels). Dependency direction is one-way inward; UI must not leak into Core/Infrastructure.
- Single-process, multi-window model. No window may hold a strong reference to another window's ViewModel.
- Configuration/business rules must have exactly one authoritative source (Constitution Article 6); do not fork a second lookup table for the same fact.

---

## Notes

- Universal engineering law lives in `AI_CONSTITUTION.md`.
- Process / docs / commit mechanics live in sibling files under `docs/rules/`.
- Prefer machine-checkable rules; attach grep/AST/CI notes when a rule is activated.

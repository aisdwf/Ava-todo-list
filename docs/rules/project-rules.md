# Project-Specific Rules — FlowTask

English rule card for AI agents. Fills Constitution Article 7 (project-specific directives) and the technical/architecture layer that sits below the universal `AI_CONSTITUTION.md`.

---

## Business rules

- The product is a cross-platform desktop todo app (FlowTask): main window + a global-hotkey quick-capture mini window.
- Product requirements are the single source of truth in `docs/requirements/REQUIREMENTS.md`. Do not derive requirements from this project's own README marketing copy (see `docs/rules/rule-no-invented-user-behavior.md` — this exact mistake already happened once).
- Interaction decisions (how a field is entered, when a control appears, what happens after an action) are design decisions, not implementation details to defer. See `docs/rules/rule-no-invented-user-behavior.md`.

### BR-1: The operation guide is co-maintained with user-facing behavior

The first-run tour and Settings → Operation guide (REQUIREMENTS R-6, `spec-onboarding-guide`) teach concrete gestures, keys, and control positions. When a feature changes, they go stale without anyone noticing. Owner decision (2026-09-29): future feature updates must update the guide as well.

Mandate: any task that adds, removes, or changes a **user-visible interaction** (a gesture, shortcut, control position, entry point, or default value that the user has to know) must, in the same task branch:

1. Update `src/FlowTask.Desktop/ViewModels/GuideCatalog.cs`: add, edit, or delete the matching `GuideTopic` text. The catalog is the only source of guide text.
2. Update the matching scene in `src/FlowTask.Desktop/Views/Guide/GuideScenes.cs` when the demonstrated motion or layout changed.
3. If a first-run tour step is affected (a topic with `TargetKeys`), keep its `OnboardingAnchor.Key` on the real control, and **raise `OnboardingProgress.CurrentVersion` by 1** so existing users see the revised tour once.
4. Record the outcome in the task SPEC under **Change checklist**, using exactly one line:
   - `- [x] Guide: updated <topic titles>` or
   - `- [x] Guide: not affected — <one-sentence reason>`.

Pure refactors, bug fixes that restore already-documented behavior, and visual restyling that keeps positions and gestures unchanged count as "not affected", but still need that line.

Machine checks:

- `GuideSceneRenderTests` builds and plays every `GuideSceneKind`; a new kind without a scene fails.
- `OnboardingTests.Catalog_EveryTopicHasDistinctSceneAndSummary` fails when a scene kind has no catalog topic or vice versa.
- `CoachMarkOverlayTests` fails when a tour anchor is removed from `MainWindow.axaml` or no longer wraps a visible control.
- `GuideMaintenanceTests` fails when an `OnboardingTargets` constant is not referenced by any view, or when a SPEC with `Created Date` on or after the adoption date has a `## Change checklist` with no `Guide:` line. The check reads file content only, not git history, so it behaves the same on task branches, `dev`, and `main`. SPECs created before the adoption date are exempt.

Adoption date: 2026-09-29.

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

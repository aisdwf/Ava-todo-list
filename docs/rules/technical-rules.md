# Technical Rules

English rule card for AI agents. Machine-authority definitions for mandatory
technical/architecture constraints that are broader in scope than a single
naming/comment convention (see `rule-code-standards.md`) but narrower than the
full project-rules summary (see `project-rules.md`).

---

## Authority and scope

This file is the **single authoritative definition** for mandatory
technical/architecture decomposition rules of the kind that emerge from
structural code-quality review (as opposed to process/workflow mechanics,
which live in `workflow-methodology.md` / `docs-conventions.md` /
`commit-conventions.md`).

Background research, case studies, and the reasoning that led to a rule below
live under `docs/technical/` (non-normative — informative context only, not
executable guidance). **This file does not copy that research verbatim; it
states the normative rule.** If a rule here appears to conflict with a
specific number or detail in a `docs/technical/` case study, this file wins.

---

## TR-1: ViewModel command decomposition (one user action = one ViewModel class)

### Mandate

1. A `[RelayCommand]`-annotated method in an aggregate-root ViewModel (e.g.
   `MainViewModel`) **must not** keep its full implementation inline once its
   body — excluding a leading null-guard / early-return — exceeds roughly
   10 lines, or once it performs more than one distinct side effect (e.g. a
   repository write **and** a collection reload **and** a UI-state reset in
   the same method).
2. Logic that crosses this threshold **must** be extracted into a dedicated
   ViewModel class, one class per user-initiated action (e.g.
   `CreateProjectViewModel`, `RenameProjectViewModel`, `DeleteTagViewModel`).
3. The aggregate-root ViewModel retains only: observable state, derived
   read-only properties, and thin methods that instantiate and invoke the
   extracted action ViewModel. It **must not** re-implement the action's
   internal steps itself.
4. Cross-cutting, UI-independent logic (e.g. color-cycling, formatting,
   coordinate math) that is duplicated across two or more command methods
   **must** be extracted into a static coordinator/service (following the
   existing `AppearanceCoordinator` pattern), not left duplicated inside
   ViewModel methods.

### Rationale (summary — full case study in `docs/technical/`)

A confirmed case (`MainViewModel.cs`, 1183 lines, five orthogonal
responsibilities: task CRUD, project management, tag management, due-date
editing, appearance settings) showed that without this rule, an
aggregate-root ViewModel grows without a natural stopping point. The failure
mode is not raw line count — large aggregate-root state containers are
observed even in mature, high-star Avalonia projects — the failure is mixing
**state holding** with **action implementation detail** inside the same
class. See `docs/technical/analysis-avalonia-architecture-references.md` for
the reference case (`sourcegit-scm/sourcegit`) that demonstrates the
"one action = one class" pattern at production scale.

### Applicability

- Applies to every `partial class` ViewModel under
  `src/FlowTask.Desktop/ViewModels/` going forward.
- Existing methods that already exceed the threshold as of the adoption date
  below are tracked as pre-existing technical debt, not treated as new
  violations. They **must not** be extended with additional logic without
  first being extracted — "grows further" is a violation even when "already
  large" is not.

### Machine-checkable heuristic

```bash
# Heuristic only — flags candidates for manual review, does not prove a violation.
# Looks for [RelayCommand]-annotated private/public methods whose body exceeds ~300 chars.
rg -U '\[RelayCommand\]\s*\n\s*(private|public)\s+(async\s+)?(Task\S*|void)\s+\w+\([^)]*\)\s*\{[^}]{300,}?\}' \
  src/FlowTask.Desktop/ViewModels/*.cs
```

### Exceptions

- Trivial single-statement toggles (e.g. `IsSettingsOpen = !IsSettingsOpen;`)
  are exempt.
- A method that is pure delegation — one repository/service call followed by
  one reload call, with no branching — is exempt even if it is a few lines
  long.
- Test doubles and view-only converters are out of scope for this rule.

---

## Related

- Case study and full research: `docs/technical/analysis-avalonia-architecture-references.md`
- Coding style, comments, source-generator usage: `rule-code-standards.md`
- Project-wide technical/architecture summary: `project-rules.md`

---

- **Adoption date**: 2026-09-17
- **Status**: active

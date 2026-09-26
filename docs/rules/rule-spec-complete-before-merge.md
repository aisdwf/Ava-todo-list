# rule-spec-complete-before-merge: Finish SPEC and code on the task branch before merging `dev`

## Metadata

- **Rule ID**: rule-spec-complete-before-merge
- **Category**: process
- **Severity**: BLOCK (Hard Error)
- **Status**: active
- **Created Date**: 2026-09-26
- **Related Incident / SPEC**: spec-settings-master-detail-and-theme-presets (merged while still `[IN-PROGRESS]`, then closed on `dev`); a later misread of the owner’s correction invented the opposite rule (`rule-spec-on-dev-before-merge`, withdrawn)

---

## 1. Why

The intended unit of work is one dedicated `feature/*` or `bugfix/*` branch:
SPEC + code, preview, commit, then merge into `dev`.

Two mistakes already happened:

1. `feature/settings-full-window` merged into `dev` while its SPEC was still
   `[IN-PROGRESS]`. Closing it became a follow-up commit on `dev`.
2. The owner said the SPEC must be processed **before** the merge. That was
   read as “SPEC lives only on `dev`; feature branches are code-only.” The
   owner then restated: complete SPEC **and** code on the dedicated branch,
   preview, commit, then merge.

Closing a SPEC on `dev` after merge is recovery, not the path.

---

## 2. Mandates

1. Do SPEC and code on the same `feature/*` or `bugfix/*` branch.
2. After owner preview passes: close the SPEC (`[DONE]`, rename, index,
   handoff) and commit it **with** the code on that branch.
3. Only then merge into `dev`.
4. Do not merge an `[IN-PROGRESS]` SPEC and close it afterwards on `dev`.
5. Do not split “code on the feature branch, SPEC bookkeeping on `dev`.”

---

## 3. Related

- Worktree merge sequence: container `AGENTS.md` (not in git)
- Workflow Steps 5–6: `docs/rules/workflow-methodology.md`
- SPEC index: `docs/rules/docs-conventions.md`

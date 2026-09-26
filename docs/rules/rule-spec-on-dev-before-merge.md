# rule-spec-on-dev-before-merge: SPEC lifecycle lives on `dev`

## Metadata

- **Rule ID**: rule-spec-on-dev-before-merge
- **Category**: process
- **Severity**: BLOCK (Hard Error)
- **Status**: active
- **Created Date**: 2026-09-26
- **Related Incident / SPEC**: spec-unified-svg-icons (SPEC closed on a feature branch forked from stale `main`, then merged into `dev` and conflicted the living index)

---

## 1. Why

`docs/specs/README.md` is the living handoff. It only stays current on `dev`,
where other branches have already been merged.

`feature/unified-svg-icons` was cut from `origin/main`. Its SPEC and index
described an older tree. Closing that SPEC on the feature branch and then
merging into `dev` produced a `docs/specs/README.md` conflict and mixed
SPEC bookkeeping into the code merge.

The owner correction: handle SPECs on `dev`; process the SPEC **before**
merging the feature/bugfix branch.

---

## 2. Mandates

1. Create, rename, status-change, and index SPECs **only on `dev`**.
2. Feature and bugfix worktrees implement code. They must not add or close
   SPEC files or rewrite `docs/specs/README.md`.
3. After owner verification, close the SPEC on `dev` first (`[DONE]` rename +
   index + handoff), **then** merge the feature/bugfix branch.
4. If a branch still contains SPEC or index files, `dev` wins. Do not take
   the branch's stale `docs/specs/README.md`.

---

## 3. Related

- Worktree merge sequence: container `AGENTS.md` (not in git)
- Workflow Steps 5–6: `docs/rules/workflow-methodology.md`
- SPEC index: `docs/rules/docs-conventions.md`

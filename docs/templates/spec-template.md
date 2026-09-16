# spec-${FEATURE}: ${SPEC_TITLE}

<!--
File path: docs/specs/<area>/spec-${FEATURE}[STATUS].md
Naming and lifecycle rules: docs/rules/docs-conventions.md (single authority, do not redefine here).
Iron Law: Once development starts, never stay in [DRAFT] - switch immediately to [IN-PROGRESS] and rename the file.
Index update required on add/rename/status change: docs/specs/README.md (hand-maintained, see docs-conventions.md).
-->

## Metadata

- **ID**: spec-${FEATURE}
- **Type**: simple | complex | exploratory
- **Status**: draft | in-progress | done | done-refactored | superseded | archived | obsolete
- **Owner**: ${OWNER_OR_AI}
- **Created Date**: YYYY-MM-DD
- **Last Updated**: YYYY-MM-DD

---

## 1. Why (Problem & Context)

${PROBLEM_DESCRIPTION_AND_BACKGROUND}

- **Core Motivation**: ${WHY_NOW_AND_WHY_THIS_MATTERS}
- **Current Limitation**: ${LIMITATIONS_OF_EXISTING_IMPLEMENTATION}

---

## 2. What (Scope & Boundaries)

- **Goals**:
  - ${GOAL_1}
  - ${GOAL_2}
- **Non-Goals (Out of Scope)**:
  - ${NON_GOAL_1}
- **Impacted Files & Modules**:
  - `${PATH_TO_AFFECTED_FILE_1}`
  - `${PATH_TO_AFFECTED_FILE_2}`

---

## 2b. Constraints and decisions

${APPLICABLE_CONSTITUTION_ARTICLES_RULES_ADRS_AND_ACCEPTED_CONTRACTS}

---

## 3. Phased Implementation Plan (分阶段实施计划)

<!--
For complex tasks: present 3-8 key execution steps to the user first.
Once user approves, change Status from [DRAFT] to [IN-PROGRESS] and rename the file.
-->

- [ ] **Phase 1: ${PHASE_1_TITLE}**
  - [ ] ${TASK_1_1}
  - [ ] ${TASK_1_2}
- [ ] **Phase 2: ${PHASE_2_TITLE}**
  - [ ] ${TASK_2_1}
- [ ] **Phase 3: Verification & Clean-up**
  - [ ] Run test suite & type checks
  - [ ] Clean obsolete references & update `docs/specs/README.md` (hand-maintained index)

---

## 4. Progress & Subagent Traceability (执行记录与上下文追踪)

<!-- 
Essential for working memory! Ensure seamless resumption after context compaction.
Record subagent dispatches, task_ids, findings, and key branch decisions.
-->

### Subagent Log

| Timestamp | Subagent Type | Task Dispatched | Task ID | Key Outcome / Findings |
| :--- | :--- | :--- | :--- | :--- |
| YYYY-MM-DD HH:mm | explore | ${SEARCH_TARGET} | `task-xxxx` | ${SHORT_SUMMARY} |

### Key Decisions & State Deltas

- **[YYYY-MM-DD]**: ${DECISION_OR_DISCOVERY_DURING_EXECUTION}

---

## 5. Verification Chain (验证矩阵)

- **Machine Gate (Required First)**:
  - [ ] Type Check: `${TYPE_CHECK_COMMAND}`
  - [ ] Unit / Integration Tests: `${TEST_COMMAND}`
  - [ ] Build Check: `${BUILD_COMMAND}`
- **Human Verification (After Machine Pass)**:
  - [ ] Step 1: ${MANUAL_VERIFICATION_STEP_1}
  - [ ] Step 2: ${MANUAL_VERIFICATION_STEP_2}

---

## 6. Commit Attribution & Lessons Learned

- **Attribution**: Design Wrong | Code Wrong | Test Wrong | N/A (Feature)
- **Root Cause & Lessons Learned**:
  - ${WHAT_WENT_WRONG_OR_VALUABLE_TAKEAWAY}

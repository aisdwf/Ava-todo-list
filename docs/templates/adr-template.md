# ADR-XXXX: ${DECISION_TITLE}

<!-- 
Architecture Decision Record (ADR).
Captures significant architectural choices along with context, alternatives, and consequences.
-->

## Metadata

- **ADR ID**: ADR-${NUMBER}
- **Status**: draft | accepted | superseded | deprecated
- **Decider(s)**: ${DECISION_MAKERS}
- **Date**: YYYY-MM-DD
- **Supersedes / Superseded By**: ${OPTIONAL_ADR_LINK}

---

## 1. Context & Problem Statement

${DESCRIBE_THE_CONTEXT_AND_FORCES_AT_PLAY}

- Key Forces & Constraints:
  - ${CONSTRAINT_1}
  - ${PERFORMANCE_SECURITY_OR_MAINTENANCE_REQUIREMENT}

---

## 2. Decision Outcome

**Chosen Option**: ${CHOSEN_OPTION}

### Detailed Rationale

${EXPLAIN_WHY_THIS_OPTION_WAS_SELECTED_OVER_ALTERNATIVES}

---

## 3. Considered Alternatives

### Option A: ${ALTERNATIVE_1}
- **Pros**: ${PROS}
- **Cons**: ${CONS_AND_REASON_REJECTED}

### Option B: ${ALTERNATIVE_2}
- **Pros**: ${PROS}
- **Cons**: ${CONS_AND_REASON_REJECTED}

---

## 4. Consequences & Impact

- **Positive Impact**:
  - ${BENEFIT_1}
- **Negative Impact & Trade-offs**:
  - ${TRADE_OFF_OR_TECHNICAL_DEBT_ACCEPTED}
- **Follow-up Actions**:
  - [ ] Update affected architectural rules (`docs/rules/`)
  - [ ] Implement guardrail test or AST check

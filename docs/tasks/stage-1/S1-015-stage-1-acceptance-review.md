---
schema_version: 1
id: S1-015
title: Complete BDD bindings and Stage 1 acceptance review
stage: 1
status: in_progress
priority: 1000
type: acceptance
depends_on: [S1-009, S1-010, S1-011, S1-012, S1-013, S1-014]
labels: [bdd, stage-gate, review]
created: 2026-08-19
updated: 2026-08-19
---

# S1-015: Complete BDD Bindings and Stage 1 Acceptance Review

## Objective

Bind every Stage 1 Gherkin scenario to implemented behavior, execute the complete stage suite, and produce the Stage Review Record.

## Scope

- Implement or complete thin step definitions and reusable Stage 1 drivers.
- Remove all pending or undefined Stage 1 steps.
- Execute the acceptance, unit, and architecture suites on applicable platforms.
- Verify every Stage 1 criterion and traceability link.
- Produce the Stage 1 Review Record with validation evidence and known non-blocking limitations.
- Update task statuses and the Stage 1 completion summary.

## Out of Scope

- Stage 2 persistence implementation.
- New behavior not required by Stage 1 criteria.
- Waiving financial, authorization, idempotency, or architecture failures.

## Acceptance Criteria

- Every `@stage1` Reqnroll scenario is discovered and passes on Windows and Linux unless explicitly Windows-only.
- No required Stage 1 scenario is pending, undefined, or skipped.
- All Stage 1 unit and architecture tests pass.
- The Release build has no warnings.
- WPF builds successfully on Windows.
- Non-WPF projects build and test successfully on Windows and Linux.
- Every task dependency is `done` and the stage index is current.
- The Stage Review Record documents evidence, migration version where applicable, deviations, follow-up tasks, and approval to begin Stage 2.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 test
.\dev.ps1 format
```

Confirm equivalent Linux CI results for every non-WPF project and `@stage1` scenario.

## Completion Notes

In progress.

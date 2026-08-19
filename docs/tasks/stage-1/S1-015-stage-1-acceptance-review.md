---
schema_version: 1
id: S1-015
title: Complete BDD bindings and Stage 1 acceptance review
stage: 1
status: done
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

Completed 2026-08-19.

- Added thin, scenario-scoped Stage 1 bindings backed by direct domain behavior and deterministic repository inspection; removed all five justified feature-level `@ignore` tags.
- Added the acceptance-to-Core project reference and refreshed the committed lock file. No external provider dependency or runtime integration was introduced.
- Added the [Stage 1 Review Record](../../stage-1-review.md), including traceability, migration applicability, evidence, limitations, follow-ups, ADRs, and the remaining approval condition.
- Validation passed: `./dev.ps1 restore`; `./dev.ps1 build` (Release, zero warnings/errors); `./dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage1"` (48 passed, zero skipped); `./dev.ps1 test` (275 Core, 6 architecture, 48 acceptance; zero failed/skipped); and `./dev.ps1 format`.
- Hosted validation passed for exact public `main` revision `facd9652303dffddc4875f719c6b673c7de516a4` in [CI run 32264483096](https://github.com/alistairshaw/llm-trader/actions/runs/32264483096): Windows and Linux validation succeeded, native WPF built on Windows, and unexpired TRX artifacts were retained as `test-results-Windows` (`9369645877`, 71,313 bytes) and `test-results-Linux` (`9369632187`, 70,803 bytes).
- [Security run 32264481275](https://github.com/alistairshaw/llm-trader/actions/runs/32264481275) succeeded with secret scanning and SARIF artifact `9369590494`; dependency review was correctly skipped for the push event.
- Every dependency is `done`, every criterion passes, and the Stage Review Record approves beginning Stage 2.
- Deviations: none. Follow-up tasks: none. ADRs: none. Migration version: not applicable.

---
schema_version: 1
id: S7-016
title: Complete production-backed non-UI acceptance
stage: 7
status: done
priority: 760
type: test
depends_on: [S7-003, S7-013]
labels: [acceptance, cross-platform]
created: 2026-08-22
updated: 2026-08-22
---
# S7-016: Complete Production-Backed Non-UI Acceptance

## Objective
Activate every cross-platform Stage 7 scenario through production operator services.

## Context
Use [Steps and Drivers](../../test-plan.md#103-steps-and-drivers) and Stage 7 traceability.

## Scope
- Add thin steps and a scenario driver using production Host composition and fresh migrated SQLite.
- Exercise authorization, kill-switch hierarchy/audit, commands, update delivery, and shutdown with deterministic substitutes.
- Observe only authorized queries, command results, update contracts, and lifecycle diagnostics.
- Remove non-UI pending tags and synchronize generated sources.

## Out of Scope
None.

## Acceptance Criteria
- Every non-UI scenario passes twice locally with zero skips.
- Driver has no keyword-derived oracle or direct EF/repository/broker access.
- Diagnostics are bounded, stable, and redacted.

## Validation
Build; Stage7 cross-platform acceptance twice; full tests; format.

## Completion Notes
Activated all four cross-platform Stage 7 scenarios through explicit thin step bindings and a scenario-scoped
application driver. The driver starts the production Generic Host against a unique freshly migrated SQLite file,
uses deterministic application-boundary substitutes, routes commands and queries through `AuthorizedOperatorService`,
persists and queries hierarchical kill switches through the production store, consumes the production bounded update
contract, and observes bounded authorization audit and lifecycle diagnostics. It contains no scenario-title/keyword
oracle and no direct EF, repository, or broker access. Removed the non-UI ignore tag and synchronized generated
Reqnroll sources and traceability.

Validation:

- `.\dev.ps1 build` — passed with 0 warnings and 0 errors.
- `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage7&TestCategory!=windows"` —
  passed twice; 4 passed, 0 failed, 0 skipped on each run.
- `.\dev.ps1 test` — 1,227 passed, 0 failed, 0 skipped.
- `.\dev.ps1 format` — passed after correcting whitespace reported by the first verification run.

All local validation ran in the Linux development container. Cross-platform Windows execution remains delegated to
hosted CI. No deviations, follow-up tasks, or ADR changes.

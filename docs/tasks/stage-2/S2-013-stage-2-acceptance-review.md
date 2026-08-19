---
schema_version: 1
id: S2-013
title: Complete Stage 2 acceptance and review
stage: 2
status: review
priority: 1000
type: acceptance
depends_on: [S2-001, S2-002, S2-003, S2-004, S2-005, S2-006, S2-007, S2-008, S2-009, S2-010, S2-011, S2-012]
labels: [bdd, review, stage-gate]
created: 2026-08-19
updated: 2026-08-19
---

# S2-013: Complete Stage 2 Acceptance and Review

## Objective

Prove every Stage 2 criterion on Windows and Linux and close the stage with auditable evidence.

## Context

Use [Implementation Plan — Stage 2](../../implementation-plan.md#4-stage-2-persistence-and-portfolio-state), [Test Plan](../../test-plan.md), [Task Management](../../task-management.md), and every completed Stage 2 task record.

## Scope

- Audit traceability from every Stage 2 acceptance criterion to passing unit, SQLite integration, migration, transaction, query, integration, and Reqnroll tests.
- Run locked restore, Release build, formatting, architecture tests, focused data tests, integration tests, and the complete test suite.
- Run every Stage 2 Reqnroll scenario with no pending or skipped result.
- Verify the initial migration against a new database and the empty Stage 1 upgrade fixture.
- Demonstrate create, persist, restart, reload, query, and hash verification for the complete portfolio slice.
- Publish the completed revision and record successful Windows and Linux hosted CI evidence.
- Create the Stage 2 Review Record with delivered scope, migration identity, commands, results, demonstration evidence, defects, follow-up task IDs, ADRs, and the Stage 3 commencement decision.
- Reconcile task metadata, the Stage 2 index, and review evidence.

## Acceptance Criteria

- Every Stage 2 task dependency is `done`.
- Every Stage 2 acceptance criterion has passing automated evidence.
- Every Stage 2 scenario passes on Windows and Linux with zero pending or skipped scenarios.
- Fresh and Stage 1 fixture migration paths pass.
- Release build has zero warnings and formatting, analyzer, architecture, migration, integration, and full-suite gates pass.
- The Stage 2 Review Record contains exact local and hosted evidence and an explicit Stage 3 decision.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 format
.\dev.ps1 test -Project tests/Trading.Data.Tests
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage2"
.\dev.ps1 test
```

## Completion Notes

Audited every Stage 2 task, acceptance criterion, and scenario; expanded the restart-safe integration demonstration to cover the complete persisted portfolio slice including Trading Bot configuration, immutable Decision Snapshot, canonical hash, and no-tracking query reload. Created `docs/stage-2-review.md` with migration identity, criterion traceability, exact local commands and counts, restart/hash evidence, defects, follow-ups, ADRs, and the conditional Stage 3 decision.

Local validation passed: locked restore; Release build with 0 warnings and 0 errors; formatting; Data 92/92; Integration 1/1; Stage 2 acceptance 20/20 with zero skipped; Architecture 11/11; migration tests 3/3; EF migration-model drift check; and full suite 447/447 with zero skipped. The demonstrated snapshot hash is `8cfd7f682511c8b68fe8491b4c801c3734b72d4d300f01af954feaa8509813c2` under migration `20260819154728_InitialStage2Persistence`.

No scope deviations, follow-up tasks, ADR changes, or known critical/high defects. The sole remaining gate is successful hosted Windows and Linux CI on the exact published review revision. The task remains `review` until that evidence is recorded.

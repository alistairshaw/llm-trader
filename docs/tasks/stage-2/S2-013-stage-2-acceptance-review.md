---
schema_version: 1
id: S2-013
title: Complete Stage 2 acceptance and review
stage: 2
status: planned
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

Not completed.

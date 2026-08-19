---
schema_version: 1
id: S3-015
title: Complete Stage 3 acceptance and review
stage: 3
status: ready
priority: 1000
type: acceptance
depends_on: [S3-001, S3-002, S3-003, S3-004, S3-005, S3-006, S3-007, S3-008, S3-009, S3-010, S3-011, S3-012, S3-013, S3-014]
labels: [bdd, review, stage-gate]
created: 2026-08-19
updated: 2026-08-19
---

# S3-015: Complete Stage 3 Acceptance and Review

## Objective

Prove every Stage 3 runtime criterion on Windows and Linux and close the stage with exact evidence.

## Context

Use [Implementation Plan — Stage 3](../../implementation-plan.md#5-stage-3-multi-bot-runtime-and-scheduling), [Trading Bot](../../trading-bot.md), [Test Plan](../../test-plan.md), every completed Stage 3 task, and the Stage 2 Review Record.

## Scope

- Audit traceability from every Stage 3 criterion to passing unit, data, integration, host, recovery, and Reqnroll tests.
- Run locked restore, Release build, formatting, architecture, Core, Data, Engine, Integration, Acceptance, migration, model-drift, host smoke, and complete-suite gates.
- Run every Stage 3 scenario with zero pending or skipped result.
- Verify the Stage 3 migration against a new database and the completed Stage 2 fixture.
- Demonstrate two concurrent isolated scripted Bots, trigger coalescing, bounded reasoning, safe timeout, schedule adjustment, expired-lease recovery, and graceful headless shutdown.
- Publish the completed revision and record successful Windows and Linux hosted CI and security evidence.
- Create the Stage 3 Review Record with delivered scope, migration identity, commands, counts, demonstration identities, audit reconstruction, defects, follow-up task IDs, ADRs, and the Stage 4 commencement decision.
- Reconcile task metadata, Stage 3 index, documentation, and review evidence.

## Acceptance Criteria

- Every Stage 3 dependency is `done`.
- Every Stage 3 acceptance criterion has passing automated evidence.
- Every Stage 3 scenario passes on Windows and Linux with zero pending or skipped result.
- Fresh and Stage 2 fixture migration paths pass with no model drift.
- Release build has zero warnings and every local and hosted gate passes.
- The Stage 3 Review Record contains exact local and hosted evidence and an explicit Stage 4 decision.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 format
.\dev.ps1 test -Project tests/Trading.Core.Tests
.\dev.ps1 test -Project tests/Trading.Data.Tests
.\dev.ps1 test -Project tests/Trading.Engine.Tests
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage3"
.\dev.ps1 run
.\dev.ps1 test
```

## Completion Notes

Not completed.

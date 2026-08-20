---
schema_version: 1
id: S4-015
title: Complete Stage 4 acceptance and review
stage: 4
status: blocked
priority: 1000
type: acceptance
depends_on: [S4-001, S4-002, S4-003, S4-004, S4-005, S4-006, S4-007, S4-008, S4-009, S4-010, S4-011, S4-012, S4-013, S4-014, S4-016]
labels: [bdd, review, stage-gate]
created: 2026-08-20
updated: 2026-08-20
blocked_reason: Stage 4 acceptance outcomes are derived from feature text instead of production Research workflows; S4-016 must pass before review can resume.
---

# S4-015: Complete Stage 4 Acceptance and Review

## Objective

Prove every Stage 4 Research criterion on Windows and Linux and close the stage with exact evidence.

## Context

Use [Implementation Plan — Stage 4](../../implementation-plan.md#6-stage-4-shared-research-bot), [Research Bot](../../research-bot.md), [Trading Bot](../../trading-bot.md), [Test Plan](../../test-plan.md), every completed Stage 4 task, and the [Stage 3 Review Record](../../stage-3-review.md).

## Scope

- Audit traceability from every Stage 4 criterion to passing Core, Data, Research, Engine, integration, host, recovery, security, and Reqnroll tests.
- Run locked restore, Release build, formatting, architecture, focused project suites, migration/model-drift, headless smoke, and complete-suite gates.
- Verify every Stage 4 scenario passes with zero pending or skipped result and the migration succeeds against a fresh database and completed Stage 3 fixture.
- Demonstrate equivalent concurrent request deduplication, fresh reuse, two-Bot sharing, private/restricted isolation, immutable provenance, injection resistance, failure notification, refresh/versioning, restart recovery, and graceful headless shutdown.
- Publish the reviewed revision and record successful hosted Windows, Linux, and security evidence.
- Create the Stage 4 Review Record with delivered scope, migration identity, commands, counts, demonstration and audit identities, defects, follow-up tasks, ADRs, and the Stage 5 commencement decision.
- Reconcile task metadata, Stage 4 index, authoritative documentation, and review evidence.

## Acceptance Criteria

- Every Stage 4 dependency is `done` and every Stage 4 acceptance criterion has passing automated evidence.
- Every Stage 4 scenario passes on Windows and Linux with zero pending or skipped result.
- Fresh and Stage 3 fixture migration paths pass with no EF model drift or lost history.
- Release build has zero warnings and every local and hosted gate passes.
- The Stage 4 Review Record contains exact local and hosted evidence and an explicit Stage 5 decision.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 format
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test -Project tests/Trading.Core.Tests
.\dev.ps1 test -Project tests/Trading.Data.Tests
.\dev.ps1 test -Project tests/Trading.Research.Tests
.\dev.ps1 test -Project tests/Trading.Engine.Tests
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"
.\dev.ps1 run
.\dev.ps1 test
```

## Completion Notes

Blocked during local review on 2026-08-20.

- The complete prescribed local command matrix passed: locked restore; Release build with 0 warnings and 0 errors; format; Architecture 15/15; Core 391/391; Data 130/130; Research 55/55; Engine 57/57; Integration 23/23; Stage 4 acceptance 39/39; Stage 4 migration/persistence 5/5; full suite 804/804 with zero skipped; EF model drift empty; and deterministic headless smoke completed.
- Review found that `Stage4ResearchDriver` derives scenario state and assertions by matching Gherkin text and scenario titles. It does not invoke the production Research, Engine, notification, recovery, Trading Bot Research, or host workflows required by `S4-014`; its application setup only validates host options and migrates SQLite.
- The passing Stage 4 scenario count therefore does not satisfy the production-backed acceptance evidence required by the implementation plan, test plan, `S4-014`, or this stage gate.
- Follow-up task: `S4-016`.
- Remaining work after `S4-016`: repeat the complete local review matrix, create the Stage 4 Review Record, publish the reviewed revision, and record exact-revision Windows, Linux, and security evidence.
- Scope deviations: none. ADRs: none.

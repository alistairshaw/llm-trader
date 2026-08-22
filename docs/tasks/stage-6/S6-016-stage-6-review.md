---
schema_version: 1
id: S6-016
title: Complete Stage 6 acceptance and review
stage: 6
status: ready
priority: 1000
type: test
depends_on: [S6-001, S6-002, S6-003, S6-004, S6-005, S6-006, S6-007, S6-008, S6-009, S6-010, S6-011, S6-012, S6-013, S6-014, S6-015]
labels: [review, ci, security, stage-gate]
created: 2026-08-21
updated: 2026-08-22
---

# S6-016: Complete Stage 6 Acceptance and Review

## Objective

Audit Stage 6 against every exit criterion, close discovered defects, and publish an exact-revision review record.

## Context

Use [Implementation Plan — Acceptance Rules](../../implementation-plan.md#2-acceptance-rules-for-every-stage), [Implementation Plan — Stage 6](../../implementation-plan.md#8-stage-6-paper-order-execution), [Task Management — Stage Completion](../../task-management.md#15-stage-completion), and [Test Plan](../../test-plan.md).

## Scope

- Audit every Stage 6 criterion against production code, deterministic tests, migrated evidence, traceability, projections, and the headless demonstration.
- Run restore, Release build, formatting, architecture, focused project suites, migration upgrade/drift, complete tests, repeated Stage 6 acceptance, and repeated headless smoke.
- Inspect authorization, environment isolation, client-ID idempotency, durable messaging, unknown reconciliation, event ordering, atomic financial accounting, restart recovery, audit completeness, and SQLite ownership for critical or high-severity defects.
- Reconcile README, AGENTS.md, architecture, domain, data model, Trading Bot, Research Bot, test plan, implementation plan, local development, task metadata, and traceability with observed behavior.
- Create the Stage 6 Review Record with migration identity, exact commands/results, demonstration identities, limitations, decisions, ADRs, and Stage 7 decision.
- Validate the exact pushed revision through hosted Windows/Linux CI and security workflows and record direct run links.

## Acceptance Criteria

- Every Stage 6 task and stage criterion has objective passing evidence.
- Stage 6 acceptance passes twice locally and on applicable hosted platforms with zero failed, pending, or skipped scenarios.
- Fresh and Stage 5 upgrade migrations, EF drift, full suite, build, format, architecture, repeated headless demonstration, and security gates pass.
- The review finds zero unresolved critical or high-severity defects in authorization, financial integrity, idempotency, reconciliation, recovery, audit, environment isolation, and resource ownership.
- The review record identifies the exact validated revision and hosted workflow results and makes an explicit Stage 7 commencement decision.

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
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage6"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage6"
.\dev.ps1 test
.\dev.ps1 run
.\dev.ps1 run
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
```

## Completion Notes

Pending.

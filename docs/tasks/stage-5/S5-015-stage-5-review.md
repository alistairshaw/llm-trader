---
schema_version: 1
id: S5-015
title: Complete Stage 5 acceptance and review
stage: 5
status: review
priority: 1000
type: test
depends_on: [S5-001, S5-002, S5-003, S5-004, S5-005, S5-006, S5-007, S5-008, S5-009, S5-010, S5-011, S5-012, S5-013, S5-014, S5-016]
labels: [review, ci, security, stage-gate]
created: 2026-08-20
updated: 2026-08-20
---

# S5-015: Complete Stage 5 Acceptance and Review

## Objective

Audit Stage 5 against every exit criterion, close discovered defects, and publish an exact-revision review record.

## Context

Use [Implementation Plan — Acceptance Rules](../../implementation-plan.md#2-acceptance-rules-for-every-stage), [Implementation Plan — Stage 5](../../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk), [Task Management — Stage Completion](../../task-management.md#15-stage-completion), and [Test Plan](../../test-plan.md).

## Scope

- Audit every Stage 5 criterion against production code, deterministic tests, durable evidence, traceability, and the headless demonstration.
- Run restore, Release build, formatting, architecture, focused project suites, migration upgrade/drift, complete tests, repeated Stage 5 acceptance, and headless smoke.
- Inspect proposal authority, evidence binding, policy hierarchy, immutable audit, authorization, fresh-state revalidation, reservation concurrency, ResearchOnly behavior, and broker-boundary isolation for critical or high-severity defects.
- Reconcile README, AGENTS.md, architecture, domain, data model, Trading Bot, Research Bot, test plan, implementation plan, local development, task metadata, and traceability with observed behavior.
- Create the Stage 5 Review Record with migration identity, exact commands/results, demonstration identities, limitations, decisions, ADRs, and Stage 6 decision.
- Validate the exact pushed revision through hosted Windows/Linux CI and security workflows and record direct run links.

## Acceptance Criteria

- Every Stage 5 task and stage criterion has objective passing evidence.
- Stage 5 acceptance passes twice locally and on applicable hosted platforms with zero failed, pending, or skipped scenarios.
- Fresh and Stage 4 upgrade migrations, EF drift, full suite, build, format, architecture, headless demonstration, and security gates pass.
- The review finds zero unresolved critical or high-severity defects in financial integrity, authorization, idempotency, audit, isolation, and broker-boundary safety.
- The review record identifies the exact validated revision and hosted workflow results and makes an explicit Stage 6 commencement decision.

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
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 test
.\dev.ps1 run
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
```

## Completion Notes

Local review completed on 2026-08-20. All fourteen dependencies and every Stage 5 criterion were audited against production code, deterministic application-facing acceptance coverage, migrated SQLite evidence, the headless demonstration, and authoritative documentation. The review record is [Stage 5 Review Record](../../stage-5-review.md).

Validation passed through Linux Docker: locked restore; Release build with zero warnings/errors; format; Architecture 19/19; Core 491/491; Data 149/149; Research 56/56; Engine 92/92; Integration 25/25; Stage 5 acceptance twice at 32/32 with zero failed, pending, or skipped; full suite 997/997 with zero failed or skipped; Stage 5 fresh/Stage-4-upgrade migration coverage 5/5; EF model drift clean; and the deterministic headless smoke twice with matching Stage 5 identities and hashes, one `700 USD` reservation, stable contention and ResearchOnly outcomes, zero broker submissions, and recoverable shutdown.

The audit corrected the hosted CI gate so Windows and Linux execute the complete solution suite rather than only Core, Architecture, and Stage 1 acceptance, advanced the README documentation map to Stage 5, and reconciled task/index metadata. No ADR or follow-up task was created. The existing SQLite migration-runner warnings are recorded in the review and are covered by passing fresh/upgrade/immutability tests; they are not an unresolved integrity defect.

Hosted validation of revision `88681e512dcbde8f04a3e2865722f5646f0b073f` passed Linux job `96622973099` and Security run `32431213636` / secret-scan job `96622973233`, but Windows job `96622973230` failed. Artifact `9429220872` shows widespread teardown `IOException` failures while recursively deleting `test.db`, `runtime.db`, `smoke.db`, `workflow.db`, `capital.db`, `research.db`, and `recovery.db` because another process still held each file. The failure is not an assertion retry or environmental flake: it identifies incomplete SQLite connection/host resource ownership and disposal in cross-platform fixtures.

`S5-016` repaired the fixture lifecycle in commit `f00f99434106624503886dd4cf0bb5678b762cc6`. The resumed local review passed locked restore; Release build with zero warnings/errors; format; `FixtureDisposal` 2/2; `MultiBotSupervisor` 8/8; Architecture 19/19; Core 491/491; Data 149/149; Research 56/56; Engine 93/93; Integration 27/27; Stage 5 acceptance twice at 32/32 with zero failed, pending, or skipped; full suite 1000/1000 with zero failed or skipped; Stage 5 fresh/Stage-4-upgrade migration coverage 5/5; clean EF drift; and deterministic headless smoke with the previously recorded stable identities, hashes, `700 USD` reservation, zero broker submissions, and recoverable shutdown.

The previous failed revision and jobs are superseded. S5-015 is in `review`, and the new exact review revision's hosted Windows, Linux, and security results are the sole remaining gate. Stage 6 commencement is not approved.

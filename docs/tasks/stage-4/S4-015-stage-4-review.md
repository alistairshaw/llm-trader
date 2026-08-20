---
schema_version: 1
id: S4-015
title: Complete Stage 4 acceptance and review
stage: 4
status: done
priority: 1000
type: acceptance
depends_on: [S4-001, S4-002, S4-003, S4-004, S4-005, S4-006, S4-007, S4-008, S4-009, S4-010, S4-011, S4-012, S4-013, S4-014, S4-016]
labels: [bdd, review, stage-gate]
created: 2026-08-20
updated: 2026-08-20
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

Local review passed on 2026-08-20 after `S4-016` repaired the production-backed acceptance boundary.

- Locked restore passed; Release build passed with 0 warnings and 0 errors; format passed.
- Architecture 15/15, Core 391/391, Data 130/130, Research 56/56, Engine 57/57, Integration 23/23, and full suite 805/805 passed with zero failed or skipped tests.
- Stage 4 acceptance passed twice consecutively at 39/39 with zero failed or skipped tests. Explicit feature-case routing executes production application workflows and asserts returned results plus durable migrated-SQLite facts.
- Stage 4 migrations passed 5/5 for fresh and completed Stage 3 upgrade paths; EF model drift is empty.
- Deterministic headless smoke passed shared reuse, private isolation, immutable refresh versioning, exact identities and hash, recoverable shutdown, and graceful deadline completion.
- The Stage 4 Review Record contains exact commands, counts, migration identity, runtime identities, audit evidence, documentation reconciliation, and the pending decision.
- The reviewed revision was published and its exact-revision hosted Windows, Linux, and security results were recorded below.
- Deviations: none. Follow-up tasks: none. ADRs: none.

Hosted validation completed against exact revision `9f318b380646c00ebdd5089f2e5543180c5a0938`:

- CI run `32412331394` passed; Linux job `96565386940` and Windows job `96565386726` passed with test-result artifacts `9422643098` and `9422695375` present and not expired.
- Security run `32412331359` passed; secret scan job `96565386569` passed with non-expired SARIF artifact `9422586531`; dependency review job `96565388000` was skipped as expected for a push.
- Every acceptance criterion and local/hosted gate now passes. Stage 4 is complete and Stage 5 may begin.

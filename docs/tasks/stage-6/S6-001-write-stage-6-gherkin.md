---
schema_version: 1
id: S6-001
title: Write Stage 6 executable Gherkin specifications
stage: 6
status: done
priority: 1000
type: acceptance
depends_on: []
labels: [bdd, orders, fills, paper-trading]
created: 2026-08-21
updated: 2026-08-21
---

# S6-001: Write Stage 6 Executable Gherkin Specifications

## Objective

Define executable business specifications for every Stage 6 paper-order execution criterion.

## Context

Use [Implementation Plan — Stage 6](../../implementation-plan.md#8-stage-6-paper-order-execution), [Domain Model — Order](../../domain.md#91-order-aggregate), [Data Model — Execution Tables](../../data-model.md#10-execution-tables), and [Test Plan — Gherkin Acceptance Tests](../../test-plan.md#10-gherkin-acceptance-tests).

## Scope

- Add tagged features for authorized order creation, atomic outbox creation, stable client identities, submission retry, acknowledgement, rejection, cancellation, expiration, partial and final fills, duplicate events, invalid event order, unknown outcomes, reconciliation, atomic accounting, restart recovery, and paper/live separation.
- Specify the complete scripted research, proposal, approval, reservation, paper Order, partial Fill, final Fill, Position, ledger, and audit demonstration.
- Add traceability from every Stage 6 criterion to named scenarios and implementing tasks.
- Generate discoverable Reqnroll tests and mark implementation-dependent scenarios with the acceptance harness temporary pending tag; `S6-015` activates them.

## Acceptance Criteria

- Every Stage 6 criterion maps to at least one named business-facing scenario.
- Scenarios identify exact Proposal, Approval, Reservation, Order, client order ID, broker event, Fill, Position, ledger source, and reconciliation outcomes where applicable.
- Tags identify Stage 6, execution, idempotency, accounting, recovery, and applicable platforms.
- The Stage 6 filter discovers every scenario with implementation-dependent scenarios explicitly pending.
- Every scenario uses deterministic time and identities, migrated temporary SQLite, and simulated application boundaries.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage6"
.\dev.ps1 format
```

## Completion Notes

Completed 2026-08-21.

- Added six Stage 6 feature files containing 32 named scenarios and 34 discoverable Reqnroll test cases covering authorized Proposal-to-Order conversion, atomic submission work, stable client identities, submission retry and unknown-outcome reconciliation, broker acknowledgements and terminal outcomes, duplicate and out-of-order events, partial and final Fill accounting, restart recovery, paper/live separation, and the complete headless audit chain.
- Added criterion-to-scenario and implementing-task traceability in `tests/Trading.AcceptanceTests/Features/Execution/TRACEABILITY.md`.
- Tagged every scenario for cross-platform discovery and applied the temporary `@ignore` acceptance-harness tag until `S6-015` supplies production-backed bindings.
- Generated and committed synchronized Reqnroll `.feature.cs` files through the repository build.
- Validation: `.\dev.ps1 build` passed with zero warnings and errors; `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage6"` discovered 34 tests with 34 explicitly skipped, zero failed, and zero passed; `.\dev.ps1 format` passed; `git diff --check` passed.
- The first build attempt encountered a stale generated `Trading.Host.AssemblyInfoInputs.cache` file that Docker could not overwrite. Removing that disposable build artifact and rerunning produced the clean build recorded above.
- Local validation used the Linux Docker workflow. Windows execution remains delegated to hosted CI at the Stage 6 gate.
- No deviations, follow-up tasks, ADRs, README changes, AGENTS.md changes, or authoritative-document changes were required because this task defined specifications without changing durable project guidance.

---
schema_version: 1
id: S6-001
title: Write Stage 6 executable Gherkin specifications
stage: 6
status: ready
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

Pending.

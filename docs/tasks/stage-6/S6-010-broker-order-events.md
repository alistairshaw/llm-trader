---
schema_version: 1
id: S6-010
title: Process broker acknowledgements and order outcomes
stage: 6
status: done
priority: 800
type: feature
depends_on: [S6-006, S6-008]
labels: [broker-events, acknowledgement, rejection, cancellation]
created: 2026-08-21
updated: 2026-08-22
owner: s6_010
---

# S6-010: Process Broker Acknowledgements and Order Outcomes

## Objective

Apply normalized broker acknowledgements, rejections, cancellations, and expirations idempotently to Orders.

## Context

Use [Domain Model — Order](../../domain.md#91-order-aggregate), [Data Model — Infrastructure Tables](../../data-model.md#11-infrastructure-tables), and [Test Plan — Orders and Fills](../../test-plan.md#102-initial-journey-catalog).

## Scope

- Dispatch claimed inbox messages to exact account and Order identities with source-event deduplication.
- Apply acknowledged, rejected, cancel-requested, cancelled, expired, and reconciliation-required outcomes through the Order state machine.
- Atomically record the event outcome, Order transition, inbox completion, Reservation release request, and follow-up work.
- Reject, defer, or reconcile unknown Orders, identity mismatches, impossible transitions, stale events, and events that conflict with fill state using stable codes.
- Preserve canonical bounded raw-message hashes and normalized audit facts.

## Acceptance Criteria

- Duplicate messages change Order and Reservation workflow once.
- Valid events produce only state-machine-permitted transitions.
- Terminal Orders never return to active states.
- Out-of-order and conflicting events produce durable safe dispositions without losing the message.
- Rejection, cancellation, and expiration release only the remaining reserved capital.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderExecution"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=BrokerOrderEvents"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=BrokerInboxOutbox"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented strict versioned canonical paper broker-event dispatch for acknowledgement, rejection, cancel request,
cancellation, and expiration. The dispatcher validates account, client, broker, environment, code, timestamp, and event
kind before invoking a single SQLite transaction. That transaction conditionally advances the Order, appends immutable
transition audit, completes the claimed inbox message, releases an active reservation for terminal non-fill outcomes,
and schedules reconciliation for stale conflicts. Unknown Orders and optimistic contention remain durable for bounded
retry; duplicates and terminal conflicts receive stable safe dispositions. Execution events remain assigned to
`S6-011` fill accounting.

Validation:

- `./dev.ps1 build` — passed, 0 warnings and 0 errors.
- `./dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderExecution"` — 10 passed.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=BrokerOrderEvents"` — 9 passed.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=BrokerInboxOutbox"` — 2 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=BrokerInboxOutbox"` — 5 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage6Migrations"` — 9 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=BrokerInboxOutbox|Category=Migrations|Category=OrderConversionTransaction"` — 12 passed after the baseline formatting repair.
- `./dev.ps1 test` — 1,086 passed, 34 intentionally pending Stage 6 acceptance cases, 0 failed.
- `./dev.ps1 format` — passed after minimal formatter-only baseline corrections in `InitialMigrationTests.cs` and
  `AtomicOrderConversionRepositoryTests.cs`; those corrections have no behavior change.

The only scope deviation was the mechanical baseline formatting repair required to restore the repository gate. No
ADRs or code follow-ups were introduced.

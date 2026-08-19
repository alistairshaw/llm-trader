---
schema_version: 1
id: S1-013
title: Implement the Order aggregate and state machine
stage: 1
status: done
priority: 720
type: feature
depends_on: [S1-007]
labels: [domain, orders, fills, state-machine]
created: 2026-08-19
updated: 2026-08-19
---

# S1-013: Implement the Order Aggregate and State Machine

## Objective

Implement the platform-owned Order lifecycle, transition history, and Fill invariants without a broker dependency.

## Scope

- Implement `Order`, `OrderTransition`, and `Fill`.
- Encode creation, submission, acknowledgement, partial fill, fill, cancellation, rejection, expiration, and unknown-outcome states.
- Enforce quantities, prices, currencies, client-order identity, and execution idempotency at the domain boundary.

## Out of Scope

- Broker submission.
- Reconciliation service.
- Position and ledger application.
- EF Core persistence.

## Acceptance Criteria

- Invalid order-type and price combinations are rejected.
- Filled quantity cannot exceed ordered quantity.
- Duplicate broker execution identity cannot be applied twice.
- Unknown submission outcomes cannot be treated as safely retryable without reconciliation.
- Terminal Orders cannot return to active states.
- Transition history is immutable and ordered.
- Every allowed and forbidden state transition has a table-driven unit test.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderAggregate"
```

## Completion Notes

- Added the platform-neutral `Order` aggregate with explicit side, type, time-in-force, currency, client identity, broker identity, timestamps, optimistic version, filled quantity, and reconciliation-needed state. Market and limit order construction rejects invalid price combinations, and all quantity, price, fee, unit, currency, timestamp, and identity inputs are protected at the domain boundary.
- Added immutable `OrderTransition` and `Fill` child entities. Transition history is chronologically ordered and sequenced; fill execution identities are normalized and idempotent; cumulative fills cannot exceed the ordered quantity; and terminal orders cannot reactivate.
- Encoded creation, submission, unknown outcome, reconciliation, acknowledgement, partial/final fill, cancellation, rejection, and expiration transitions. An unknown submission cannot be submitted again and can leave `Unknown` only through an explicitly reconciliation-sourced transition.
- Added 127 focused `OrderAggregate` tests: a generated 11-by-11 table covers every allowed and forbidden state pair, with additional construction, identity, currency/unit, fill-limit, duplicate-execution, unknown-outcome, chronology, and immutable-history cases.
- Validation passed: `.\dev.ps1 build` (Release, 0 warnings and 0 errors); `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderAggregate"` (127 passed); `.\dev.ps1 test` (275 Core tests, 6 architecture tests, and 1 acceptance infrastructure test passed; 47 intentionally deferred Stage 1 acceptance scenarios skipped); and `.\dev.ps1 format` (passed after correcting reported whitespace). The first format attempt was blocked from reading the host Docker configuration inside the filesystem sandbox, then ran with approved access and reported the corrected whitespace before the final passing run.
- The ignored order/fill Gherkin bindings remain intentionally deferred to `S1-015`. Globally enforcing `ClientOrderId` uniqueness requires the persistence boundary and remains outside this task; this aggregate makes the value required and immutable. No other scope deviations, follow-up tasks, or ADRs were required. Git status and diff inspection were unavailable because this workspace exposes no usable Git repository metadata.

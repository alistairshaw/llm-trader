---
schema_version: 1
id: S6-011
title: Apply partial and final fills atomically
stage: 6
status: planned
priority: 780
type: feature
depends_on: [S6-009, S6-010]
labels: [fills, positions, ledger, reservations]
created: 2026-08-21
updated: 2026-08-21
---

# S6-011: Apply Partial and Final Fills Atomically

## Objective

Apply each broker execution exactly once across Order, Position, ledger, Fill, and Capital Reservation state.

## Context

Use [Domain Model — Position](../../domain.md#42-position-aggregate), [Domain Model — Capital Reservation](../../domain.md#82-capitalreservation-aggregate), [Domain Model — Order](../../domain.md#91-order-aggregate), and [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions).

## Scope

- Validate broker account, Order, instrument, execution identity, side, currency, quantity, price, fee, timestamp, and cumulative limits.
- In one transaction insert the immutable Fill/applied marker, transition the Order, update the Position, append exact trade and fee ledger entries, consume the Reservation, and complete the inbox item.
- Calculate weighted cost, proceeds, realized effects, cash, fees, remaining quantity, and remaining reservation with deterministic decimal arithmetic.
- Release residual reservation at terminal completion and preserve exact source identities for every accounting entry.
- Handle duplicate, partial, final, late, overfill, wrong-account, wrong-instrument, and conflicting executions with stable outcomes.

## Acceptance Criteria

- A partial Fill consistently updates all five durable state areas and leaves the exact remaining reservation.
- The final Fill marks the Order filled and consumes or releases the reservation exactly.
- Duplicate execution identity changes no financial value twice.
- Any failure rolls back Order, Position, ledger, Fill marker, Reservation, and inbox completion together.
- Concurrent fills serialize safely and cumulative filled quantity never exceeds ordered quantity.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=FillAccounting"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=AtomicFillApplication"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=PartialAndFinalFills"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending.

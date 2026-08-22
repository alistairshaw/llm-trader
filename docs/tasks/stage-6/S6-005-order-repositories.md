---
schema_version: 1
id: S6-005
title: Implement order execution repositories
stage: 6
status: ready
priority: 900
type: data
depends_on: [S6-004, S6-017]
labels: [repositories, orders, fills, reconciliation]
created: 2026-08-21
updated: 2026-08-22
owner: s6_005
---

# S6-005: Implement Order Execution Repositories

## Objective

Provide aggregate-oriented repositories and atomic operations for paper execution state.

## Context

Use [Architecture — Persistence Design](../../architecture.md#13-persistence-design), [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions), and [Domain Model — Order](../../domain.md#91-order-aggregate).

## Scope

- Implement Order, Fill, broker-account, reconciliation, inbox, and outbox repository adapters behind application ports.
- Add conditional claims, leases, completion, retry scheduling, and stale-claim recovery for durable work.
- Implement exact lookups by Order, Proposal, client order ID, broker order ID, source event ID, and broker execution ID.
- Return domain aggregates and bounded records without exposing EF entities, `DbSet`, or `IQueryable`.
- Add concurrency, rollback, idempotency, account isolation, and immediate Windows fixture-cleanup tests.

## Acceptance Criteria

- Concurrent claims grant one worker and preserve unclaimed work.
- Duplicate broker and execution identities return stable existing outcomes.
- Transaction rollback leaves no partial aggregate, work-item, or reconciliation state.
- Repository reads enforce broker-account, Portfolio, and Order ownership.
- Tests use isolated migrated SQLite files and dispose every owner before first-attempt deletion.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=OrderRepositories"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=DurableBrokerWork"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Blocked on 2026-08-22 before implementation. Inspection of the S6-004 EF model and migration found that
`orders` omits the aggregate's currency and quantity unit, persists lifecycle tokens that do not match
`OrderStatus`, and constrains `TimeInForce` to a differently spelled, incomplete token set. An exact repository
round trip would therefore require inventing financial facts or ambiguous token translation. S6-017 records the
required schema correction. No production or test files were changed and no .NET validation was applicable to
this documentation-only blocker record.

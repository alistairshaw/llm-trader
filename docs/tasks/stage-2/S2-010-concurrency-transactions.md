---
schema_version: 1
id: S2-010
title: Implement concurrency and transaction boundaries
stage: 2
status: planned
priority: 760
type: feature
depends_on: [S2-008, S2-009]
labels: [concurrency, transactions, unit-of-work]
created: 2026-08-19
updated: 2026-08-19
---

# S2-010: Implement Concurrency and Transaction Boundaries

## Objective

Make Stage 2 writes atomic and return deterministic application results for stale or conflicting operations.

## Context

Implement [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions) and [Concurrency and SQLite Operation](../../data-model.md#14-concurrency-and-sqlite-operation).

## Scope

- Implement `IUnitOfWork` over the EF Core context.
- Increment application-maintained integer versions on every mutable aggregate write.
- Translate `DbUpdateConcurrencyException` and scoped uniqueness violations into provider-neutral application results.
- Add explicit transactional operations for initial bot/configuration creation, Portfolio ownership assignment, Position plus applied-fill marker updates, ledger append/correction, and snapshot creation.
- Add deterministic failpoints before commit and after each material write within test-only transaction drivers.
- Add rollback tests proving no partial aggregate, ownership, Position, ledger, marker, or snapshot state survives a failed transaction.
- Add concurrent-context tests proving stale writes are rejected and committed state remains unchanged.

## Acceptance Criteria

- Two writers using the same expected version cannot both commit.
- A stale write returns the documented application concurrency result.
- Every tested failpoint rolls back the complete transaction.
- Uniqueness conflicts return purpose-built results without provider exceptions crossing the data boundary.
- Successful commits increment mutable aggregate versions exactly once.
- Transactions remain short and contain no external call.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ConcurrencyOrTransactions"
.\dev.ps1 build
```

## Completion Notes

Not completed.

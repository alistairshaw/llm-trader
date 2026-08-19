---
schema_version: 1
id: S2-002
title: Define persistence contracts and results
stage: 2
status: ready
priority: 920
type: feature
depends_on: [S2-001]
labels: [repositories, unit-of-work, architecture]
created: 2026-08-19
updated: 2026-08-19
---

# S2-002: Define Persistence Contracts and Results

## Objective

Define application-facing persistence contracts for the complete Stage 2 aggregate set.

## Context

Follow [Data Model — Repository Contracts](../../data-model.md#12-repository-contracts), [Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions), and [Domain Model — Repository Boundaries](../../domain.md#12-repository-boundaries).

## Scope

- Add intent-oriented repository interfaces for Broker Connections, Broker Accounts, Instruments, Trading Bots, Portfolios, Positions, Portfolio Ledger Entries, and Portfolio Decision Snapshots.
- Add an `IUnitOfWork` contract.
- Add explicit application results for successful writes, uniqueness conflicts, and optimistic-concurrency conflicts.
- Add query-service contracts and projection records required by Stage 2 portfolio reads.
- Keep contracts expressed exclusively in domain types and purpose-built application results.
- Extend architecture tests to enforce that contracts expose no EF Core type, persistence entity, `DbSet`, or `IQueryable`.

## Acceptance Criteria

- Each Stage 2 aggregate root has an intent-oriented repository contract.
- Write contracts carry the expected aggregate version where concurrency applies.
- Conflict outcomes are explicit and provider-neutral.
- Query contracts return immutable projections.
- Architecture tests prove no persistence implementation type crosses the contract boundary.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 build
```

## Completion Notes

Not completed.

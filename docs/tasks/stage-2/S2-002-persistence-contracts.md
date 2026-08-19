---
schema_version: 1
id: S2-002
title: Define persistence contracts and results
stage: 2
status: done
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

Completed 2026-08-19.

- Added intent-oriented repository contracts for all eight Stage 2 aggregate roots, including expected-version parameters on mutable writes.
- Added provider-neutral success, uniqueness-conflict, and concurrency-conflict results plus the application-facing unit-of-work contract.
- Added immutable portfolio, position, ledger, and decision-snapshot projections and their query-service contract.
- Added architecture tests covering aggregate contract completeness, expected-version writes, explicit results, immutable read projections, and exclusion of EF Core, `Trading.Data`, `DbSet`, and `IQueryable` types.
- Corrected the existing Stage 1 acceptance inspection to scan the Stage 1 `Foundation` features after Stage 2 specifications introduced intentionally ignored scenarios in a separate directory.

Validation:

- `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` — passed, 11 tests.
- `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter 'Name=RunTheStage1ExecutableSpecificationsOnTheCurrentSupportedPlatform'` — passed, 1 test.
- `.\dev.ps1 build` — passed in Release with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed: 334 tests; 20 intentionally deferred Stage 2 scenarios skipped.
- `.\dev.ps1 format` — passed.

Deviations: none.

Follow-up tasks: none.

ADRs: none.

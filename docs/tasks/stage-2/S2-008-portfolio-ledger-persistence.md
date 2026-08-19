---
schema_version: 1
id: S2-008
title: Persist Portfolios, Positions, and ledger entries
stage: 2
status: planned
priority: 800
type: feature
depends_on: [S2-006, S2-007]
labels: [portfolio, positions, ledger]
created: 2026-08-19
updated: 2026-08-19
---

# S2-008: Persist Portfolios, Positions, and Ledger Entries

## Objective

Implement exact, constrained persistence for governed portfolios, current positions, and append-only accounting facts.

## Context

Follow [Domain Model — Portfolio](../../domain.md#5-portfolio) and [Data Model — Portfolio Tables](../../data-model.md#6-portfolio-tables).

## Scope

- Map Portfolios, Positions, position applied-fill markers, and Portfolio Ledger Entries with explicit configurations.
- Implement repositories with aggregate reconstruction, expected-version writes, intent-oriented ledger append operations, and applied-fill idempotency.
- Enforce one active Portfolio per Broker Account and one active Trading Bot per Portfolio through domain checks and partial unique indexes.
- Preserve exact financial values, base currency, capital policy, timestamps, lifecycle state, and versions.
- Enforce unique Position identity per Portfolio/Instrument and retain zero-quantity Positions.
- Enforce unique ledger source identity and append-only entries.
- Implement correction entries that reference and compensate an existing entry.
- Add positive, negative, round-trip, uniqueness, idempotency, and restricted-delete integration tests.

## Acceptance Criteria

- Portfolio, Position, and ledger state reloads exactly.
- A Broker Account cannot own two active Portfolios.
- A Trading Bot cannot own two active Portfolio assignments.
- Duplicate fill application and duplicate ledger source identity produce no duplicate financial change.
- Ledger entries cannot be updated or deleted through normal repository operations.
- Corrections append a compensating entry and preserve the original entry.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=PortfolioPersistence"
.\dev.ps1 build
```

## Completion Notes

Not completed.

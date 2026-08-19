---
schema_version: 1
id: S2-011
title: Implement no-tracking portfolio read models
stage: 2
status: done
priority: 740
type: feature
depends_on: [S2-009]
labels: [queries, projections, no-tracking]
created: 2026-08-19
updated: 2026-08-19
---

# S2-011: Implement No-Tracking Portfolio Read Models

## Objective

Provide efficient immutable projections for Stage 2 portfolio inspection and demonstrations.

## Context

Follow [Data Model — Read Models](../../data-model.md#18-read-models), [Initial Index Set](../../data-model.md#16-initial-index-set), and the query boundaries in [Domain Model](../../domain.md#12-repository-boundaries).

## Scope

- Implement no-tracking query services for Portfolio summaries, Position views, ledger history, Broker Account association, and Decision Snapshot history.
- Return immutable projection records with strongly typed identities, exact financial values, UTC timestamps, lifecycle state, and concurrency version where relevant.
- Add deterministic ordering and bounded pagination for collection queries.
- Add query filters for Portfolio, Broker Account, Trading Bot, Instrument, and time range.
- Verify generated SQL uses `AsNoTracking` behavior and the defined indexes for primary list queries.
- Add integration tests for projection accuracy, ordering, filtering, pagination, and query plans.

## Acceptance Criteria

- Query results exactly match persisted state without loading aggregate graphs.
- Query execution leaves the EF change tracker empty.
- Ordering and pagination are deterministic for equal timestamps.
- Primary Portfolio, Position, ledger, and snapshot queries use their intended indexes under `EXPLAIN QUERY PLAN`.
- Query contracts expose no EF Core type or `IQueryable`.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=PortfolioReadModels"
.\dev.ps1 build
```

## Completion Notes

Implemented EF Core no-tracking query services and immutable provider-neutral projections for Portfolio summaries, Positions, ledger history, Broker Account associations, and Decision Snapshot history. Added bounded offset pagination, deterministic timestamp-and-identity ordering, and Portfolio, Broker Account, Trading Bot, Instrument, and inclusive UTC time-range filters. Existing Stage 2 indexes satisfied the primary query plans without schema changes.

Validation performed on 2026-08-19:

- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=PortfolioReadModels"` — passed 4 tests.
- `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` — passed 11 tests.
- `.\dev.ps1 build` — succeeded in Release with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed 426 tests: Core 275, Data 92, Architecture 11, and Acceptance 48; 20 explicitly deferred Stage 2 acceptance scenarios were skipped.
- `.\dev.ps1 format` — passed with no changes required.

No deviations, follow-up tasks, or ADR changes.

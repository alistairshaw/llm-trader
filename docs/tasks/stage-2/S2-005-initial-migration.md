---
schema_version: 1
id: S2-005
title: Create and verify the initial persistence migration
stage: 2
status: done
priority: 860
type: infrastructure
depends_on: [S2-004]
labels: [migration, schema, constraints]
created: 2026-08-19
updated: 2026-08-19
---

# S2-005: Create and Verify the Initial Persistence Migration

## Objective

Create the initial SQLite schema required for the complete Stage 2 persistence slice.

## Context

Implement the Stage 2 tables and constraints from [Data Model](../../data-model.md), following its [Migration Order](../../data-model.md#19-migration-order) and [Delete, Retention, and Immutability](../../data-model.md#15-delete-retention-and-immutability) rules.

## Scope

- Create tables for Broker Connections, Broker Accounts, Instruments, Instrument Broker Mappings, Trading Bots, Trading Bot Configuration Versions, Portfolios, Positions, position applied-fill markers, Portfolio Ledger Entries, Portfolio Decision Snapshots, and schema metadata.
- Add every required primary key, foreign key, check constraint, unique index, partial unique index, query index, concurrency version, and `ON DELETE RESTRICT` rule for those tables.
- Add the initial EF Core migration and model snapshot.
- Add an empty SQLite Stage 1 upgrade fixture containing no application tables.
- Add migration tests for a new database and the empty Stage 1 fixture.
- Add schema assertions for tables, columns, indexes, foreign keys, delete actions, migration history, and schema metadata.

## Acceptance Criteria

- The initial migration applies successfully to a new database.
- The same migration upgrades the empty Stage 1 fixture successfully.
- Reapplying migrations is idempotent.
- Schema inspection matches every Stage 2 table and constraint in scope.
- Financial and audit relationships use restricted deletion.
- Migration tests run on Windows and Linux through the standard suite.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Migrations"
.\dev.ps1 build
```

## Completion Notes

Completed 2026-08-19.

- Added the complete Stage 2 EF Core model configuration, initial migration, model snapshot, schema metadata version record, and pinned repository-local `dotnet-ef` tool.
- Added all scoped tables, columns, primary and foreign keys, restricted delete actions, enum and integrity checks, concurrency tokens, unique and query indexes, and partial one-to-one indexes.
- Added a tracked empty Stage 1 SQLite fixture and migration integration coverage for fresh creation, fixture upgrade, idempotent reapplication, migration history, schema metadata, and exhaustive schema inspection.

Validation:

- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Migrations"` — passed, 3 tests.
- `.\dev.ps1 build` — passed in Release with 0 warnings and 0 errors.
- `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` — passed; no pending model changes.
- `.\dev.ps1 test` — passed: 385 tests; 20 intentionally deferred Stage 2 acceptance scenarios skipped.
- `.\dev.ps1 format` — passed.

Deviations: none.

Follow-up tasks: none.

ADRs: none.

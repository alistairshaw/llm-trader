---
schema_version: 1
id: S2-005
title: Create and verify the initial persistence migration
stage: 2
status: ready
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

Not completed.

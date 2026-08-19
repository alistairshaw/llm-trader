---
schema_version: 1
id: S2-003
title: Configure EF Core and SQLite infrastructure
stage: 2
status: ready
priority: 900
type: infrastructure
depends_on: [S2-002]
labels: [ef-core, sqlite, docker]
created: 2026-08-19
updated: 2026-08-19
---

# S2-003: Configure EF Core and SQLite Infrastructure

## Objective

Establish the production and test infrastructure for SQLite persistence through EF Core.

## Context

Implement [Data Model — SQLite Storage Conventions](../../data-model.md#3-sqlite-storage-conventions), [Concurrency and SQLite Operation](../../data-model.md#14-concurrency-and-sqlite-operation), and [Local Development](../../local-development.md).

## Scope

- Add centrally pinned EF Core SQLite, design-time, and test dependencies with committed lock files.
- Add the application `DbContext` and explicit `DbSet` declarations inside `Trading.Data`.
- Add typed database options requiring an absolute runtime database path outside the source tree.
- Configure foreign keys, bounded busy timeout, WAL mode, migration history, and sensitive-data-safe logging.
- Add database initialization that applies explicit migrations and fails startup on migration failure.
- Add an isolated temporary-SQLite fixture for `Trading.Data.Tests` that creates a unique database per test and disposes it deterministically.
- Add `Trading.Data.Tests` to the solution and standard test workflow.

## Acceptance Criteria

- Production configuration cannot place a live database inside the repository tree.
- Each data test receives an isolated real SQLite database.
- SQLite foreign keys are enabled for every opened connection.
- WAL and busy-timeout settings are verified through integration tests.
- Startup applies migrations without calling `EnsureCreated`.
- Restore and build run entirely through the Docker workflow.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Infrastructure"
```

## Completion Notes

Not completed.

---
schema_version: 1
id: S2-003
title: Configure EF Core and SQLite infrastructure
stage: 2
status: done
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

Completed 2026-08-19.

- Pinned EF Core SQLite and design-time dependencies at 10.0.10, including patched transitive security pins, and refreshed all affected lock files.
- Added `TradingDbContext` with explicit Stage 2 `DbSet` declarations, validated database options, a safe context factory, per-connection SQLite configuration, and migration-based initialization.
- Enforced absolute runtime database paths outside the repository tree and bounded busy-timeout values before filesystem or database access.
- Added `Trading.Data.Tests` to the solution with a deterministic async temporary-database fixture using a unique real SQLite database for each test.
- Added infrastructure integration tests for path safety, provider selection, isolation, foreign keys, WAL, busy timeout, migration initialization, and invalid option rejection.
- Added `-RefreshLocks` to the standard restore wrapper so dependency lock updates remain an explicit Docker workflow operation.

Validation:

- `.\dev.ps1 restore -RefreshLocks` — passed and refreshed affected lock files.
- `.\dev.ps1 restore` — passed in locked mode.
- `.\dev.ps1 build` — passed in Release with 0 warnings and 0 errors.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Infrastructure"` — passed, 5 tests.
- `.\dev.ps1 test` — passed: 339 tests; 20 intentionally deferred Stage 2 scenarios skipped.
- `.\dev.ps1 format` — passed.

Deviations: none.

Follow-up tasks: none.

ADRs: none.

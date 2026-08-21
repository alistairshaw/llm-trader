---
schema_version: 1
id: S5-016
title: Make SQLite fixture and host disposal Windows-safe
stage: 5
status: ready
priority: 1100
type: defect
depends_on: [S5-014]
labels: [windows, sqlite, testing, resource-lifetime, ci]
created: 2026-08-20
updated: 2026-08-20
---

# S5-016: Make SQLite Fixture and Host Disposal Windows-Safe

## Objective

Make every file-backed SQLite test and headless-host fixture release all database and host resources before deleting its temporary directory on Windows and Linux.

## Context

Hosted validation of revision `88681e512dcbde8f04a3e2865722f5646f0b073f` passed Linux job `96622973099` and the security secret scan, but Windows job `96622973230` failed during teardown. Artifact `9429220872` records `IOException` for locked `test.db`, `runtime.db`, `smoke.db`, `workflow.db`, `capital.db`, `research.db`, and `recovery.db` files across Data, Integration, Acceptance, and Host tests.

Use [Test Plan — Data Integration Tests](../../test-plan.md#6-data-integration-tests), [Test Plan — Host Integration Tests](../../test-plan.md#8-host-integration-tests), [Test Plan — Steps and Drivers](../../test-plan.md#103-steps-and-drivers), [Local Development](../../local-development.md), and [Architecture — Host Lifecycle](../../architecture.md#8-runtime-and-concurrency-model).

## Scope

- Inventory every temporary file-backed SQLite fixture and Generic Host test owner in Data, Integration, Acceptance, and Host smoke coverage.
- Give each fixture one explicit asynchronous ownership boundary that disposes child scopes, EF Core contexts, SQLite connections, service providers, hosts, and related streams before directory deletion.
- Await host stop and asynchronous disposal paths before releasing database files.
- Clear the applicable Microsoft.Data.Sqlite connection pool after owned connections are closed where pooled handles otherwise retain a Windows file lock; document the ownership reason at that call site.
- Centralize repeated safe cleanup behavior when the same lifecycle exists in multiple fixtures.
- Add deterministic tests that create, use, dispose, and immediately delete representative migrated databases and hosted workflows.
- Preserve failed-test diagnostics without retaining live handles.
- Update documentation whose fixture-lifecycle guidance or commands change.

## Acceptance Criteria

- Every temporary database owner releases its file handle before recursive directory cleanup on Windows and Linux.
- Data, Integration, Acceptance, and Host tests delete their temporary database directories immediately on the first cleanup attempt.
- Cleanup correctness uses deterministic resource ownership and disposal; it contains no sleeps, cleanup retries, ignored exceptions, conditional skips, or process-wide masking of leaked connections.
- Connection-pool clearing is limited to owned, closed SQLite connections and has a documented justification.
- New regression coverage fails if a representative database, scope, context, connection, service provider, or host remains live at deletion time.
- The complete local Docker suite passes with zero failed or skipped tests.
- The exact pushed repair revision passes the complete hosted Windows and Linux jobs and the security workflow.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 format
.\dev.ps1 test -Project tests/Trading.Data.Tests
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.AcceptanceTests
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 test
.\dev.ps1 run
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
```

## Completion Notes

Pending implementation.

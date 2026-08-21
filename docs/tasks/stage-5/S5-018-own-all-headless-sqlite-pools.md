---
schema_version: 1
id: S5-018
title: Own every headless smoke SQLite pool and context
stage: 5
status: ready
priority: 1100
type: defect
depends_on: [S5-017]
labels: [windows, sqlite, host, diagnostics, resource-lifetime, ci]
created: 2026-08-20
updated: 2026-08-20
---

# S5-018: Own Every Headless Smoke SQLite Pool and Context

## Objective

Enumerate and deterministically release every SQLite connection-string identity, pool, factory, context, connection, and background owner that can access the HostBootstrap `smoke.db` before first-attempt deletion.

## Context

Hosted candidate `737103afcdb71b8e04b5c90394b7adc2b782f7b6` passed Linux job `96637323989` and security validation. Windows job `96637323821` failed only `HeadlessHostTests.SmokeModeMigratesSeedsRunsAndStopsCleanly`; artifact `9430857531` reports `smoke.db` locked at `SqliteTestDatabaseCleanup.DeleteOwnedDirectory` line 16. S5-017 already closes the inspection connection and asynchronously disposes the host/root provider before exact-pool cleanup, proving another pool identity or owner exists.

Use [Architecture — Host Lifecycle](../../architecture.md#8-runtime-and-concurrency-model), [Data Model — EF Core and SQLite](../../data-model.md), [Test Plan — Host Integration Tests](../../test-plan.md#8-host-integration-tests), and [Local Development — SQLite Fixture Lifecycle](../../local-development.md).

## Scope

- Instrument the complete HostBootstrap smoke composition to enumerate each normalized SQLite connection string and its owner, including registration factories, DbContext options/factories, scoped contexts, explicit connections, hosted/background services, and test inspection connections.
- Establish one canonical absolute `smoke.db` connection identity and use it consistently for production composition, inspection, and exact pool release, or explicitly own and release every intentionally distinct identity.
- Dispose every created scope, context, factory-owned context, explicit connection, hosted/background service, root provider, and host before clearing its exact closed pool.
- Add bounded diagnostic assertions that identify the database path, normalized connection identity, and remaining owner when first-attempt deletion fails, without exposing secrets.
- Add a production-composed regression that migrates and runs the full smoke workflow, proves all recorded owners are closed, clears every exact owned pool, and deletes `smoke.db` and its directory on the first attempt.
- Preserve successful, cancelled, and failed host lifecycle disposal.
- Update lifecycle documentation with the canonical connection identity and ownership contract.

## Acceptance Criteria

- Every HostBootstrap component that can open `smoke.db` is enumerated with a deterministic owner and disposal boundary.
- Equivalent connection strings normalize to the same canonical absolute database identity before pool ownership and cleanup decisions.
- Every intentionally distinct exact pool is cleared only after all of its owned connections and contexts are closed.
- The focused production-composed regression proves immediate first-attempt `smoke.db` directory deletion and emits bounded ownership diagnostics on failure.
- Cleanup contains no `ClearAllPools`, sleeps, retry loops, skipped assertions, swallowed exceptions, garbage-collection forcing, or process-wide masking.
- `HeadlessHostTests.SmokeModeMigratesSeedsRunsAndStopsCleanly`, all fixture-disposal regressions, and successful/cancelled/failed host lifecycle tests pass.
- The complete local Docker suite passes with zero failed or skipped tests.
- The exact pushed repair revision passes the complete hosted Windows and Linux jobs and the security workflow.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 format
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "FullyQualifiedName~HeadlessHostTests|Category=FixtureDisposal"
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "FullyQualifiedName~MultiBotSupervisor"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 test
.\dev.ps1 run
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
```

## Completion Notes

Pending implementation.

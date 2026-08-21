---
schema_version: 1
id: S5-017
title: Release the headless smoke database on host disposal
stage: 5
status: done
priority: 1100
type: defect
depends_on: [S5-016]
labels: [windows, sqlite, host, resource-lifetime, ci]
created: 2026-08-20
updated: 2026-08-20
owner: s5_017
---

# S5-017: Release the Headless Smoke Database on Host Disposal

## Objective

Make the production-composed headless smoke host release `smoke.db` completely before its test fixture performs the first recursive directory deletion on Windows and Linux.

## Context

Hosted validation of revision `9ff1e6b58b5810266365c1eb5a6b19914e40ea0b` passed Linux job `96633639723` and the security secret scan. Windows job `96633639864` passed every test except `HeadlessHostTests.SmokeModeMigratesSeedsRunsAndStopsCleanly`. Artifact `9430431487` records teardown `IOException` because `smoke.db` remained in use. The S5-016 scoped-provider, acceptance-driver, integration-fixture, and other database cleanup repairs passed, isolating this defect to the complete HostBootstrap smoke lifecycle.

Use [Architecture — Host Lifecycle](../../architecture.md#8-runtime-and-concurrency-model), [Test Plan — Host Integration Tests](../../test-plan.md#8-host-integration-tests), [Test Plan — Fixture Lifecycle](../../test-plan.md#6-data-integration-tests), and [Local Development — Repository Support](../../local-development.md#repository-support).

## Scope

- Trace ownership of the HostBootstrap smoke host, root service provider, hosted/background services, child scopes, TradingDbContext instances, SQLite connections, and the exact `smoke.db` pool through shutdown and disposal.
- Make host shutdown and asynchronous disposal await the complete release of every owned service and database resource.
- Ensure the fixture disposes the root provider/host and clears only the exact closed `smoke.db` pool after all owners are released.
- Preserve idempotent shutdown and disposal for successful, cancelled, and failed smoke execution.
- Add a focused regression that runs the complete production-composed smoke host, disposes it, and deletes `smoke.db` and its directory immediately on the first attempt.
- Update lifecycle documentation if the production HostBootstrap ownership contract changes.

## Acceptance Criteria

- `HeadlessHostTests.SmokeModeMigratesSeedsRunsAndStopsCleanly` releases `smoke.db` before its first deletion attempt on Windows and Linux.
- The focused regression exercises HostBootstrap with its real background-service and scoped-persistence composition and asserts immediate directory removal after asynchronous disposal.
- Host stop, provider disposal, DbContext/connection closure, and exact-pool release occur in a deterministic documented ownership order.
- Cleanup contains no sleeps, retry loops, skipped assertions, swallowed exceptions, garbage-collection forcing, or process-wide pool clearing.
- Successful, cancelled, and failed host lifecycles remain idempotently disposable.
- The complete local Docker suite passes with zero failed or skipped tests.
- The exact pushed repair revision passes the complete hosted Windows and Linux jobs and the security workflow.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 format
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "FullyQualifiedName~HeadlessHostTests|Category=FixtureDisposal"
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 test
.\dev.ps1 run
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
```

## Completion Notes

Completed 2026-08-20.

- Traced the remaining hosted Windows lock to C# scope ordering in `HeadlessHostTests`: its `finally` deletion ran before method-scoped host and inspection-connection disposals. The production host, hosted service, supervisor, scoped EF services, and explicit SQLite reader are now released before exact-pool cleanup and first-attempt directory deletion.
- Made the executable `HostBootstrap.RunAsync` own and asynchronously dispose the built Generic Host/root provider on successful, cancelled, and failed runs. Direct-host tests close inspection connections and asynchronously dispose the host before clearing only the owned `smoke.db` connection pool.
- Retained the production-composed HostBootstrap regression in `SqliteFixtureDisposalTests`; it starts the real background service, awaits smoke shutdown, disposes the complete host, clears the exact closed pool, and proves immediate directory removal without sleeps, retries, skipped assertions, swallowed failures, GC forcing, or process-wide pool clearing.
- Updated local-development lifecycle guidance. No README, AGENTS, architecture, test-plan, ADR, migration, or dependency change was required.
- Validation passed: locked restore; Release build with zero warnings/errors; HeadlessHost/FixtureDisposal focused integration 5/5; MultiBotSupervisor 8/8; Data 149/149; Integration 27/27; Stage 5 acceptance 32/32; Acceptance 165/165; full suite 1000/1000 with zero skipped; formatting verification; EF pending-model drift verification; and the deterministic proposal-governance headless smoke.
- Hosted Windows/Linux and security validation of the exact pushed revision remains the resumed `S5-015` stage-review responsibility. No follow-up task was created.

---
schema_version: 1
id: S7-020
title: Release WPF SQLite ownership on lifecycle stop
stage: 7
status: review
priority: 990
type: defect
depends_on: [S7-019]
labels: [wpf, sqlite, lifecycle, windows]
created: 2026-08-22
updated: 2026-08-22
---
# S7-020: Release WPF SQLite Ownership on Lifecycle Stop

## Objective
Release the WPF host's exact SQLite database ownership synchronously with completed lifecycle disposal on Windows and Linux.

## Context
Windows CI run `32605316561`, job `97109412699`, failed both `WpfHostLifecycleTests` deletion assertions because
`trading.db` remained in use after `TradingApplicationLifecycle.StopAsync`. Follow [Local Development](../../local-development.md),
[Test Plan](../../test-plan.md), and the `HostDatabaseIdentity` ownership diagnostics.

## Scope
- Close and asynchronously dispose every WPF host scope, context, connection, service provider, and host owner before lifecycle stop completes.
- Clear only the closed pool identified by `HostDatabaseIdentity.ConnectionString` when the owned SQLite pool requires explicit release.
- Keep `StopAsync` and `DisposeAsync` idempotent for normal stop, startup failure, cancellation, and repeated calls.
- Add Windows-applicable integration coverage that deletes the exact database and directory immediately on the first attempt without sleeps, retries, garbage collection, swallowed failures, or process-wide pool clearing.
- Preserve bounded ownership diagnostics without exposing credentials.

## Out of Scope
- WPF UI Automation, page objects, selectors, and journey bindings.
- Deterministic Proposal, Order, Fill, warning, or execution-mode fixture content.

## Acceptance Criteria
- Both `WpfHostLifecycleTests` that failed in Windows run `32605316561` pass on Windows and Linux.
- After awaited lifecycle stop or disposal, the owned `trading.db` and its directory delete immediately on the first attempt.
- No unrelated SQLite pool or database is cleared or deleted.
- Build, focused lifecycle tests, full tests, and format pass with zero warnings, failures, or skips.

## Validation
- `./dev.ps1 build`
- `./dev.ps1 test -Project tests/Trading.IntegrationTests/Trading.IntegrationTests.csproj -Filter "TestCategory=WpfHostLifecycle"`
- `./dev.ps1 test`
- `./dev.ps1 format`
- Windows CI exact lifecycle selection

## Completion Notes
Implemented on 2026-08-22. `TradingApplicationLifecycle` now captures the canonical `HostDatabaseIdentity`, awaits host shutdown and asynchronous root-provider disposal, and then clears only the exact closed SQLite pool before publishing `Stopped`. The ordering remains idempotent across normal stop, repeated stop/disposal, cancellation, startup failure, and disposal failure. Bounded ownership diagnostics and local-development guidance now name the lifecycle pool boundary.

The two original lifecycle tests now delete the complete owned directory immediately after awaited stop. A new regression keeps an unrelated pooled SQLite connection open and usable while the lifecycle releases and deletes only its owned database directory. No sleeps, retries, garbage collection, swallowed deletion failures, or process-wide pool clearing were added.

Validation passed in Linux Docker: `./dev.ps1 restore`; `./dev.ps1 build` (zero warnings and errors); `./dev.ps1 test -Project tests/Trading.IntegrationTests/Trading.IntegrationTests.csproj -Filter "TestCategory=WpfHostLifecycle"` (5/5); `./dev.ps1 test` (1,230/1,230, zero skipped); `./dev.ps1 format`; and `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` (no pending model changes). The exact Windows lifecycle selection remains delegated to hosted CI, so the task is in review rather than done. No scope deviations, follow-up tasks, or ADR changes.

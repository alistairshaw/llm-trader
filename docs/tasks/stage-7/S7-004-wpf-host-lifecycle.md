---
schema_version: 1
id: S7-004
title: Compose WPF with the Generic Host
stage: 7
status: done
priority: 920
type: infrastructure
depends_on: [S7-002]
labels: [wpf, host, lifecycle]
created: 2026-08-22
updated: 2026-08-22
---
# S7-004: Compose WPF with the Generic Host

## Objective
Start and stop the production Generic Host from WPF with bounded ownership.

## Context
Use [Windows Desktop](../../architecture.md#81-windows-desktop) and [Local Development](../../local-development.md).

## Scope
- Replace `StartupUri` with async host-owned startup.
- Reuse production Engine, Data, Research, and paper composition.
- Complete migrations and recovery before readiness.
- Surface startup/shutdown state and dispose host, scopes, and SQLite ownership once within a bound.
- Add lifecycle and immediate fixture-cleanup tests.

## Out of Scope
None.

## Acceptance Criteria
- No ready window appears before migration and recovery finish.
- Close, cancellation, failure, and deadline paths dispose ownership exactly once.
- The test database deletes immediately after lifecycle completion on Windows.

## Validation
Build; HostLifecycle WPF tests; WpfHostLifecycle integration tests; full tests; publish-wpf; format.

## Completion Notes
Added the cross-platform `Trading.Composition` boundary so the headless and WPF entry points reuse the exact production
Engine, Data, Research, and simulated-paper registration graph. WPF now starts asynchronously without `StartupUri`,
waits for migration and all recovery services to publish readiness before showing its window, reports startup failure,
and performs deadline-bounded, idempotent shutdown and disposal. Runtime readiness now exposes explicit starting, ready,
failed, and stopped states. Integration tests exercise real migrated SQLite readiness, cancellation, failure, exact-once
ownership disposal, and immediate database deletion.

Validation:

- `.\dev.ps1 restore -RefreshLocks` — passed; lock files refreshed for `Trading.Composition`.
- `.\dev.ps1 build` — passed with 0 warnings and 0 errors.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=HostLifecycle|Category=WpfHostLifecycle"` — 3 passed, 0 failed, 0 skipped.
- `.\dev.ps1 test` — 1,159 passed, 0 failed, 4 skipped. The four skips remain the Stage 7 scenarios assigned to S7-016.
- `.\dev.ps1 format` — passed.
- `.\dev.ps1 publish-wpf` — not locally applicable yet; S7-014 owns introduction of this wrapper target and its
  self-contained `win-x64` test profile.

The immediate database deletion assertion passed in Linux Docker and remains delegated to Windows CI for platform
confirmation. No follow-up tasks or ADRs.

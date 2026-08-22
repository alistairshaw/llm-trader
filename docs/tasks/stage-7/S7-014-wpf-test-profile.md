---
schema_version: 1
id: S7-014
title: Publish deterministic WPF test profile
stage: 7
status: done
priority: 800
type: infrastructure
depends_on: [S7-013]
labels: [wpf, publish, fixtures]
created: 2026-08-22
updated: 2026-08-22
owner: codex-s7-014
---
# S7-014: Publish Deterministic WPF Test Profile

## Objective
Produce a self-contained Windows artifact backed by an isolated paper-only fixture.

## Context
Use [Local Development](../../local-development.md) and [UI Testability](../../test-plan.md#111-ui-testability-requirements).

## Scope
- Complete `publish-wpf` and `run-wpf` for self-contained `win-x64` output built through Docker.
- Add a validated profile with temporary migrated SQLite, deterministic IDs/clock/scripts, fixture Research, and simulated broker.
- Seed operator journeys and expose bounded readiness/shutdown signals.
- Keep and redact runtime artifacts outside source control; test layout, isolation, readiness, and first-attempt cleanup.

## Out of Scope
None.

## Acceptance Criteria
- A clean checkout publishes/launches without host .NET.
- The profile has no live/network/real-LLM/credential authority.
- Closing releases process and database on the first bounded attempt.

## Validation
Restore; build; publish-wpf; WpfTestProfile tests; format.

## Completion Notes
Implemented Docker-built, locked, self-contained `win-x64` publishing and a host-side `run-wpf` launcher that requires
no host .NET installation. The ignored publish layout includes a paper-only authority manifest. Each launch owns a
unique `%LOCALAPPDATA%` runtime directory, migrated SQLite database, deterministic clock and identities, two seeded
Bot/Portfolio operator journeys, fixture Research, and the simulated paper broker. Atomic bounded readiness, failure,
and shutdown signals contain only schema, run identity, state, and a sanitized diagnostic code. Normal close awaits
host and SQLite disposal before the launcher removes the run directory on its first attempt. Host launch validation
also found and corrected the shell route selection binding so the published window can be shown.

Validation:

- `./dev.ps1 restore` — passed in locked mode.
- `./dev.ps1 build` — passed in Release with zero warnings and errors.
- `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests -Filter "TestCategory=WpfTestProfile"` — 2/2 passed.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "TestCategory=WpfTestProfile"` — 1/1 passed with a fresh migrated SQLite database, two stable seeded Portfolios, fixed clock, and first-attempt cleanup.
- `./dev.ps1 publish-wpf` — passed in Linux Docker; produced 296 files including `Trading.UI.Wpf.exe` and the authority manifest.
- Published executable Windows-host probe — exited 0 after `ready`, a normal main-window close, `stopped`, and first-attempt runtime-directory deletion; no host .NET tooling was used.
- `./dev.ps1 test` — 1,223 passed, 0 failed, and the four previously declared Stage 7 non-UI scenarios remained skipped for S7-016.
- `./dev.ps1 format` — passed.

No scope deviations, follow-up tasks, or ADRs.

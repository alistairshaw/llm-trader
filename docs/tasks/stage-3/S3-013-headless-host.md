---
schema_version: 1
id: S3-013
title: Run configured bots through the headless host
stage: 3
status: done
priority: 700
type: feature
depends_on: [S3-012]
labels: [host, docker, configuration, health]
created: 2026-08-19
updated: 2026-08-19
---

# S3-013: Run Configured Bots Through the Headless Host

## Objective

Compose the Stage 3 runtime in `Trading.Host` and operate it through Docker in simulated mode.

## Context

Implement [Architecture — Trading.Host](../../architecture.md#66-tradinghost), [Headless Runtime](../../architecture.md#82-headless-windows-or-linux), and [Configuration and Secrets](../../architecture.md#15-configuration-and-secrets).

## Scope

- Replace the placeholder host with .NET Generic Host composition for database initialization, repositories, runtime services, scheduler, supervisor, recovery, scripted model client, logging, and lifetime.
- Bind and validate database, scheduler, lease, concurrency, shutdown, configured-Bot, and scripted-session options through standard configuration precedence.
- Default every local and container profile to simulated `ResearchOnly` execution.
- Add startup checks for migrations, configured Bot existence, active configuration, Portfolio assignment, snapshot availability, and runtime option validity.
- Add structured lifecycle logs and readiness state with Bot, Run, host-instance, and correlation identities.
- Implement `dev.ps1 run` through Docker Compose and return the host exit code.
- Add a deterministic smoke mode that seeds fixture state, executes configured scripted Bots, reports terminal outcomes, and exits cleanly.

## Acceptance Criteria

- `dev.ps1 run` starts the cross-platform headless host without host .NET tooling.
- Invalid configuration fails before scheduler activation with actionable diagnostics.
- Startup migration and recovery complete before readiness becomes true.
- Configured scripted Bots run in isolated simulated mode.
- `SIGINT`, `SIGTERM`, and cancellation stop the host through the graceful shutdown service.
- Logs contain no credential, model secret, or unredacted tool payload.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 run
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=HeadlessHost"
```

## Completion Notes

Implemented the cross-platform Generic Host composition root with validated simulated-mode configuration, SQLite migration and expired-lease recovery before readiness, configured-Bot startup checks, scripted sessions, bounded supervision, structured lifecycle logs, and graceful shutdown. Deterministic smoke mode recreates its isolated `smoke.db`, seeds one enabled ResearchOnly Bot with an active configuration, Portfolio, and snapshot, runs it through the durable trigger/orchestration/tool path, verifies a completed terminal outcome, and exits. `dev.ps1 run` now builds and runs the published Docker image and returns its exit code.

Validation completed on 2026-08-19:

- `./dev.ps1 restore -RefreshLocks` — passed.
- `./dev.ps1 build` — passed in Release with zero warnings and zero errors.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=HeadlessHost"` — 2 passed, 0 failed, 0 skipped.
- `./dev.ps1 run` — passed; Docker image built, migrations applied, the smoke Bot completed, and shutdown completed within the deadline.
- `./dev.ps1 test` — 624 passed, 0 failed; 26 previously declared Stage 3 acceptance scenarios remain pending for S3-014.
- `./dev.ps1 format` — passed.

No scope deviations, follow-up tasks, or ADR changes.

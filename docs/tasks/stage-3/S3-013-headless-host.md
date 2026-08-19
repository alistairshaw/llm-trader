---
schema_version: 1
id: S3-013
title: Run configured bots through the headless host
stage: 3
status: ready
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

Not completed.

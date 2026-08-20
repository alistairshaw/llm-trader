---
schema_version: 1
id: S4-013
title: Run shared Research through the headless host
stage: 4
status: planned
priority: 710
type: infrastructure
depends_on: [S4-011, S4-012]
labels: [research, hosting, supervisor, smoke]
created: 2026-08-20
updated: 2026-08-20
---

# S4-013: Run Shared Research Through the Headless Host

## Objective

Compose and supervise the shared Research service with Trading Bots in the cross-platform headless host.

## Context

Use [Architecture — Trading.Host](../../architecture.md#66-tradinghost), [Research Bot — Shared Service Model](../../research-bot.md#3-shared-service-model), [Local Development](../../local-development.md), and [Test Plan — Component and Integration Tests](../../test-plan.md#8-component-and-integration-tests).

## Scope

- Register Research repositories, request service, catalog, fixture source providers, scripted model client, dispatcher, orchestrator, bounded supervisor, recovery, and pending-notification processing.
- Bind and validate safe Research options for capacity, polling, budgets, fixture set, and pinned versions at startup.
- Start migrations and recovery before accepting Research work, propagate host cancellation, isolate failures, and shut down cleanly.
- Add deterministic smoke mode demonstrating two Bots sharing one public report, private access denial, report refresh/versioning, notification-triggered Bot runs, and graceful shutdown.

## Acceptance Criteria

- Default local execution is fixture-backed and ResearchOnly and requires no credential or network connection.
- Global Research capacity is bounded and one failed or slow request does not stop unrelated Research or Trading Bot work.
- Startup recovery completes before new work is claimed and shutdown leaves every attempt and notification in a recoverable durable state.
- `dev.ps1 run` prints stable smoke identities and outcomes for shared reuse, isolation, versioning, notification, and shutdown.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ResearchHost"
.\dev.ps1 run
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

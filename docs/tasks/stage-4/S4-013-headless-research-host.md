---
schema_version: 1
id: S4-013
title: Run shared Research through the headless host
stage: 4
status: done
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

Completed 2026-08-20.

- Composed the Research repositories, fixture source, scripted model factory, authorized catalog and artifact adapters, tool dispatcher, publisher, request service, bounded supervisor, recovery, and durable notification delivery in `Trading.Host`.
- Added startup-validated fixture-only Research options for capacity, batches, budgets, recovery age, fixture version, and model/prompt/tool/report-schema pins. Migrations and Trading/Research recovery complete before readiness and work claims.
- Extended deterministic smoke mode with two fixed ResearchOnly Bot identities. It proves shared request coalescing, a private report access denial, a refresh published as immutable version 2, durable subscription-trigger delivery, and recoverable graceful shutdown without credentials or network access.
- Corrected refresh metadata reconstruction to deserialize the canonical persistence envelope; the prior direct JSON-root read lost the predecessor ID and incorrectly started a new report series.
- Updated README, architecture, Research Bot, local-development, and agent workflow documentation. Refreshed affected NuGet lock files after adding the Host-to-Research project reference.
- Validation: `.\dev.ps1 build` passed with zero warnings and errors; ResearchHost integration tests passed 3/3; Research tests 55/55, Engine 57/57, Data 130/130, Integration 23/23, and Architecture 15/15 passed; the full suite passed 765 with the 39 intentionally pending S4-014 acceptance scenarios and no failures; Stage 4 migration/drift tests passed 5/5; `.\dev.ps1 format` passed.
- `.\dev.ps1 run` passed in Linux Docker. Smoke evidence included Bot A `01J5QH8M000000000000000101`, Bot B `01J5QH8M000000000000000201`, shared report `01J5QH8M000000000000000501`, shared hash `c288b6f376c0e943d867dfa236417ecbd3b5dbc0c7362869a27d73c491d3db83`, `First=Queued`, `Second=Subscribed`, `PrivateDenied=True`, refreshed report `01J5QH8M000000000000000503`, `LatestVersion=2`, `InitialRuns=1`, and `Shutdown=recoverable`.
- No deviations, follow-up tasks, or ADRs.

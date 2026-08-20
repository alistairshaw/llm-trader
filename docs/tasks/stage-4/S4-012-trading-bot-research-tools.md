---
schema_version: 1
id: S4-012
title: Add Trading Bot Research tools and report consumption
stage: 4
status: done
priority: 730
type: feature
depends_on: [S4-005, S4-010]
labels: [trading-bot, research, tools, isolation]
created: 2026-08-20
updated: 2026-08-20
owner: s4_012
---

# S4-012: Add Trading Bot Research Tools and Report Consumption

## Objective

Allow a Trading Bot to request asynchronous Research and list or read only authorized immutable reports.

## Context

Use [Trading Bot — Research tools](../../trading-bot.md#84-research-tools), [Isolation and Concurrency](../../trading-bot.md#12-isolation-and-concurrency), [Research Bot — Shared Service Model](../../research-bot.md#3-shared-service-model), and [Test Plan — Security and Authorization Tests](../../test-plan.md#14-security-and-authorization-tests).

## Scope

- Register strict schema version `1` Trading Bot tools `RequestResearch`, `ListReports`, and `GetReport` alongside the Stage 3 tools.
- Authorize each call against the pinned Bot configuration, Bot identity, report visibility, research-request and tool budgets, source/report scope, freshness, and exact report version.
- Return asynchronous request/subscription status from `RequestResearch`, authorized metadata and freshness from `ListReports`, and one immutable exact version from `GetReport`.
- Include authorized report catalog metadata in deterministic Bot input and audit exact request and consumed report versions.
- Consume durable Research terminal triggers through the existing scheduling and coalescing workflow.

## Acceptance Criteria

- A Bot cannot request outside its pinned policy or access another Bot's private or unauthorized restricted report.
- Equivalent requests use the shared request service and do not synchronously run Research inside a Trading Bot session.
- Exact report reads return canonical immutable content and provenance with current freshness metadata.
- Research tools cannot alter reports, Bot configuration, budgets, visibility, proposals, orders, or broker state.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ResearchTools"
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=TradingBotAccess"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=TradingBotResearch"
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

- Added strict version `1` `RequestResearch`, `ListReports`, and `GetReport` contracts alongside the Stage 3 tool surface. Dispatch binds calls to the active Bot Run and pinned configuration, enforces per-tool, total-tool, Research-request, source-provider, identity, visibility, freshness, and exact-version boundaries, and records canonical start/terminal audit payloads including the exact report version returned.
- `RequestResearch` delegates to the shared asynchronous request/deduplication service and returns queued, subscribed, or reused state. Deterministic Bot input now includes only catalog metadata authorized for that Bot at the pinned snapshot time. Existing durable completion/failure notification delivery remains the sole wake mechanism and is covered by the Stage 4 integration suite.
- Updated `README.md`, `AGENTS.md`, and the Trading Bot authority document to define the implemented three-tool contract and remove the obsolete synchronous/status-tool wording.
- Validation: `./dev.ps1 build` passed with zero warnings and errors; focused Engine ResearchTools passed 5/5; Research TradingBotAccess passed 7/7; Data TradingBotAccess plus Stage4Migrations/model-drift passed 9/9; Integration TradingBotResearch passed 1/1; all Research tests passed 55/55; all Data tests passed 130/130; all Integration tests passed 22/22; architecture tests passed 15/15; the full suite passed 764 with 39 intentionally pending Stage 4 acceptance scenarios and no failures; `./dev.ps1 format` passed after applying the repository formatter in Docker.
- Deviations: none. Follow-up tasks: none. ADRs: none.

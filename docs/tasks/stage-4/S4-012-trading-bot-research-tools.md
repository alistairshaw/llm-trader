---
schema_version: 1
id: S4-012
title: Add Trading Bot Research tools and report consumption
stage: 4
status: ready
priority: 730
type: feature
depends_on: [S4-005, S4-010]
labels: [trading-bot, research, tools, isolation]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.

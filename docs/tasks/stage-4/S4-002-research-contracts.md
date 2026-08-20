---
schema_version: 1
id: S4-002
title: Define Research runtime and publication contracts
stage: 4
status: ready
priority: 930
type: feature
depends_on: [S4-001]
labels: [research, domain, contracts]
created: 2026-08-20
updated: 2026-08-20
---

# S4-002: Define Research Runtime and Publication Contracts

## Objective

Define the domain lifecycle and provider-neutral contracts required to execute and audit shared Research.

## Context

Use [Domain Model — Research](../../domain.md#7-research), [Research Bot](../../research-bot.md), [Architecture — Trading.Research](../../architecture.md#65-tradingresearch), and [Test Plan — Unit Tests](../../test-plan.md#5-unit-tests).

## Scope

- Complete `ResearchRequest`, subscription, Research run-attempt, and report lifecycle behavior with exhaustive transition rules.
- Define contracts for normalized request specifications, authorization principals and restricted groups, subscribers, pinned model/prompt/tool-set/report-schema versions, budgets, usage, scripted model sessions, source results, tool calls, drafts, reports, catalog queries, notifications, clocks, identifiers, and cancellation.
- Define stable terminal outcomes and validation, authorization, tool, publication, and recovery result codes.
- Enforce bounded questions, UTC timestamps, immutable request meaning, subscriber authorization, private-input visibility narrowing, and published-report immutability.

## Acceptance Criteria

- Table-driven tests cover every permitted and forbidden request, run, subscription, visibility, and report-disposition transition.
- Every Research run pins exact model, prompt, tool-set, and report-schema versions and records deterministic resource usage.
- Research contracts expose no proposal, approval, reservation, order, broker, EF Core, SQLite, WPF, or arbitrary-code authority.
- `Trading.Core` remains platform-neutral and `Trading.Research` depends only on approved application-owned abstractions and `Trading.Core`.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=Research"
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=Contracts"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

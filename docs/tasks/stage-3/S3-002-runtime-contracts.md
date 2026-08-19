---
schema_version: 1
id: S3-002
title: Define runtime, model, and tool contracts
stage: 3
status: ready
priority: 930
type: feature
depends_on: [S3-001]
labels: [contracts, llm, tools, clock]
created: 2026-08-19
updated: 2026-08-19
---

# S3-002: Define Runtime, Model, and Tool Contracts

## Objective

Define provider-neutral contracts and immutable messages for deterministic Trading Bot execution.

## Context

Follow [Trading Bot — Authority Boundary](../../trading-bot.md#2-authority-boundary), [Run Workflow](../../trading-bot.md#7-run-workflow), [Tool Contract](../../trading-bot.md#8-tool-contract), and [Architecture — Concurrency and Messaging](../../architecture.md#14-concurrency-and-messaging).

## Scope

- Add an injectable UTC clock, delay abstraction, host-instance identity, and typed runtime identifier generation contracts.
- Add `Trading.Engine.Tests` as a `net10.0` NUnit project in the solution with shared test conventions, a reference to `Trading.Engine`, and committed locked dependencies.
- Extend the Bot Run lifecycle with `Pending`, `AcquiringLease`, `PreparingSnapshot`, `Reasoning`, `WaitingForTool`, `Completed`, `TimedOut`, `BudgetExceeded`, `Cancelled`, and `Faulted`, including explicit allowed and forbidden transitions.
- Add provider-neutral model request, assistant response, tool-call, usage, and normalized failure records.
- Add an asynchronous model-session contract that yields one assistant response at a time and accepts tool results.
- Add typed tool definitions, arguments, results, schema versions, authorization outcomes, and dispatcher contracts.
- Define the production Stage 3 tools as `GetPortfolioSnapshot` and `Finish`.
- Add run-result, schedule-decision, budget-decision, lease-result, trigger-claim, shutdown, and recovery result records.
- Extend architecture tests to keep provider SDK types and infrastructure implementations outside runtime contracts.

## Acceptance Criteria

- Contracts use only application-owned immutable records and domain types.
- Every asynchronous operation accepts a `CancellationToken`.
- Bot Run lifecycle tests cover every allowed and forbidden transition, and identify all five active runtime states used by the persistence index.
- Model failures are normalized into explicit timeout, malformed-response, provider-failure, and cancellation outcomes.
- Tool calls carry a name, schema version, canonical arguments, and stable invocation identity.
- Runtime contracts expose no provider SDK, EF Core, WPF, broker SDK, `IQueryable`, or service locator.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 build
```

## Completion Notes

Not completed.

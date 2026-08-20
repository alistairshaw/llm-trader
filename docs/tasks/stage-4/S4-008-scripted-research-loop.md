---
schema_version: 1
id: S4-008
title: Implement the scripted bounded Research loop
stage: 4
status: ready
priority: 810
type: feature
depends_on: [S4-007]
labels: [research, llm, budgets, deterministic-testing]
created: 2026-08-20
updated: 2026-08-20
---

# S4-008: Implement the Scripted Bounded Research Loop

## Objective

Execute deterministic Research model sessions within every pinned resource and failure boundary.

## Context

Use [Research Bot — Research Lifecycle](../../research-bot.md#6-research-lifecycle), [Budgets and Scheduling](../../research-bot.md#12-budgets-and-scheduling), [Test Plan — LLM Testing](../../test-plan.md#12-llm-testing-and-evaluation), and [Architecture — Resilience and Recovery](../../architecture.md#17-resilience-and-recovery).

## Scope

- Implement a scripted Research model client and bounded message/tool loop using the authorized dispatcher.
- Enforce wall-clock, token, cost, total/per-tool calls, documents retrieved, retained bytes, consecutive failures, global concurrency cancellation, and pinned run identity.
- Require one valid draft submission and one valid `FinishResearch` result before the loop can produce a publication candidate.
- Persist exact pinned versions, model messages required for audit, cumulative usage, stable terminal reason, and bounded redacted failure details.

## Acceptance Criteria

- Every individual budget boundary terminates with its specified safe outcome and publishes no report.
- Missing draft, missing finish, malformed model output, repeated tool failures, timeout, provider failure, and cancellation terminate deterministically without inferring completion.
- Retries are bounded and cannot duplicate draft submission or other material effects.
- Scripted tests require no real model, credentials, network, wall-clock wait, or mutable external data.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=ModelLoop|Category=Budgets"
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

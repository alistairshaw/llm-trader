---
schema_version: 1
id: S4-008
title: Implement the scripted bounded Research loop
stage: 4
status: done
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

Completed on 2026-08-20.

- Added a deterministic scripted Research model session and bounded loop over the authorized Stage 4 dispatcher. The loop preserves pinned attempt/version identity, accumulates model and tool usage, returns a byte-bounded canonical audit transcript, persists the terminal attempt, and produces a publication candidate only after one valid draft and one valid `FinishResearch` result.
- Enforced wall-clock, token, cost, total/per-tool call, document, retained-byte, transcript, iteration, consecutive-failure, cancellation, provider, malformed-response, missing-draft, and missing-finish boundaries. One-shot draft and finish effects are rejected before redispatch.
- Added table-driven loop tests and classified the existing dispatcher per-tool/total/document/byte policy test in the budget suite. Updated `README.md`, Research Bot authority, and the test plan. Reviewed `AGENTS.md`; its existing canonical bounded-audit, deterministic-client, and build-before-test rules remain accurate, so no change was required.
- Validation: `.\dev.ps1 build` passed with zero warnings and errors; focused ModelLoop/Budgets tests passed 18/18; all Research tests passed 48/48; Data tests passed 120/120; architecture tests passed 15/15; full suite passed 739 with 39 intentionally pending Stage 4 acceptance scenarios and no failures; `.\dev.ps1 format` passed. Migration drift was not affected because this task added no persistence model or migration change.
- Deviations: none. Follow-up tasks: none. ADRs: none.

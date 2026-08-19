---
schema_version: 1
id: S3-009
title: Implement the scripted bounded model loop
stage: 3
status: ready
priority: 780
type: feature
depends_on: [S3-008]
labels: [llm, scripted, budgets, deterministic]
created: 2026-08-19
updated: 2026-08-19
---

# S3-009: Implement the Scripted Bounded Model Loop

## Objective

Execute deterministic scripted model sessions while enforcing every Stage 3 run budget and safe terminal outcome.

## Context

Implement [Trading Bot — Run Budgets and Failure Policy](../../trading-bot.md#9-run-budgets-and-failure-policy) and [Test Plan — LLM Testing and Evaluation](../../test-plan.md#12-llm-testing-and-evaluation).

## Scope

- Implement `ScriptedLlmClient` with ordered expected requests, assistant messages, tool calls, usage, delays, malformed responses, provider failures, and cancellation steps.
- Implement the provider-neutral loop that sends deterministic input, dispatches authorized tools, returns tool results, accumulates usage, and stops on `Finish`.
- Enforce wall-clock, token, cost, total tool-call, per-tool, research-request, proposal, and consecutive-failure limits before the next model or tool action.
- Reject non-zero research-request or proposal usage in Stage 3.
- Terminate with explicit Completed, TimedOut, BudgetExceeded, Cancelled, or Faulted results.
- Persist the schema-versioned canonical transcript and cumulative usage.
- Add table-driven tests for every exact boundary, first-over-limit case, missing `Finish`, malformed output, provider failure, cancellation, and zero-action completion.

## Acceptance Criteria

- The same script and inputs produce the same transcript, tool history, usage, and result.
- No model or tool action occurs after a budget is exhausted.
- Missing `Finish` creates no inferred action and terminates safely.
- Malformed and failed model responses become explicit safe terminal results.
- The scripted client never accesses a network or external model.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ScriptedModelLoop"
.\dev.ps1 build
```

## Completion Notes

Not completed.

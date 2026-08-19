---
schema_version: 1
id: S3-009
title: Implement the scripted bounded model loop
stage: 3
status: done
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

Implemented the provider-neutral `BoundedModelLoop` and deterministic `ScriptedLlmClient`. The loop records a schema-versioned canonical assistant/tool transcript, accumulates model and tool usage, enforces wall-clock, token, cost, tool, research, proposal, iteration, and consecutive-failure limits before further actions, and produces stable safe terminal results for missing `Finish`, malformed/provider responses, cancellation, timeout, and budget exhaustion. Script steps support ordered request expectations, responses, usage, delays, provider faults, and cancellation without network access. `BotRun` now exposes guarded model-progress and terminal-reason audit mutations that are persisted by the existing repository mapping.

Validation completed on 2026-08-19:

- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ScriptedModelLoop"` — passed 16, failed 0, skipped 0.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests` — passed 45, failed 0, skipped 0.
- `./dev.ps1 build` — succeeded in Release with 0 warnings and 0 errors.
- `./dev.ps1 test` — passed 604, failed 0, skipped 26 pending Stage 3 acceptance scenarios.
- `./dev.ps1 format` — passed with no formatter or analyzer findings.

No persistence schema changed, so an EF migration drift check was not applicable. No scope deviations, follow-up tasks, or ADR changes.

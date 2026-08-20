---
schema_version: 1
id: S5-011
title: Orchestrate proposal validation and approval
stage: 5
status: done
priority: 760
type: feature
depends_on: [S5-005, S5-007, S5-008, S5-009, S5-010]
labels: [engine, orchestration, proposals, recovery]
created: 2026-08-20
updated: 2026-08-20
---

# S5-011: Orchestrate Proposal Validation and Approval

## Objective

Coordinate recorded proposals through deterministic validation, execution-mode disposition, human decision, fresh revalidation, and capital reservation.

## Context

Use [Architecture — Core Execution Flow](../../architecture.md#9-core-execution-flow), [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution), and [Data Model — Transaction Boundaries](../../data-model.md#15-transaction-boundaries).

## Scope

- Process recorded proposals idempotently after the Bot model session commits.
- Acquire fresh state, append the initial evaluation, and transition each proposal according to policy outcome and pinned execution mode.
- Coordinate authorized human decisions, post-approval fresh-state revalidation, and atomic reservation.
- Expire eligible proposals and release associated reservations through recoverable scheduled processing.
- Persist stable workflow outcomes, correlations, and bounded diagnostics at every material boundary.
- Keep model, market/provider, and other external I/O outside database transactions.

## Acceptance Criteria

- Valid HumanApproval proposals await an authorized decision, revalidate against newer state, and reserve capital only after the fresh evaluation passes.
- Invalid, expired, cancelled, changed-state, ResearchOnly, and contention cases reach deterministic durable outcomes.
- Restart and retry reconstruct work from durable state without duplicate decisions, evaluations, or reservations.
- The orchestration graph ends at a reserved approved proposal and exposes no broker submission call.
- One proposal failure leaves unrelated Bot and Portfolio processing available.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalOrchestration"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ProposalGovernance"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented `ProposalGovernanceOrchestrator` and provider-neutral orchestration contracts for initial validation,
authorized exact-review decisions, post-approval fresh-state revalidation, atomic reservation, bounded failures,
and recoverable expiration. Passing revalidation preserves an existing approval; failed revalidation rejects the
proposal while preserving immutable approval and evaluation history. The workflow ends at reservation and has no
order or broker dependency. README, architecture, and Trading Bot documentation describe the boundary.

Validation completed in Linux Docker:

- `.\dev.ps1 build` — passed with 0 warnings and 0 errors.
- `.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalOrchestration"` — 6 passed.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ProposalGovernance"` — 2 passed.
- `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` — 19 passed.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage5Migrations"` — 5 passed, including model drift.
- `.\dev.ps1 test` — 959 passed, 32 expected pending Stage 5 acceptance cases, 0 failed.
- `.\dev.ps1 format` — passed.

The full suite discovered that the pre-existing headless smoke path created its manual trigger with the wall clock
while the scheduler used the injected fixed clock. The scoped correction routes trigger time through `IUtcClock`;
the focused `HeadlessHostTests` suite then passed 3 tests and the full suite passed. No ADRs or follow-up tasks were
required. No model, market-provider, order, or broker operation was added to a database transaction.

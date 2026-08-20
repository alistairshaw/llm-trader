---
schema_version: 1
id: S5-007
title: Persist immutable guardrail evaluations
stage: 5
status: done
priority: 840
type: feature
depends_on: [S5-004, S5-006]
labels: [risk, audit, evaluations, persistence]
created: 2026-08-20
updated: 2026-08-20
owner: s5_007
---

# S5-007: Persist Immutable Guardrail Evaluations

## Objective

Run the guardrail hierarchy against pinned state and append a complete immutable evaluation to each proposal decision.

## Context

Use [Domain Model — TradeProposal Aggregate](../../domain.md#81-tradeproposal-aggregate), [Data Model — Guardrail Evaluations](../../data-model.md#93-guardrail_evaluations), and [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution).

## Scope

- Build fresh evaluation inputs with exact account, Portfolio, market, proposal, policy, configuration, snapshot, evidence, and active-reservation references.
- Execute the hierarchy and append monotonically sequenced evaluations with canonical ordered rule results.
- Transition proposals to rejection or the execution-mode-specific eligible state using optimistic concurrency.
- Persist every evaluation attempt with stable failure diagnostics and bounded redaction.
- Update domain, data-model, test-plan, README, and AGENTS.md documentation when implementation changes normative behavior or validation workflow.

## Acceptance Criteria

- Every validation decision records policy versions, state references, rule results, outcome, sequence, and UTC time.
- Revalidation appends a new evaluation while preserving every earlier evaluation.
- Concurrent validation yields one accepted lifecycle transition and reconstructable conflict outcomes.
- Persistence rollback leaves proposal state and evaluation history mutually consistent.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=GuardrailEvaluation"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=GuardrailEvaluationPersistence"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented immutable, monotonically sequenced hierarchical evaluation artifacts and an atomic evaluate-and-persist application service. Each artifact preserves all 44 ordered rule results, every policy identity/version, proposal content version/hash, configuration version, fresh snapshot/time/hash, disposition code, UTC evaluation time, and a canonical SHA-256 input identity. Exact retries return the existing artifact; changed state revalidates by appending; optimistic concurrency reports a stable conflict and SQLite/EF guards reject updates and deletes. Passing evaluations advance to `AwaitingHumanApproval`; failures transition to `Rejected`.

Added migrations `20260820221702_AddImmutableGuardrailEvaluationArtifacts` and `20260820222346_RestoreGuardrailEvaluationImmutabilityTriggers`. The separate trigger-restoration migration is required because SQLite table rebuilds can discard triggers created in the same EF migration batch. Existing evaluation rows receive deterministic unique hashes during upgrade. Updated README, AGENTS.md, domain, data-model, and all migration-contract fixtures.

Validation: `./dev.ps1 build` passed with 0 warnings/errors; focused Engine evaluation tests passed 2/2; focused Stage 5 migration and evaluation persistence tests passed 6/6; full suite passed 933 with 32 intentionally pending Stage 5 acceptance scenarios; `./dev.ps1 format` passed. Golden input hash: `34e2dd5a6a57e2343b46aff397817c604b96d75efa8dd4a5657c7e7f6881c41b`.

Deviations: none. Follow-ups: none. ADRs: none.

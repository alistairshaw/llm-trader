---
schema_version: 1
id: S5-007
title: Persist immutable guardrail evaluations
stage: 5
status: ready
priority: 840
type: feature
depends_on: [S5-004, S5-006]
labels: [risk, audit, evaluations, persistence]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.

---
schema_version: 1
id: S5-001
title: Write Stage 5 executable Gherkin specifications
stage: 5
status: ready
priority: 1000
type: acceptance
depends_on: []
labels: [bdd, proposals, risk, planning]
created: 2026-08-20
updated: 2026-08-20
---

# S5-001: Write Stage 5 Executable Gherkin Specifications

## Objective

Define executable business specifications for every Stage 5 proposal-governance acceptance criterion.

## Context

Use [Implementation Plan — Stage 5](../../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk), [Domain Model — Trade Proposals](../../domain.md#8-trade-proposals), [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution), [Data Model — Trade Proposal Tables](../../data-model.md#9-trade-proposal-tables), and [Test Plan — Gherkin Acceptance Tests](../../test-plan.md#10-gherkin-acceptance-tests).

## Scope

- Add tagged features for direct-trade and target-allocation proposals, exact identity and evidence references, schema validation, portfolio assignment, hierarchical guardrails, immutable evaluations, human decisions, fresh-state revalidation, capital reservations, contention, release, expiration, and ResearchOnly behavior.
- Specify the scripted headless demonstration for valid and invalid proposals and concurrent capital demand.
- Add traceability from every Stage 5 criterion to named scenarios and implementing tasks.
- Generate discoverable Reqnroll tests and mark implementation-dependent scenarios with the acceptance harness temporary pending tag; `S5-014` activates them.

## Acceptance Criteria

- Every Stage 5 criterion maps to at least one named business-facing scenario.
- Scenario language identifies exact Bot Run, configuration, Portfolio, snapshot, proposal version, evidence version, policy version, evaluation state, actor, and reservation outcomes where applicable.
- Tags identify Stage 5, proposals, risk, concurrency, recovery, and applicable platforms.
- The Stage 5 filter discovers every scenario with implementation-dependent scenarios explicitly pending.
- Every scenario uses deterministic scripted inputs, injected time and identifiers, migrated temporary SQLite, and simulated application boundaries.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

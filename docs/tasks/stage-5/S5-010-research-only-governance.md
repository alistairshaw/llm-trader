---
schema_version: 1
id: S5-010
title: Enforce ResearchOnly proposal governance
stage: 5
status: done
priority: 780
type: feature
depends_on: [S5-005, S5-007]
labels: [research-only, safety, proposals]
created: 2026-08-20
updated: 2026-08-20
owner: s5_010
---

# S5-010: Enforce ResearchOnly Proposal Governance

## Objective

Record and evaluate ResearchOnly proposals while keeping them permanently non-executable.

## Context

Use [Trading Bot — Execution Modes](../../trading-bot.md#11-execution-modes), [Architecture — LLM Authority](../../architecture.md#9-core-execution-flow), and [Implementation Plan — Stage 5](../../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk).

## Scope

- Route ResearchOnly proposal tools through the same schema, identity, evidence, persistence, and guardrail pipeline.
- Record the pinned execution mode and stable non-executable outcome in proposal history and projections.
- Reject approval, reservation, conversion, and execution eligibility commands for ResearchOnly proposals with stable audited codes.
- Prove the ResearchOnly composition graph excludes broker submission authority.

## Acceptance Criteria

- ResearchOnly direct-trade and target-allocation proposals are immutable, queryable, and fully evaluated.
- Every attempt to approve, reserve, or make a ResearchOnly proposal executable returns the documented stable outcome without changing proposal state.
- Architecture and integration tests prove the ResearchOnly workflow has no broker submission dependency or invocation.
- Proposal evidence and private visibility remain isolated by the pinned Bot identity.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ResearchOnlyProposal"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ResearchOnlyProposal"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented pinned execution-mode persistence for direct-trade and target-allocation proposals. ResearchOnly
proposals use the normal authorized tool, evidence, identity, persistence, and complete hierarchical guardrail
pipeline, then a passing evaluation is atomically persisted with the stable
`proposal_governance.research_only` diagnostic and terminal rejected disposition. Human approval, capital
reservation, and order conversion return or throw the stable ResearchOnly outcome without changing proposal
state or reaching reservation/order/broker authority. The pinned mode survives persistence and is unaffected by
later Bot configuration changes. Updated README, AGENTS.md, Architecture, and Trading Bot documentation.

Validation:

- `./dev.ps1 build` — passed; 0 warnings and 0 errors.
- `./dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=ResearchOnlyProposal"` — 3 passed.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ResearchOnlyProposal"` — 2 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ResearchOnlyProposal"` — 1 passed using real SQLite.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ResearchOnlyProposal"` — 1 passed.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests` — 84 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests` — 145 passed.
- `./dev.ps1 test -Project tests/Trading.Architecture.Tests` — 19 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "FullyQualifiedName~Stage5MigrationTests|Category=MigrationDrift"` — 5 passed.
- `./dev.ps1 test` — 953 passed, 32 intentionally pending S5-014 acceptance cases, 0 failed.
- `./dev.ps1 format` — passed.

Deviations: none. Follow-up tasks: none. ADRs: none.

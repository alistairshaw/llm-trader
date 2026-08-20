---
schema_version: 1
id: S5-010
title: Enforce ResearchOnly proposal governance
stage: 5
status: ready
priority: 780
type: feature
depends_on: [S5-005, S5-007]
labels: [research-only, safety, proposals]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.

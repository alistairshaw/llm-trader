---
schema_version: 1
id: S5-006
title: Implement hierarchical guardrail policies
stage: 5
status: ready
priority: 860
type: feature
depends_on: [S5-002]
labels: [risk, guardrails, policy, domain]
created: 2026-08-20
updated: 2026-08-20
---

# S5-006: Implement Hierarchical Guardrail Policies

## Objective

Evaluate proposals deterministically through platform, account, Portfolio, and Bot policy levels.

## Context

Use [Architecture — Core Execution Flow](../../architecture.md#9-core-execution-flow), [Domain Model — Trade Proposals](../../domain.md#8-trade-proposals), [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution), and [Test Plan — Domain Model](../../test-plan.md#51-domain-model).

## Scope

- Define immutable versioned policy sets and deterministic rule contracts for every hierarchy level.
- Compose parent and child limits so each effective child policy is at least as restrictive as its parent.
- Evaluate identity, mandate, instrument eligibility, proposal expiry, position and concentration limits, available capital, price/freshness, liquidity, and market-hours inputs required by configured policies.
- Produce ordered structured rule results with stable rule IDs, policy versions, observed values, thresholds, outcomes, and reason codes.
- Short-circuit executable eligibility on rejection while preserving complete deterministic results required for audit.

## Acceptance Criteria

- Table-driven tests prove hierarchy order and effective-limit composition across platform, account, Portfolio, and Bot policies.
- Property and boundary tests prove a child configuration cannot weaken an inherited restriction.
- Identical canonical inputs and policy versions produce identical ordered rule results.
- Missing, stale, unauthorized, or uncertain state produces a stable restrictive outcome.
- Policy evaluation remains platform-neutral and side-effect free.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=HierarchicalGuardrails"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=GuardrailPipeline"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

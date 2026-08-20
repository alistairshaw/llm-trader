---
schema_version: 1
id: S5-006
title: Implement hierarchical guardrail policies
stage: 5
status: done
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

- Added immutable versioned policy definitions for the platform, account, Portfolio, and Trading Bot hierarchy with monotonic composition of kill switches, eligible universes, maximum position/concentration/price-age limits, minimum capital/liquidity limits, and market-hours requirements.
- Added the pure deterministic evaluator and Engine adapter. The eleven stable rule IDs are `guardrail.authority`, `guardrail.kill_switch`, `guardrail.mandate`, `guardrail.instrument_eligibility`, `guardrail.proposal_expiry`, `guardrail.position_notional`, `guardrail.concentration`, `guardrail.available_capital`, `guardrail.price_freshness`, `guardrail.liquidity`, and `guardrail.market_hours`; every policy level emits the complete ordered set with version, observed value, threshold, outcome, and stable reason code.
- Updated README, Domain Model, and Trading Bot documentation with the durable merge and restrictive unknown-state semantics.
- Validation: `./dev.ps1 build` passed with zero warnings and errors; focused `HierarchicalGuardrails` passed 15/15; focused `GuardrailPipeline` passed 2/2; Core passed 488/488; Engine passed 72/72; architecture passed 18/18; the full suite passed 930 with 32 intentionally pending Stage 5 scenarios and no failures; `./dev.ps1 format` passed.
- Deviations: none. Follow-up tasks: none. ADRs: none.

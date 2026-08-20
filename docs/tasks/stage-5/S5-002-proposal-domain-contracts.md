---
schema_version: 1
id: S5-002
title: Define proposal governance domain and contracts
stage: 5
status: planned
priority: 940
type: feature
depends_on: [S5-001]
labels: [domain, proposals, approvals, reservations]
created: 2026-08-20
updated: 2026-08-20
---

# S5-002: Define Proposal Governance Domain and Contracts

## Objective

Define the domain lifecycles and provider-neutral contracts for proposals, evaluations, approvals, and capital reservations.

## Context

Use [Domain Model — Trade Proposals](../../domain.md#8-trade-proposals), [Architecture — Trading.Engine](../../architecture.md#64-tradingengine), [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution), and [Test Plan — Domain Model](../../test-plan.md#51-domain-model).

## Scope

- Complete `TradeProposal`, `GuardrailEvaluation`, `ProposalApproval`, and `CapitalReservation` models with exhaustive transition rules and stable result codes.
- Define versioned direct-trade and target-allocation actions, exact Report and Hypothesis version references, proposal content versions, policy references, fresh-state references, decision actors, rule results, and reservation amounts.
- Enforce immutable recorded content, UTC expiration, Bot/Run/configuration/Portfolio/snapshot binding, portfolio assignment, evidence visibility, and exact-version approval binding.
- Define application ports for proposal recording, policy evaluation, identity authorization, fresh-state acquisition, capital availability, clocks, identifiers, and transactions.

## Acceptance Criteria

- Table-driven tests cover every permitted and forbidden proposal and reservation transition.
- Tests prove approval binds the exact proposal content version and reviewed state snapshot.
- Direct-trade and target-allocation contracts use exact financial primitives and bounded canonical values.
- Architecture tests prove the domain and application contracts expose proposal governance authority without broker submission authority.
- `Trading.Core` remains platform-neutral.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=ProposalGovernance"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalContracts"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

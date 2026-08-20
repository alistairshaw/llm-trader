---
schema_version: 1
id: S5-005
title: Implement structured proposal tool dispatch
stage: 5
status: ready
priority: 880
type: feature
depends_on: [S5-002, S5-004]
labels: [tools, proposals, authorization, audit]
created: 2026-08-20
updated: 2026-08-20
---

# S5-005: Implement Structured Proposal Tool Dispatch

## Objective

Add authorized `ProposeTrade` version 1 and `ProposeTargetAllocation` version 1 tools to the bounded Trading Bot loop.

## Context

Use [Trading Bot — Tool Contract](../../trading-bot.md#8-tool-contract), [Architecture — Core Execution Flow](../../architecture.md#9-core-execution-flow), and [Test Plan — Engine and Application Policies](../../test-plan.md#52-engine-and-application-policies).

## Scope

- Register exact version 1 schemas for direct-trade and target-allocation proposal tools.
- Validate canonical JSON, required and unknown fields, enum values, exact decimals, identifiers, evidence versions, rationale size, expiration, per-run proposal count, and notional budgets.
- Authorize each call against the pinned Bot identity, configuration, tool policy, Portfolio assignment, decision snapshot, exact Report and frozen Hypothesis version visibility, run state, usage, and cancellation.
- Record proposals idempotently and persist bounded canonical tool audit with stable success and rejection codes.
- Update Trading Bot, architecture, README, and AGENTS.md documentation when implemented behavior or workflow guidance changes.

## Acceptance Criteria

- Valid calls record immutable proposals and return identities without implying approval, reservation, conversion, or execution.
- Invalid schema, authority, identity, evidence, expiration, duplicate, budget, run-state, and cancellation cases return stable codes and durable bounded audit.
- Exact Report and Hypothesis version references are retained on the recorded proposal.
- Architecture tests prove the proposal tool dispatcher cannot resolve or invoke a broker submission port.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalToolDispatch"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalRepositories"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

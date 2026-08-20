---
schema_version: 1
id: S5-005
title: Implement structured proposal tool dispatch
stage: 5
status: done
priority: 880
type: feature
depends_on: [S5-002, S5-004]
labels: [tools, proposals, authorization, audit]
created: 2026-08-20
updated: 2026-08-20
owner: s5_005
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

Implemented `ProposeTrade` version 1 and `ProposeTargetAllocation` version 1 as closed,
canonical schemas layered onto the existing Trading Bot dispatcher. Calls are authorized against the
pinned run, Bot configuration, Portfolio snapshot, tool and proposal budgets, exact visible Report
versions, and optional frozen Hypothesis version. Exact decimal strings, identity, allocation,
expiration, rationale, currency, quantity, and pinned `ProposalNotional` limits are validated with
stable rejection codes. Successful calls idempotently persist immutable proposals and bounded durable
tool audit without exposing approval, reservation, order, broker, or policy-mutation authority.

Registered proposal repositories and the dispatcher in the headless composition root. Updated README,
AGENTS.md, architecture, and Trading Bot documentation. Added focused dispatcher and architecture
tests. Existing real-SQLite proposal repository tests cover the persistence/idempotency boundary.

Validation completed on 2026-08-20:

- `./dev.ps1 build` — passed with zero warnings and errors.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalToolDispatch"` — 9 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalRepositories"` — 5 passed.
- `./dev.ps1 test -Project tests/Trading.Architecture.Tests` — 18 passed.
- `./dev.ps1 test` — 913 passed; 32 Stage 5 acceptance cases remain intentionally pending for S5-014.
- `./dev.ps1 format` — passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "FullyQualifiedName~Stage5MigrationTests"` — 5 passed, including EF model drift.

No deviations, follow-up tasks, or ADRs.

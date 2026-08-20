---
schema_version: 1
id: S5-008
title: Implement authorized human proposal decisions
stage: 5
status: done
priority: 820
type: feature
depends_on: [S5-004, S5-007]
labels: [approvals, authorization, audit]
created: 2026-08-20
updated: 2026-08-20
---

# S5-008: Implement Authorized Human Proposal Decisions

## Objective

Record authorized human approval and rejection decisions against the exact proposal version and reviewed state.

## Context

Use [Domain Model — TradeProposal Aggregate](../../domain.md#81-tradeproposal-aggregate), [Data Model — Proposal Approvals](../../data-model.md#94-proposal_approvals), [Trading Bot — Execution Modes](../../trading-bot.md#11-execution-modes), and [Test Plan — Trade Proposals and Risk](../../test-plan.md#trade-proposals-and-risk).

## Scope

- Implement an application service that authorizes the actor for the proposal's Bot and Portfolio before revealing or deciding it.
- Require proposal ID, exact content version, reviewed state snapshot, decision, bounded reason, actor identity/type, and injected UTC time.
- Reject expired, changed, terminal, unauthorized, duplicated, and stale-review decisions with stable result codes.
- Append immutable decisions and transition eligible proposals atomically with optimistic concurrency.

## Acceptance Criteria

- An accepted decision durably identifies actor, exact proposal version, reviewed state, decision, reason, and time.
- Changed content, changed reviewed state, expiration, terminal state, and unauthorized actors cannot yield an approved proposal.
- Retried identical commands are idempotent; conflicting decisions preserve the first committed history and return stable conflicts.
- Authorization precedes proposal detail disclosure.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=HumanProposalApproval"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalApprovalPersistence"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented an application-owned human-decision boundary that authorizes actor identity and roles before loading
proposal details, binds approval or rejection to the exact immutable content, configuration, latest passing
evaluation, and fresh state, and returns stable outcomes for stale, expired, terminal, unauthorized, duplicate,
and concurrent commands. Identical retries reuse the immutable decision. Conflicting retries preserve the first
committed history. The aggregate now supports exact-state rejection, and SQLite reconstruction restores the
reviewed content and fresh state from immutable proposal and evaluation artifacts.

Validation:

- `./dev.ps1 build` — passed; zero warnings and zero errors.
- `./dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=HumanProposalApproval"` — 2 passed.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=HumanProposalApproval"` — 4 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalApprovalPersistence"` — 1 passed using SQLite.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "FullyQualifiedName~Stage5MigrationTests|Category=MigrationDrift"` — 5 passed.
- `./dev.ps1 test` — 938 passed, 32 intentionally pending Stage 5 acceptance cases, 0 failed.
- `./dev.ps1 format` — passed.

Documentation: clarified the authorization-before-disclosure flow and exact decision reconstruction in
`docs/trading-bot.md` and `docs/data-model.md`. No README or `AGENTS.md` change was required because the public
entry points and repository-wide workflow remain unchanged.

Deviations: no production migration was required; exact reviewed state is reconstructed from already-immutable
proposal content and guardrail evaluation rows. No follow-up tasks or ADRs were created. Hosted Windows validation
remains delegated to the Stage 5 review task.

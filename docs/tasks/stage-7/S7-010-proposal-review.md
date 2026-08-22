---
schema_version: 1
id: S7-010
title: Build Proposal review and human decisions
stage: 7
status: done
priority: 840
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, proposals, approval]
created: 2026-08-22
updated: 2026-08-22
owner: s7-010-agent
---
# S7-010: Build Proposal Review and Human Decisions

## Objective
Let authorized users inspect exact Proposal evidence and approve or reject eligible Proposals.

## Context
Use [Domain Model](../../domain.md), [Trading Bot](../../trading-bot.md), and [Read Models](../../data-model.md#18-read-models).

## Scope
- Build queue/filter/page, structured detail, rationale, evidence, freshness, guardrails, policy versions, decisions, and Reservation state.
- Implement approval/rejection with actor identity, confirmation, and stale/expired/content/concurrency outcomes.
- Refresh authoritative projections after each decision.
- Add accessibility metadata and view-model/application integration tests.

## Out of Scope
None.

## Acceptance Criteria
- Reviewed content/configuration/evaluation/snapshot identities remain visible through decision.
- Unauthorized, expired, stale, changed, and terminal Proposals cannot be approved.
- Success shows immutable actor, time, reason, and resulting Reservation or rejection.

## Validation
Build; ProposalReview WPF tests; OperatorProposalDecision integration tests; full tests; format.

## Completion Notes
Implemented an accessible Proposal queue, paging/filtering, exact detail workspace, immutable rationale/evidence,
reviewed content/configuration/snapshot/evaluation identities, guardrail and policy results, decision history, and
Reservation state. Approval and rejection require explicit confirmation, use the displayed optimistic version and
authorized operator boundary, preserve stable denial/stale/expired/content/concurrency outcomes, and reload both the
authoritative queue and exact detail after success. Approval reasons are normalized and rejection reasons are required.

Validation completed on 2026-08-22 through the Linux Docker workflow:

- `./dev.ps1 restore` — locked restore passed after one Docker Desktop `unexpected EOF` interruption was retried.
- `./dev.ps1 build` — passed with zero warnings and zero errors.
- `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests -Filter "TestCategory=ProposalReview"` — 4 passed.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "TestCategory=OperatorProposalDecision"` — 3 passed.
- `./dev.ps1 format` — passed.
- `./dev.ps1 test` — 1,206 passed, 4 pre-existing Stage 7 acceptance scenarios skipped pending their assigned
  production bindings, and zero failed.

Native Windows compilation and interactive UI automation remain delegated to the Stage 7 hosted validation tasks.
No scope deviations, follow-up tasks, or ADR changes were required.

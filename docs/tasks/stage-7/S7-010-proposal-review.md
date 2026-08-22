---
schema_version: 1
id: S7-010
title: Build Proposal review and human decisions
stage: 7
status: ready
priority: 840
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, proposals, approval]
created: 2026-08-22
updated: 2026-08-22
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
Pending implementation.

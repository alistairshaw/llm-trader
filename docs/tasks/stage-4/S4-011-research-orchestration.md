---
schema_version: 1
id: S4-011
title: Orchestrate Research runs and restart recovery
stage: 4
status: planned
priority: 750
type: feature
depends_on: [S4-010]
labels: [research, orchestration, recovery, audit]
created: 2026-08-20
updated: 2026-08-20
---

# S4-011: Orchestrate Research Runs and Restart Recovery

## Objective

Coordinate each queued Research request through a bounded, auditable, restart-safe terminal outcome.

## Context

Use [Research Bot — Research Lifecycle](../../research-bot.md#6-research-lifecycle), [Auditability](../../research-bot.md#13-auditability), [Architecture — Resilience and Recovery](../../architecture.md#17-resilience-and-recovery), and [Test Plan — Reliability and Recovery Tests](../../test-plan.md#13-reliability-and-recovery-tests).

## Scope

- Claim queued requests, create increasing attempt records, pin versions and budgets, execute the model/tool loop outside database transactions, validate publication, persist the terminal state, and dispatch subscriber outcomes.
- Bound concurrent Research attempts globally while isolating request state, sources, drafts, artifacts, audit, and failure handling.
- On startup, terminalize abandoned attempts with retained partial audit, requeue eligible requests, and resume queued requests and pending notifications without duplicate publication.
- Pass cancellation through all I/O and stop accepting work before graceful shutdown drains or safely terminates active attempts.

## Acceptance Criteria

- One request has at most one active attempt; distinct requests run concurrently only within the configured capacity.
- Completed requests link to exactly one published report; failed, timed-out, budget-exceeded, and cancelled attempts publish none.
- Restart recovery preserves prior attempts and partial audit while producing no duplicate tool effect, report version, notification, or Bot run.
- Every attempt is reconstructable from request/subscriber identities, pinned versions, messages, tools, sources, draft validation, usage, timing, terminal reason, report, and notification outcomes.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=Orchestration|Category=Recovery"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ResearchOrchestration|Category=ResearchRecovery"
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

Pending implementation.

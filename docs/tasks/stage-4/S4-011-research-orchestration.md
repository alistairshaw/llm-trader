---
schema_version: 1
id: S4-011
title: Orchestrate Research runs and restart recovery
stage: 4
status: done
priority: 750
type: feature
depends_on: [S4-010]
labels: [research, orchestration, recovery, audit]
created: 2026-08-20
updated: 2026-08-20
owner: s4_011
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

- Implemented atomic queued-request claims with monotonically increasing attempt numbers, pinned version/budget
  defaults, bounded concurrent draining, model/tool execution outside database transactions, durable model/tool
  transcript reconstruction, deterministic draft/provenance reconstruction, publication, terminal request updates,
  and per-subscriber notification dispatch.
- Added restart recovery that finds bounded orphan batches, retains each abandoned attempt and its partial audit as
  failed with `research.recovery.expired_lease`, and atomically requeues the request for a fresh attempt. Claim,
  publication, report-run uniqueness, and source-keyed notification triggers prevent duplicate durable effects.
- Added Research orchestration/recovery tests, real-SQLite Data claim/recovery tests, and a cross-context Integration
  restart test proving retained partial audit and a single newly numbered attempt.
- Validation: `./dev.ps1 build` passed with 0 warnings and 0 errors; focused Research passed 2/2, Data passed 1/1,
  and Integration passed 1/1; affected suites passed Research 55/55, Data 130/130, and Integration 22/22; the full
  suite passed 759 with 39 intentionally pending Stage 4 acceptance scenarios; `./dev.ps1 format` passed; Stage 4
  migration/model-drift tests passed 5/5. A sandbox Docker-config read failure on one build and one format invocation
  was retried successfully with the approved Docker wrapper permission.
- Updated `README.md`, `AGENTS.md`, the Research Bot authority, and the data-model transaction documentation.
- Deviations: recovery uses the released `Failed` persistence status plus the stable recovery reason instead of the
  domain-only `Recovered` value because the Stage 4 schema intentionally constrains stored terminal statuses. No
  migration was required. Hosted and interactive Windows checks are deferred to the Stage 4 review task.
- Follow-ups: none. ADRs: none.

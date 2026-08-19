---
schema_version: 1
id: S3-005
title: Implement deterministic scheduling policy
stage: 3
status: ready
priority: 860
type: feature
depends_on: [S3-002]
labels: [scheduling, policy, clock]
created: 2026-08-19
updated: 2026-08-19
---

# S3-005: Implement Deterministic Scheduling Policy

## Objective

Compute baseline and requested wake times through deterministic scheduling rules.

## Context

Implement [Trading Bot — Triggers and Scheduling](../../trading-bot.md#5-triggers-and-scheduling) and the Stage 3 scheduling criteria in [Implementation Plan](../../implementation-plan.md#5-stage-3-multi-bot-runtime-and-scheduling).

## Scope

- Extend `SchedulingPolicy` with immutable non-overlapping UTC weekly windows, represented as day of week plus inclusive start and exclusive end times within one UTC day.
- Require at least one scheduling window and represent overnight availability as two explicit windows.
- Compute the next baseline time from the pinned policy and previous accepted or activation time.
- Validate a requested wake time against UTC, current time, minimum delay, maximum delay, permitted windows, Bot lifecycle state, and baseline schedule.
- Return an immutable decision with requested time, accepted time, baseline time, outcome, reason code, and policy inputs.
- Select the earlier eligible time between the bounded request and baseline so a model request cannot disable or delay the baseline.
- Add table-driven boundary tests using an injected clock.

## Acceptance Criteria

- Identical inputs always produce the same decision.
- Requests before the minimum delay are raised to the earliest permitted instant.
- Requests after the maximum delay are reduced to the latest permitted instant without exceeding the baseline.
- Requests outside a window move to the next window opening.
- Paused and retired Bots receive no executable schedule.
- Every adjustment or rejection has a stable reason code.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=SchedulingPolicy"
.\dev.ps1 build
```

## Completion Notes

Not completed.

---
schema_version: 1
id: S3-005
title: Implement deterministic scheduling policy
stage: 3
status: done
priority: 860
type: feature
depends_on: [S3-002]
labels: [scheduling, policy, clock]
owner: s3_005
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

Completed on 2026-08-19.

- Added immutable, ordered, non-overlapping UTC weekly windows to `SchedulingPolicy`, including explicit same-day boundaries and full-week migration behavior for persisted policies created before windows were introduced.
- Added deterministic baseline and requested-wake scheduling with injected UTC time, lifecycle suppression, minimum and maximum bounds, window movement, earlier-of-baseline selection, immutable policy inputs, and stable reason codes.
- Added paused and retired bot lifecycle handling and table-driven boundary coverage for UTC, future-time, minimum, maximum, inclusive opening, exclusive closing, baseline advancement, and missing requests.
- Added canonical persistence coverage proving legacy scheduling-policy JSON loads with the current explicit policy schema version.

Validation:

- `.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=SchedulingPolicy"` — passed 12, failed 0, skipped 0.
- `.\dev.ps1 test -Project tests/Trading.Core.Tests` — passed 367, failed 0, skipped 0.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests` — passed 103, failed 0, skipped 0; includes the model-drift assertion.
- `.\dev.ps1 build` — succeeded with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed 566, failed 0; 26 future Stage 3 acceptance scenarios remained intentionally skipped.
- `.\dev.ps1 format` — passed with no formatter changes required.

No scope deviations, follow-up tasks, or ADRs were required.

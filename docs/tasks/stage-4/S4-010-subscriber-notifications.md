---
schema_version: 1
id: S4-010
title: Deliver durable subscriber notifications and Bot triggers
stage: 4
status: done
priority: 770
type: feature
depends_on: [S4-009]
labels: [research, notifications, triggers, idempotency]
created: 2026-08-20
updated: 2026-08-20
owner: s4_010
---

# S4-010: Deliver Durable Subscriber Notifications and Bot Triggers

## Objective

Durably notify every subscriber of Research completion or failure and trigger eligible Trading Bots once.

## Context

Use [Research Bot — Deduplication and Reuse](../../research-bot.md#5-deduplication-and-reuse), [Auditability](../../research-bot.md#13-auditability), [Trading Bot — Triggers and Scheduling](../../trading-bot.md#5-triggers-and-scheduling), and [Architecture — Concurrency and Messaging](../../architecture.md#14-concurrency-and-messaging).

## Scope

- Create one durable visibility-safe completion or failure notification outcome for each Research subscription.
- Atomically create or coalesce a report-completion or report-failure `BotRunTrigger` before marking a subscription delivered.
- Retry pending delivery with bounded attempts and stable audit results after rollback, contention, cancellation, or restart.
- Include exact request ID, terminal outcome, authorized report version when present, and correlation ID in notification and trigger facts.

## Acceptance Criteria

- Every subscriber obtains one durable terminal outcome and every eligible Bot obtains one coalesced follow-up trigger.
- Duplicate terminal processing, delivery retry, and host restart create no duplicate subscription outcome or duplicate follow-up run.
- A failure delivering to one subscriber does not lose or roll back outcomes already durably delivered to other subscribers.
- Notifications and triggers reveal no private report content or identity to an unauthorized Bot.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ResearchNotifications"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ResearchNotifications|Category=TriggerCoalescing"
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

- Implemented a bounded delivery service and a real-SQLite notification repository. Each subscriber is handled
  independently in an immediate transaction that derives the terminal outcome, checks report visibility, writes
  bounded canonical request/report/version facts to a source-keyed `ResearchCompleted` or `ResearchFailed`
  trigger, and only then records delivery. Existing source-keyed triggers make duplicate processing and restart
  return the original durable outcome.
- Added Data coverage for every failure outcome, atomic delivery, retry idempotency, pending ordering, visibility-
  safe payloads, and non-terminal rollback; Research coverage for bounded retries and subscriber containment; and
  real-SQLite Integration coverage for multi-subscriber restart delivery and pending-trigger coalescing.
- Validation: `./dev.ps1 build` passed with 0 warnings and 0 errors; focused notification tests passed Data 6/6,
  Research 1/1, and Integration/trigger-coalescing 5/5; Engine passed 52/52; architecture passed 15/15; Data passed
  129/129; Research passed 53/53; Integration passed 21/21; the full suite passed 755 with 39 intentionally pending
  Stage 4 acceptance scenarios; `./dev.ps1 format` passed; Stage 4 migration/model-drift tests passed 5/5. An
  initial mistyped `Category=Stage4Migration` filter matched no tests and was immediately corrected to
  `Category=Stage4Migrations`.
- Updated `README.md`, `AGENTS.md`, the Research Bot authority, and the data-model transaction documentation.
- Deviations: no migration was required because S4-003 already supplied notification state/timestamp columns and
  the source-keyed Bot-trigger uniqueness boundary. No externally hosted or interactive Windows checks apply to
  this task.
- Follow-ups: none. ADRs: none.

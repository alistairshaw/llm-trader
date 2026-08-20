---
schema_version: 1
id: S4-010
title: Deliver durable subscriber notifications and Bot triggers
stage: 4
status: planned
priority: 770
type: feature
depends_on: [S4-009]
labels: [research, notifications, triggers, idempotency]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.

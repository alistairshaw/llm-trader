---
schema_version: 1
id: S3-011
title: Implement isolated multi-bot supervision
stage: 3
status: done
priority: 740
type: feature
depends_on: [S3-010]
labels: [multi-bot, concurrency, isolation]
created: 2026-08-19
updated: 2026-08-19
---

# S3-011: Implement Isolated Multi-Bot Supervision

## Objective

Run different Trading Bots concurrently while preserving per-Bot isolation and configured global capacity.

## Context

Implement [Trading Bot — Isolation and Concurrency](../../trading-bot.md#12-isolation-and-concurrency) and [Architecture — Multi-Bot, Portfolio, and Account Isolation](../../architecture.md#11-multi-bot-portfolio-and-account-isolation).

## Scope

- Add validated supervisor options for positive global run concurrency and bounded in-memory queue capacity.
- Implement one bounded `Channel<T>` for eligible Bot work and one logical execution partition per Bot identity.
- Permit concurrent execution for different Bots up to the global limit while serializing work for the same Bot.
- Namespace every run context, scripted session, snapshot, transcript, and diagnostic by Bot and Run identity.
- Contain Bot-specific timeout, budget exhaustion, model failure, and tool failure without cancelling unrelated Bots.
- Apply backpressure without dropping durable triggers.
- Add deterministic concurrency tests using gates rather than timing sleeps.

## Acceptance Criteria

- Two different Bots execute concurrently when capacity is at least two.
- One Bot never has two concurrent run services.
- Global active runs never exceed configured capacity.
- One Bot cannot read another Bot’s configuration, snapshot, transcript, tools, or run result.
- Failure of one Bot does not alter another Bot’s execution or terminal result.
- Queue saturation leaves triggers durable and eligible for later claim.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=MultiBotSupervisor"
.\dev.ps1 build
```

## Completion Notes

- Added validated supervisor options, bounded channel admission, global execution capacity, deterministic
  per-Bot partition dispatch, and observable queue/completion outcomes.
- Preserved durable trigger safety by admitting work before invoking the one-run service; saturated work is
  rejected without claiming a trigger. Supervisor cancellation safely completes queued work.
- Added deterministic gated tests for cross-Bot concurrency, per-Bot serialization, global limits, identity-
  scoped model sessions, fault containment, saturation, and lifecycle behavior.
- Validation passed:
  - `.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=MultiBotSupervisor"` — 5 passed.
  - `.\dev.ps1 test -Project tests/Trading.Engine.Tests` — 50 passed.
  - `.\dev.ps1 test -Project tests/Trading.IntegrationTests` — 14 passed.
  - `.\dev.ps1 build` — succeeded with zero warnings and zero errors.
  - `.\dev.ps1 format` — passed.
  - `.\dev.ps1 test` — 618 passed, 26 expected Stage 3 acceptance scenarios skipped, zero failed.
- Deviation: corrected formatter violations in the S3-010 orchestration test while running the required formatter.
- Follow-up tasks: none. ADRs: none.

---
schema_version: 1
id: S3-011
title: Implement isolated multi-bot supervision
stage: 3
status: planned
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

Not completed.

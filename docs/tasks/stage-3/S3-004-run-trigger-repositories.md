---
schema_version: 1
id: S3-004
title: Implement durable Bot Run, trigger, and lease repositories
stage: 3
status: ready
priority: 890
type: feature
depends_on: [S3-003]
labels: [repositories, leases, triggers, audit]
created: 2026-08-19
updated: 2026-08-19
---

# S3-004: Implement Durable Bot Run, Trigger, and Lease Repositories

## Objective

Persist and reconstruct complete Bot Run aggregates while enforcing durable lease and trigger invariants.

## Context

Follow [Data Model — Repository Contracts](../../data-model.md#12-repository-contracts), [Start a Bot Run](../../data-model.md#131-start-a-bot-run), and [Trading Bot — Triggers and Scheduling](../../trading-bot.md#5-triggers-and-scheduling).

## Scope

- Add provider-neutral repository contracts for Bot Runs and trigger ingestion/claiming.
- Implement Bot Run aggregate reconstruction including triggers, transcript, tool history, usage, finish, schedule, terminal state, and version.
- Implement idempotent sourced-trigger append and ordered unsourced-trigger append.
- Implement one conditional transactional operation that acquires a bot lease, creates the Bot Run, pins configuration and snapshot, and consumes all claimed triggers.
- Implement owner-checked lease renewal, release through terminal persistence, expired-lease discovery, and expected-version writes.
- Translate uniqueness and concurrency failures into typed application results.
- Add real-SQLite tests for exact round trips, append-only audit history, one active run, competing lease acquisition, owner-only renewal, trigger idempotency, and restricted deletion.

## Acceptance Criteria

- Two concurrent claimants cannot acquire an active lease for the same Bot.
- Different Bots can hold active leases concurrently.
- A successful claim atomically creates one run and marks every claimed trigger consumed by it.
- Failed claims leave triggers unconsumed.
- Only the current owner can renew a live lease.
- A reloaded run contains every decision-relevant audit fact in deterministic order.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=BotRuntimePersistence"
.\dev.ps1 build
```

## Completion Notes

Not completed.

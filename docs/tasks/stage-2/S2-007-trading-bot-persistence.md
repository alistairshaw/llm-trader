---
schema_version: 1
id: S2-007
title: Persist Trading Bots and configuration versions
stage: 2
status: planned
priority: 810
type: feature
depends_on: [S2-005]
labels: [bots, configurations, repositories]
created: 2026-08-19
updated: 2026-08-19
---

# S2-007: Persist Trading Bots and Configuration Versions

## Objective

Persist Trading Bot lifecycle state and immutable configuration-version history needed by portfolio ownership and snapshots.

## Context

Follow [Domain Model — TradingBot Aggregate](../../domain.md#41-tradingbot-aggregate) and [Data Model — Bot Management Tables](../../data-model.md#5-bot-management-tables).

## Scope

- Map Trading Bots and configuration versions with explicit `IEntityTypeConfiguration<T>` classes.
- Implement the Trading Bot repository with aggregate reconstruction and version-aware writes.
- Persist active configuration identity, scheduling timestamps, lifecycle state, canonical versioned policy JSON, prompt version, content hash, activation, and supersession timestamps.
- Create a Trading Bot and its initial configuration version atomically while resolving the active-version insertion cycle.
- Enforce unique bot names, monotonically increasing per-bot version numbers, one active configuration, and immutable published configuration content.
- Add round-trip, uniqueness, immutability, transaction, and concurrency integration tests.

## Acceptance Criteria

- A Trading Bot and all configuration history reload with equivalent domain state.
- Initial bot/configuration creation commits atomically.
- Only one configuration version is active after every successful write.
- Published configuration content cannot be updated.
- Content hashes remain stable across save and reload.
- Repository writes return explicit uniqueness and concurrency outcomes.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=TradingBotPersistence"
.\dev.ps1 build
```

## Completion Notes

Not completed.

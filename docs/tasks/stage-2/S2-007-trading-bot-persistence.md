---
schema_version: 1
id: S2-007
title: Persist Trading Bots and configuration versions
stage: 2
status: done
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

Implemented the Trading Bot repository, aggregate reconstruction, canonical policy JSON persistence, stable configuration content hashing, atomic initial bot/configuration creation, version-aware updates, and explicit uniqueness and concurrency results. The initial migration now creates a SQLite trigger that permits activation/supersession metadata changes while rejecting updates to published configuration content. Added real-SQLite integration coverage for round trips, stable hashes, the initial insertion cycle, rollback on uniqueness conflicts, monotonically increasing versions, single-active-version behavior, immutable content, stale writes, and migration/model agreement.

Validation completed on 2026-08-19:

- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=TradingBotPersistence"` — passed, 7 tests.
- `.\dev.ps1 build` — passed in Release with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed: 400 tests; 20 Stage 2 acceptance scenarios remain intentionally pending until their implementing tasks.
- `.\dev.ps1 format` — passed with no changes required.

No scope deviations, follow-up tasks, or ADRs.

---
schema_version: 1
id: S3-007
title: Build deterministic Bot Run input
stage: 3
status: ready
priority: 820
type: feature
depends_on: [S3-004]
labels: [snapshot, rendering, isolation]
created: 2026-08-19
updated: 2026-08-19
---

# S3-007: Build Deterministic Bot Run Input

## Objective

Load and render the exact pinned configuration and Portfolio Decision Snapshot for one Bot Run.

## Context

Implement [Trading Bot — Deterministic Input Snapshot](../../trading-bot.md#6-deterministic-input-snapshot) using the persistence boundaries completed in Stage 2.

## Scope

- Add an application service that loads the Bot, pinned configuration version, assigned Portfolio, and pinned immutable Decision Snapshot by strongly typed identity.
- Verify that every loaded identity belongs to the same Bot and Portfolio assignment.
- Define version `1` of the deterministic model input rendering with explicit identity, trigger, mandate, policy, schedule, snapshot, reconciliation, freshness, financial, and previous-run fields available in Stage 3.
- Use invariant formatting, canonical ordering, UTC timestamps, and the snapshot content hash.
- Store the rendering version and SHA-256 hash in the Bot Run audit record.
- Implement `GetPortfolioSnapshot` to return the pinned snapshot content and metadata without querying another Bot or a newer snapshot.
- Add golden rendering fixtures and isolation tests.

## Acceptance Criteria

- Equivalent inputs produce byte-identical rendering and hash.
- The service rejects mismatched Bot, Portfolio, configuration, and snapshot identities.
- The rendered input contains no credential reference or other Bot’s state.
- `GetPortfolioSnapshot` returns only the run’s pinned immutable snapshot.
- Rendering remains identical across Windows and Linux.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=BotRunInput"
.\dev.ps1 build
```

## Completion Notes

Not completed.

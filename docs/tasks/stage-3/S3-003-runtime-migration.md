---
schema_version: 1
id: S3-003
title: Add Bot Run persistence migration
stage: 3
status: ready
priority: 910
type: infrastructure
depends_on: [S3-002]
labels: [migration, bot-runs, triggers, audit]
created: 2026-08-19
updated: 2026-08-19
---

# S3-003: Add Bot Run Persistence Migration

## Objective

Add the durable schema required for triggers, Bot Runs, leases, model audit, and tool invocations.

## Context

Implement [Data Model — Bot Management Tables](../../data-model.md#5-bot-management-tables), [Migration Order](../../data-model.md#19-migration-order), and [Trading Bot — Audit Record](../../trading-bot.md#13-audit-record).

## Scope

- Add persistence entities and explicit mappings for `bot_run_triggers`, `bot_runs`, and `bot_tool_invocations`.
- Persist trigger source identity, consumption identity, reasons, and timestamps.
- Persist pinned Bot, configuration, and snapshot identities; status; lease owner/expiry; start/completion; finish result; requested/accepted schedule; usage; terminal reason; and version.
- Persist schema-versioned canonical model transcript JSON and deterministic input-rendering version on each Bot Run.
- Persist ordered tool invocations with schema version, canonical arguments, status, timestamps, canonical result or artifact reference, normalized redacted error, and usage.
- Add the unique partial active-run index, trigger idempotency index, tool sequence index, scheduler scan indexes, foreign keys, checks, concurrency token, and restricted delete rules.
- Add the Stage 3 migration, model snapshot, and a Stage 2 fixture copied from the completed Stage 2 schema.
- Test fresh creation, Stage 2 upgrade, idempotent reapplication, schema inspection, and EF model drift.

## Acceptance Criteria

- Fresh and Stage 2 upgrade migrations pass through real SQLite.
- The active-run partial index covers every active runtime status.
- Duplicate sourced triggers and duplicate tool sequence numbers are rejected.
- Audit relationships use restricted deletion.
- The migration preserves every Stage 2 table, relationship, value, and hash fixture.
- EF reports no pending model changes.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage3Migrations"
.\dev.ps1 build
```

## Completion Notes

Not completed.

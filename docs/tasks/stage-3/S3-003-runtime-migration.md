---
schema_version: 1
id: S3-003
title: Add Bot Run persistence migration
stage: 3
status: done
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

- Added explicitly mapped `bot_run_triggers`, `bot_runs`, and `bot_tool_invocations` persistence entities with canonical transcript/input-rendering audit fields, ordered invocation payloads, lease and lifecycle state, schedule/finish/usage data, and application-managed run concurrency versions.
- Added the `AddStage3BotRuntime` migration and updated model snapshot, including the five-state active-run partial unique index, sourced-trigger and tool-sequence uniqueness, scheduler indexes, validation checks, restricted audit foreign keys, and data-format version 3.
- Added a populated completed-Stage-2 SQLite fixture and migration tests that verify fresh creation, idempotent reapplication, semantic preservation of every Stage 2 table row and both content hashes, exact Stage 3 schema, constraint enforcement, restricted deletion, and model drift.
- Updated existing migration/schema assertions and the read-model SQL seed to account for the SQLite table rebuild required by the new restricted `last_completed_run_id` relationship.
- Validation passed: `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage3Migrations"` (5 passed); `.\dev.ps1 build` (zero warnings/errors); `.\dev.ps1 test` (547 passed, 26 intentionally pending Stage 3 scenarios skipped); `.\dev.ps1 format`; and `dotnet ef migrations has-pending-model-changes` through the repository Docker environment (no pending model changes).
- No deviations, follow-up tasks, or ADRs.

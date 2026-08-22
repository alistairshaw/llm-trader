---
schema_version: 1
id: S7-003
title: Implement durable hierarchical kill switches
stage: 7
status: done
priority: 940
type: feature
depends_on: [S7-002]
labels: [safety, persistence, audit]
created: 2026-08-22
updated: 2026-08-22
---
# S7-003: Implement Durable Hierarchical Kill Switches

## Objective
Implement authorized, audited switches at platform, account, Portfolio, and Bot scope.

## Context
Use [Stage 7](../../implementation-plan.md#9-stage-7-wpf-operator-interface), [Domain Model](../../domain.md), and [Data Model](../../data-model.md).

## Scope
- Add explicit scope, state, reason, actor, confirmation, timestamp, and version contracts.
- Persist current state and immutable history in a Stage 7 migration.
- Enforce the most restrictive switch before run admission, approval/reservation/conversion, and submission.
- Add idempotent authorized commands, optimistic concurrency, projections, migration and recovery tests.

## Out of Scope
None.

## Acceptance Criteria
- A switch blocks covered work only within its hierarchy using a stable reason.
- Every change records exact actor, reason, scope, prior/resulting state, and UTC time.
- Restart preserves effective state and history.

## Validation
Build; KillSwitch Core/Engine/Data/integration tests; Stage7 migration/drift; full tests; format.

## Completion Notes
Implemented explicit platform, Broker Account, Portfolio, and Trading Bot switch contracts; a restrictive
checkpoint gate; durable current-state and immutable-history persistence; idempotent writes; optimistic
concurrency; current/history/effective projections; host registration; and the Stage 7 migration.

Validation:
- `docker compose run --rm --no-deps dev dotnet build tests/Trading.Data.Tests/Trading.Data.Tests.csproj --configuration Release --no-restore` — passed with zero warnings and errors.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests/Trading.Data.Tests.csproj -Filter TestCategory=KillSwitch` — 2 passed.
- `.\dev.ps1 test -Project tests/Trading.Engine.Tests/Trading.Engine.Tests.csproj -Filter TestCategory=KillSwitch` — 5 passed.
- `.\dev.ps1 build` — passed with zero warnings and errors.
- `.\dev.ps1 test` — 1,163 passed and the four intentionally pending Stage 7 acceptance scenarios skipped.
- `.\dev.ps1 format` — passed.
- `dotnet ef migrations has-pending-model-changes` inside the development container — no drift.

Deviations: none. Follow-up tasks: none. ADRs: none.

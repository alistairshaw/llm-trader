---
schema_version: 1
id: S7-003
title: Implement durable hierarchical kill switches
stage: 7
status: planned
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
Pending implementation.


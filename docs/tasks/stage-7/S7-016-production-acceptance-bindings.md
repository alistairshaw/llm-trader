---
schema_version: 1
id: S7-016
title: Complete production-backed non-UI acceptance
stage: 7
status: planned
priority: 760
type: test
depends_on: [S7-003, S7-013]
labels: [acceptance, cross-platform]
created: 2026-08-22
updated: 2026-08-22
---
# S7-016: Complete Production-Backed Non-UI Acceptance

## Objective
Activate every cross-platform Stage 7 scenario through production operator services.

## Context
Use [Steps and Drivers](../../test-plan.md#103-steps-and-drivers) and Stage 7 traceability.

## Scope
- Add thin steps and a scenario driver using production Host composition and fresh migrated SQLite.
- Exercise authorization, kill-switch hierarchy/audit, commands, update delivery, and shutdown with deterministic substitutes.
- Observe only authorized queries, command results, update contracts, and lifecycle diagnostics.
- Remove non-UI pending tags and synchronize generated sources.

## Out of Scope
None.

## Acceptance Criteria
- Every non-UI scenario passes twice locally with zero skips.
- Driver has no keyword-derived oracle or direct EF/repository/broker access.
- Diagnostics are bounded, stable, and redacted.

## Validation
Build; Stage7 cross-platform acceptance twice; full tests; format.

## Completion Notes
Pending implementation.


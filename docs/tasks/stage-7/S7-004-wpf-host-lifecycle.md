---
schema_version: 1
id: S7-004
title: Compose WPF with the Generic Host
stage: 7
status: planned
priority: 920
type: infrastructure
depends_on: [S7-002]
labels: [wpf, host, lifecycle]
created: 2026-08-22
updated: 2026-08-22
---
# S7-004: Compose WPF with the Generic Host

## Objective
Start and stop the production Generic Host from WPF with bounded ownership.

## Context
Use [Windows Desktop](../../architecture.md#81-windows-desktop) and [Local Development](../../local-development.md).

## Scope
- Replace `StartupUri` with async host-owned startup.
- Reuse production Engine, Data, Research, and paper composition.
- Complete migrations and recovery before readiness.
- Surface startup/shutdown state and dispose host, scopes, and SQLite ownership once within a bound.
- Add lifecycle and immediate fixture-cleanup tests.

## Out of Scope
None.

## Acceptance Criteria
- No ready window appears before migration and recovery finish.
- Close, cancellation, failure, and deadline paths dispose ownership exactly once.
- The test database deletes immediately after lifecycle completion on Windows.

## Validation
Build; HostLifecycle WPF tests; WpfHostLifecycle integration tests; full tests; publish-wpf; format.

## Completion Notes
Pending implementation.


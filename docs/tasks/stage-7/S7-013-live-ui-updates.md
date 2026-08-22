---
schema_version: 1
id: S7-013
title: Deliver live operator updates through the UI dispatcher
stage: 7
status: planned
priority: 820
type: feature
depends_on: [S7-006, S7-007, S7-008, S7-009, S7-010, S7-011, S7-012]
labels: [wpf, notifications, concurrency]
created: 2026-08-22
updated: 2026-08-22
---
# S7-013: Deliver Live Operator Updates Through the UI Dispatcher

## Objective
Update every active operator view from authoritative state without restarting WPF.

## Context
Use [Trading.UI.Wpf](../../architecture.md#67-tradinguiwpf) and [Performance Tests](../../test-plan.md#15-performance-tests).

## Scope
- Add a bounded update stream for Bots, runs, Research, Proposals, Orders, Fills, Positions, reconciliation, warnings, and switches.
- Marshal mutations through an injected dispatcher abstraction.
- Coalesce redundant identities, preserve terminal transitions, and refresh through query services.
- Stop subscriptions on route disposal and shutdown; test with controlled sources and fake dispatcher.

## Out of Scope
None.

## Acceptance Criteria
- Material state changes appear without restart.
- No UI-bound property or collection mutates off dispatcher.
- Bursts are bounded, per-identity ordered, cancellation-safe, and leak-free.

## Validation
Build; LiveUpdates WPF tests; OperatorUpdates integration tests; full tests; format.

## Completion Notes
Pending implementation.


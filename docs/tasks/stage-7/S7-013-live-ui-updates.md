---
schema_version: 1
id: S7-013
title: Deliver live operator updates through the UI dispatcher
stage: 7
status: done
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
Implemented bounded operator update hints for Bots, runs, Research, Proposals, Orders, Fills, Positions,
reconciliation, warnings, and kill switches. Active navigation pages start a route-scoped subscription after their
initial load, refresh through authorized query services on an injected dispatcher, and cancel and await the
subscription before disposing the workspace. The bounded buffer coalesces redundant identities, keeps per-identity
sequence order, preserves terminal transitions during bursts, and propagates cancellation under backpressure. The WPF
host uses a bounded polling source so material persisted changes appear without restarting the application.

Validation:

- `.\dev.ps1 build` — passed; zero warnings and zero errors.
- `.\dev.ps1 test -Project tests/Trading.UI.Wpf.Tests/Trading.UI.Wpf.Tests.csproj -Filter "Category=LiveUpdates"` — 2 passed, 0 failed, 0 skipped.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests/Trading.IntegrationTests.csproj -Filter "Category=OperatorUpdates"` — 3 passed, 0 failed, 0 skipped.
- `.\dev.ps1 test` — 1,220 passed, 0 failed, 4 skipped pending Stage 7 acceptance bindings.
- `.\dev.ps1 format` — passed after correcting import ordering reported by the first verification run.

No scope deviations, follow-up tasks, or ADR changes.

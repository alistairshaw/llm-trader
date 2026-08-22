---
schema_version: 1
id: S7-005
title: Build shell navigation and accessibility foundations
stage: 7
status: planned
priority: 900
type: feature
depends_on: [S7-004]
labels: [wpf, mvvm, accessibility]
created: 2026-08-22
updated: 2026-08-22
---
# S7-005: Build Shell Navigation and Accessibility Foundations

## Objective
Build a keyboard-accessible MVVM shell for every Stage 7 feature area.

## Context
Use [Trading.UI.Wpf](../../architecture.md#67-tradinguiwpf) and [UI Testability](../../test-plan.md#111-ui-testability-requirements).

## Scope
- Implement route model, navigation, async commands, busy/error state, and lifetime status.
- Add routes for Bots, Portfolios, Runs, Research, Proposals, Execution, Risk, and Settings.
- Add stable Automation IDs, accessible names/roles/state, keyboard paths, and deterministic loading/error surfaces.
- Test view models without launching WPF and inspect XAML accessibility metadata.

## Out of Scope
None.

## Acceptance Criteria
- Every route is keyboard reachable with one active state.
- Navigation cancels obsolete loading and releases prior subscriptions.
- All asserted shell controls pass automation/accessibility checks.

## Validation
Build; Shell and Accessibility WPF tests; full tests; format.

## Completion Notes
Pending implementation.

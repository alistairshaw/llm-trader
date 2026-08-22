---
schema_version: 1
id: S7-005
title: Build shell navigation and accessibility foundations
stage: 7
status: done
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
Implemented the immutable shell route catalog, cancellable navigation and page lifetime contract, asynchronous
commands, observable busy/error/lifetime state, and an accessible keyboard-reachable WPF shell. Added view-model
tests for active-route selection, cancellation, disposal, and deterministic failure state, plus XAML metadata
inspection for stable automation identifiers, accessible names, headings, live status, commands, and keyboard
navigation.

Validation completed on 2026-08-22:

- `./dev.ps1 restore -RefreshLocks` generated the new test-project lock; `./dev.ps1 restore` then passed in locked mode.
- `./dev.ps1 build` passed with zero warnings and zero errors.
- `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests` passed 5/5 tests.
- `./dev.ps1 test` passed 1,164 tests with four expected pending Stage 7 acceptance scenarios skipped.
- `./dev.ps1 format` passed.
- `git diff --check` passed.

The first restore attempt encountered crash-corrupted disposable NuGet cache data after Docker Desktop became
unresponsive. The isolated worktree cache and generated `obj` directories were cleared, Docker Desktop was restarted,
and the complete validation sequence then passed without retrying any test.

No scope deviations, follow-up tasks, or ADRs.

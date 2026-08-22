---
schema_version: 1
id: S7-015
title: Build Windows FlaUI automation harness
stage: 7
status: done
priority: 780
type: test
depends_on: [S7-014]
labels: [flaui, reqnroll, windows]
owner: s7_015
created: 2026-08-22
updated: 2026-08-22
---
# S7-015: Build Windows FlaUI Automation Harness

## Objective
Create a deterministic Windows-only Reqnroll/FlaUI UIA3 harness.

## Context
Use [WPF UI Acceptance Tests](../../test-plan.md#11-wpf-ui-acceptance-tests) and [CI Matrix](../../test-plan.md#17-ci-matrix).

## Scope
- Add the WPF acceptance project with pinned packages and locks.
- Implement process/database lifetime, readiness, bounded waits, cleanup, and orphan detection.
- Implement Automation-ID/UIA page objects and failure screenshot/tree/log artifacts.
- Add interactive Windows CI selection and result/artifact publication.

## Out of Scope
None.

## Acceptance Criteria
- Harness smoke launches, navigates, verifies state, closes, and deletes its fixture.
- No selector uses coordinates, color, animation, or localized display text.
- Linux excludes UI execution while validating solution and non-UI scenarios.

## Validation
Build; publish-wpf; HarnessSmoke WPF acceptance; full tests; format.

## Completion Notes
Added the locked `net10.0-windows` Reqnroll/FlaUI UIA3 acceptance project, generated sources for the staged Stage 7
features, and a `HarnessSmoke` journey. The shared driver owns a unique local-app-data database/run directory, bounded
readiness and interaction waits, process shutdown and orphan checks, first-attempt fixture deletion, Automation-ID page
objects, and bounded redacted failure diagnostics. Windows CI publishes the deterministic self-contained application,
selects the smoke category explicitly, and retains TRX, screenshot, UIA-tree, and application-log artifacts. Linux
compiles the project but does not discover or execute its UI scenarios.

Validation:

- `./dev.ps1 restore -RefreshLocks` — passed; generated the pinned FlaUI 5.0.0 lock graph.
- `./dev.ps1 restore` — passed in locked mode.
- `./dev.ps1 build` — passed with zero warnings and errors.
- `./dev.ps1 publish-wpf` — passed; produced the self-contained `win-x64` artifact.
- `./dev.ps1 test -Project tests/Trading.UI.Wpf.AcceptanceTests -Filter "TestCategory=HarnessSmoke"` — passed on
  Linux by excluding UI discovery and execution as designed.
- `./dev.ps1 test` — passed: 1,223 passed, 0 failed, and 4 pre-existing pending Stage 7 non-UI scenarios skipped.
- `./dev.ps1 format` — passed.
- `git diff --check` — passed.

The interactive Windows `HarnessSmoke` execution and native Windows result are delegated to the new CI selection; no
WPF GUI was launched in Linux Docker and the Windows host has no repository test SDK. No scope deviations, follow-up
tasks, or ADRs.

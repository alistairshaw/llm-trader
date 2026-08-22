---
schema_version: 1
id: S7-015
title: Build Windows FlaUI automation harness
stage: 7
status: ready
priority: 780
type: test
depends_on: [S7-014]
labels: [flaui, reqnroll, windows]
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
Pending implementation.

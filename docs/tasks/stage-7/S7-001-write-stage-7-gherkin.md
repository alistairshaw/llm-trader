---
schema_version: 1
id: S7-001
title: Write Stage 7 executable Gherkin specifications
stage: 7
status: ready
priority: 1000
type: test
depends_on: []
labels: [gherkin, acceptance, wpf]
created: 2026-08-22
updated: 2026-08-22
---
# S7-001: Write Stage 7 Executable Gherkin Specifications

## Objective
Define executable scenarios for every Stage 7 criterion.

## Context
Use [Stage 7](../../implementation-plan.md#9-stage-7-wpf-operator-interface) and [WPF UI tests](../../test-plan.md#11-wpf-ui-acceptance-tests).

## Scope
- Add cross-platform scenarios for operator authorization, kill-switch behavior, updates, and shutdown.
- Add Windows UI scenarios for Bots, Portfolios, runs, Research, Proposals, execution, warnings, and kill switches.
- Add platform/capability tags, synchronized generated sources, and criterion traceability.
- Mark scenarios pending until their binding tasks activate them.

## Out of Scope
None.

## Acceptance Criteria
- Every Stage 7 criterion maps to a named scenario.
- UI scenarios contain no coordinate, color-only, timing, or display-text selectors.
- Both test projects discover all scenarios with only explicit pending outcomes.

## Validation
`.\dev.ps1 build`; focused `TestCategory=stage7` discovery in both acceptance projects; `.\dev.ps1 format`.

## Completion Notes
Pending implementation.


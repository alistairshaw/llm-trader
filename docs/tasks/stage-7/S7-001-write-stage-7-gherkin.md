---
schema_version: 1
id: S7-001
title: Write Stage 7 executable Gherkin specifications
stage: 7
status: done
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
- Add platform/capability tags, synchronized generated sources for the existing cross-platform project, and criterion traceability.
- Stage the Windows WPF feature specifications under the intended future `Trading.UI.Wpf.AcceptanceTests` structure and validate their syntax and selector-independent language before `S7-015` creates that project and generates its sources.
- Mark scenarios pending until their binding tasks activate them.

## Out of Scope
None.

## Acceptance Criteria
- Every Stage 7 criterion maps to a named scenario.
- UI scenarios contain no coordinate, color-only, timing, or display-text selectors.
- The existing cross-platform acceptance project discovers all non-UI scenarios with only explicit pending outcomes.
- Staged WPF feature specifications have valid deterministic Gherkin structure and remain owned by `S7-015` for project integration, package locking, and generated-source discovery.

## Validation
`.\dev.ps1 build`; focused `TestCategory=stage7&TestCategory!=windows` discovery in `Trading.AcceptanceTests`; deterministic structure and prohibited-selector inspection of the staged WPF features; `.\dev.ps1 format`.

## Completion Notes

Completed 2026-08-22.

- Added four cross-platform operator scenarios covering denied authority, hierarchical kill switches, ordered execution updates, and bounded recoverable host shutdown. The generated Reqnroll source is synchronized and all four cases are explicitly pending under `@ignore` until `S7-016` supplies production-backed bindings.
- Staged thirteen named WPF scenarios with eight Scenario Outline examples under the intended `Trading.UI.Wpf.AcceptanceTests` feature structure. They cover Bot lifecycle and assignment, run observation, Research, Proposal evidence and decisions, live Order/Fill updates, four execution modes, four prominent warning conditions, kill-switch confirmation, accessibility metadata, and clean shutdown.
- Added complete non-UI and WPF criterion-to-scenario traceability. UI language contains no coordinate, color-only, timing, animation, or localized display-text selectors.
- Corrected a planning defect in this task: its original acceptance criterion required discovery in a WPF test project that does not exist yet, while `S7-015` explicitly owns creating that project. S7-001 now requires existing-project discovery plus deterministic validation of staged WPF specifications; `S7-015` retains ownership of project creation, pinned packages and locks, generated sources, and harness integration.
- Validation: `.\dev.ps1 build` passed with zero warnings and errors; `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage7&TestCategory!=windows"` discovered 4 tests with 4 explicitly skipped, 0 failed, and 0 passed; staged WPF structure validation found 13 named scenarios, valid feature tags, and at least one Given/When/Then step per feature; prohibited-selector inspection found no violation; `.\dev.ps1 format` passed; `.\dev.ps1 test` passed 1,148 tests with only the 4 planned Stage 7 skips. The generated Reqnroll source retains the generator's standard trailing whitespace, consistent with existing committed generated feature sources; non-generated changes pass whitespace inspection.
- Local execution used the Linux Docker workflow. WPF discovery and interactive UI execution remain intentionally delegated to `S7-015`, `S7-017`, and hosted Windows validation.
- No production behavior, dependency, ADR, README guidance, AGENTS.md guidance, or authoritative architecture/domain decision changed. No follow-up task is required beyond the existing Stage 7 backlog.

---
schema_version: 1
id: S1-001
title: Write Stage 1 executable Gherkin specifications
stage: 1
status: done
priority: 1000
type: acceptance
depends_on: []
labels: [bdd, reqnroll, planning]
created: 2026-08-19
updated: 2026-08-19
---

# S1-001: Write Stage 1 Executable Gherkin Specifications

## Objective

Translate every Stage 1 acceptance criterion into business-readable Gherkin features before implementation begins.

## Context

The [Implementation Plan](../../implementation-plan.md#3-stage-1-solution-foundation-and-domain-model) requires every stage to begin with Gherkin and finish with its BDD suite passing.

## Scope

- Create Stage 1 `.feature` files under the planned `Trading.AcceptanceTests/Features/Foundation` structure.
- Cover clean solution build, cross-platform boundaries, domain-value construction, strongly typed IDs, aggregate transitions, and a single documented validation entry point.
- Tag scenarios with `@stage1`, plus `@windows` where genuinely platform-specific.
- Add a traceability table mapping every Stage 1 criterion to at least one scenario.
- Use domain language and observable outcomes rather than classes, project-file edits, or UI clicks.

## Out of Scope

- Implementing step definitions or production code.
- Creating the solution or test project.
- Stage 2 persistence behavior.

## Acceptance Criteria

- Every Stage 1 acceptance criterion maps to at least one scenario.
- Feature files are valid Gherkin and contain no undefined domain terminology.
- Cross-platform and Windows-only expectations are tagged explicitly.
- Scenarios do not depend on real LLM, web, market-data, or broker services.
- The traceability table identifies the future implementing task for each scenario.

## Validation

- Review feature syntax and traceability against the Stage 1 plan.
- Confirm the files can be copied into the acceptance-test project without semantic rewriting in `S1-004`.
- Full Reqnroll execution is deferred to `S1-015` after the test project and bindings exist.

## Completion Notes

Completed 2026-08-19.

- Added five Stage 1 Foundation feature files containing 25 business-readable scenarios for build and platform validation, architecture boundaries, financial values, strongly typed identities, and aggregate lifecycles.
- Added `tests/Trading.AcceptanceTests/Features/Foundation/TRACEABILITY.md`, mapping all 11 Stage 1 acceptance criteria to scenarios and future implementing tasks.
- Statically validated that every feature has one `Feature`, at least one scenario, an `@stage1` tag, non-empty steps, and unique scenario names; all 11 criteria are mapped and the sole genuinely platform-specific scenario is tagged `@windows`.
- Reqnroll execution was not run because the solution, acceptance-test project, and bindings are deliberately deferred to `S1-002`, `S1-004`, and `S1-015` by this task's scope and validation section.
- No production code, solution, test project, bindings, external-service access, follow-up tasks, or ADRs were added.

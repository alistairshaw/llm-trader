---
schema_version: 1
id: S7-014
title: Publish deterministic WPF test profile
stage: 7
status: planned
priority: 800
type: infrastructure
depends_on: [S7-013]
labels: [wpf, publish, fixtures]
created: 2026-08-22
updated: 2026-08-22
---
# S7-014: Publish Deterministic WPF Test Profile

## Objective
Produce a self-contained Windows artifact backed by an isolated paper-only fixture.

## Context
Use [Local Development](../../local-development.md) and [UI Testability](../../test-plan.md#111-ui-testability-requirements).

## Scope
- Complete `publish-wpf` and `run-wpf` for self-contained `win-x64` output built through Docker.
- Add a validated profile with temporary migrated SQLite, deterministic IDs/clock/scripts, fixture Research, and simulated broker.
- Seed operator journeys and expose bounded readiness/shutdown signals.
- Keep and redact runtime artifacts outside source control; test layout, isolation, readiness, and first-attempt cleanup.

## Out of Scope
None.

## Acceptance Criteria
- A clean checkout publishes/launches without host .NET.
- The profile has no live/network/real-LLM/credential authority.
- Closing releases process and database on the first bounded attempt.

## Validation
Restore; build; publish-wpf; WpfTestProfile tests; format.

## Completion Notes
Pending implementation.


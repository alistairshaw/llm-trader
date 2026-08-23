---
schema_version: 1
id: S7-018
title: Complete Stage 7 acceptance and review
stage: 7
status: ready
priority: 1000
type: test
depends_on: [S7-001, S7-002, S7-003, S7-004, S7-005, S7-006, S7-007, S7-008, S7-009, S7-010, S7-011, S7-012, S7-013, S7-014, S7-015, S7-016, S7-017]
labels: [review, ci, windows, security]
created: 2026-08-22
updated: 2026-08-22
---
# S7-018: Complete Stage 7 Acceptance and Review

## Objective
Audit every Stage 7 exit criterion and publish an exact-revision review record.

## Context
Use [Acceptance Rules](../../implementation-plan.md#2-acceptance-rules-for-every-stage), [Stage 7](../../implementation-plan.md#9-stage-7-wpf-operator-interface), and [Stage Completion](../../task-management.md#15-stage-completion).

## Scope
- Audit implementation, view-model tests, production acceptance, WPF automation, accessibility, migrations, docs, and demonstration.
- Run restore/build/format, all suites, migration/drift, repeated non-UI/UI journeys, publish, cleanup, and security gates.
- Inspect authorization, authority separation, switches, dispatcher safety, warnings, accessibility, automation stability, ownership, and redaction.
- Record exact hosted Linux, interactive Windows, UI artifact, and security evidence with a Stage 8 decision.

## Out of Scope
None.

## Acceptance Criteria
- Every task and criterion has objective passing evidence.
- Non-UI scenarios pass twice and on Windows/Linux; WPF scenarios pass twice on interactive Windows; zero skips.
- All build, test, migration, publish, accessibility, cleanup, and security gates pass.
- No critical/high defect remains and the review records an explicit Stage 8 decision.

## Validation
Locked restore; build; format; full tests; Stage7 non-UI twice; publish-wpf; Stage7 WPF twice; EF drift; hosted Windows/Linux/security.

## Completion Notes
Pending implementation.

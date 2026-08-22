---
schema_version: 1
id: S7-009
title: Build Research catalog and Report viewer
stage: 7
status: ready
priority: 850
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, research, provenance]
created: 2026-08-22
updated: 2026-08-22
---
# S7-009: Build Research Catalog and Report Viewer

## Objective
Let authorized operators request Research and inspect exact immutable Report versions.

## Context
Use [Research Bot](../../research-bot.md) and [Read Models](../../data-model.md#18-read-models).

## Scope
- Build Research request, catalog/filter/page, Report detail, version history, freshness, visibility, provenance, citations, and failure views.
- Bind reads to exact identity/version and enforce private/restricted visibility.
- Render source evidence as inert text within explicit untrusted boundaries.
- Add accessibility metadata and view-model/catalog integration tests.

## Out of Scope
None.

## Acceptance Criteria
- Version, hash, freshness, visibility, generator metadata, and provenance are visible.
- Inaccessible and missing Reports are indistinguishable.
- Source text cannot create executable commands or UI instructions.

## Validation
Build; ResearchCatalog WPF tests; OperatorResearch integration tests; full tests; format.

## Completion Notes
Pending implementation.

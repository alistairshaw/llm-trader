---
schema_version: 1
id: S7-009
title: Build Research catalog and Report viewer
stage: 7
status: done
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
Implemented the keyboard-accessible Research workspace with authorized catalog filtering and paging, exact immutable
Report selection, version history, freshness and visibility state, content hash, generator metadata, structured
provenance, and authorized Research requests. Expanded the UI-neutral operator Report contracts with the immutable
metadata required by the viewer. Report and source content is displayed only in read-only plain-text controls inside
explicit untrusted-evidence boundaries; exact-detail identity and version mismatches use the same non-disclosing
result as missing or inaccessible Reports.

Validation completed on 2026-08-22:

- `.\dev.ps1 restore` passed in locked mode after one Docker Desktop `unexpected EOF`; the immediate retry completed.
- `.\dev.ps1 build` passed with zero warnings and zero errors.
- `.\dev.ps1 test -Project tests/Trading.UI.Wpf.Tests -Filter "Category=ResearchCatalog"` passed 5/5.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=OperatorResearch"` passed 2/2.
- `.\dev.ps1 test` passed 1,193 tests with four expected pending Stage 7 acceptance scenarios skipped.
- `.\dev.ps1 format` passed.
- `git diff --check` passed.

No scope deviations, follow-up tasks, or ADRs.

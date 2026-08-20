---
schema_version: 1
id: S4-009
title: Validate and publish immutable Research reports
stage: 4
status: done
priority: 790
type: feature
depends_on: [S4-008]
labels: [research, reports, provenance, versioning]
created: 2026-08-20
updated: 2026-08-20
---

# S4-009: Validate and Publish Immutable Research Reports

## Objective

Convert a valid completed Research draft into one canonical immutable, cited, versioned report.

## Context

Use [Research Bot — Report Contract](../../research-bot.md#9-report-contract), [Versioning, Freshness, and Expiration](../../research-bot.md#11-versioning-freshness-and-expiration), [Data Model — Research Reports](../../data-model.md#85-research_reports), and [Test Plan — Data Integration Tests](../../test-plan.md#6-data-integration-tests).

## Scope

- Define report schema version `1` with executive summary, claims, supporting and contradictory evidence, material risks, uncertainty and missing information, methodology and calculations, time horizons, applicability limits, and machine-readable conclusions.
- Validate cited sources against the run's retrieved provenance and validate cutoff, generation, expiration, recommended refresh, visibility, and generator metadata.
- Canonically serialize content, compute SHA-256, and atomically publish the report and complete the request.
- Create refreshes as the next immutable report-series version linked to the exact superseded report and project freshness/status for catalog reads.
- Retain partial and invalid drafts with validation results in run audit without publishing them.

## Acceptance Criteria

- Only a schema-valid, fully cited draft from a successfully finished run can be published.
- Publication is idempotent and concurrent refreshes cannot create duplicate version numbers or divergent series state.
- Published content, provenance, hash, schema, generator metadata, cutoff, generated time, and visibility cannot be modified.
- Exact historical versions remain readable after expiration, supersession, correction, or retraction, while freshness projection reflects current status and time.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=ReportPublication"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ResearchReports"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ReportPublication"
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

- Added schema-1 deterministic validation and ordinal canonical JSON serialization for every required report
  section, same-attempt citation provenance, UTC cutoff/refresh times, successful run completion, and pinned
  generator/schema versions. The golden canonical content SHA-256 is
  `865cd67dc02e5b7a3b13ce38f6619748de58b6ee56cfcdfecc59f773a7393c9b`.
- Added the publication service and atomic SQLite publication operation. A single immediate transaction allocates
  series versions, writes the report and ordered provenance, completes the request, and supersedes the preceding
  latest version. Run retries return the existing report; concurrent refreshes allocate distinct monotonic
  versions. EF writes reject published fact/provenance mutation and deletion outside the allowed supersession.
- Invalid and partial drafts remain in the pre-existing bounded artifact/tool audit path and never reach the
  publication operation. Exact historical catalog reads retain their original content and hash after supersession.
- Updated `README.md`, `AGENTS.md`, the Research Bot contract, and the data model with the implemented canonical
  schema, citation, transaction, immutability, and idempotency rules.
- Validation: `\.\dev.ps1 build` passed with zero warnings and errors; focused Research publication tests passed
  4/4; focused Data report tests passed 3/3; focused Integration publication tests passed 1/1; all Research tests
  passed 52/52; all Data tests passed 123/123; architecture tests passed 15/15; the complete local suite passed
  747 with 39 intentionally pending Stage 4 acceptance scenarios; Stage 4 migration/model-drift tests passed 5/5;
  and `\.\dev.ps1 format` passed.
- Deviations: no migration was required because S4-003 already supplied the report, provenance, series-version,
  hash, and supersession schema and constraints. No follow-up tasks or ADRs were created.

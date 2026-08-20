---
schema_version: 1
id: S4-009
title: Validate and publish immutable Research reports
stage: 4
status: ready
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

Pending implementation.

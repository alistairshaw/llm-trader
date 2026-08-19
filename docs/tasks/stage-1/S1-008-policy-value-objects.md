---
schema_version: 1
id: S1-008
title: Implement foundational policy value objects
stage: 1
status: done
priority: 800
type: feature
depends_on: [S1-006]
labels: [domain, policy, value-objects]
created: 2026-08-19
updated: 2026-08-19
---

# S1-008: Implement Foundational Policy Value Objects

## Objective

Represent the immutable policy and configuration concepts required to construct valid Stage 1 aggregates.

## Scope

- Implement initial `InvestmentMandate`, `UniverseDefinition`, `RiskLimit`, `RiskPolicy`, `CashReservePolicy`, `RunBudget`, `Usage`, `ToolPolicy`, `SchedulingPolicy`, `ModelConfiguration`, `FinishResult`, and `DataFreshness` value objects.
- Validate required fields, bounds, and internally contradictory settings.
- Keep external-provider credentials and types out of configuration values.

## Out of Scope

- Full production risk-rule catalog.
- Provider-specific LLM settings.
- Runtime scheduling or budget enforcement.
- Persistence serialization.

## Acceptance Criteria

- Every implemented policy type is immutable and validates construction.
- Invalid bounds and contradictory configurations are rejected.
- Tool policy distinguishes allowed tools and per-tool limits.
- Scheduling policy represents baseline cadence and requested-wake bounds.
- Model configuration contains no secret value.
- Value equality and representative negative tests pass.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=Policies"
```

## Completion Notes

Completed 2026-08-19.

- Added immutable, provider-neutral value objects for mandates and universes; risk limits, policies, and cash reserves; run budgets and usage; tool allowances and policies; scheduling and model configuration; finish results; and data freshness.
- Construction now rejects missing fields, negative or inverted bounds, duplicate risk/tool definitions, incomplete wake requests, non-UTC or out-of-order timestamps, and negative resource values. Collection inputs are defensively copied and collection-backed policies implement value equality.
- Added eight representative `Policies` category tests covering valid construction, value equality, immutable collection behavior, invalid and contradictory configurations, allowed tools and per-tool limits, baseline/requested-wake bounds, absence of credential-bearing model fields, and freshness decisions.
- Validation passed: `.\dev.ps1 build` (0 warnings, 0 errors); `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=Policies"` (8 passed); `.\dev.ps1 test` (103 passed across Core and architecture tests, 47 intentionally deferred Stage 1 acceptance scenarios skipped); and `.\dev.ps1 format` (passed with no output).
- No scope deviations, follow-up tasks, or ADRs were required. The workspace exposes no usable Git repository metadata, so working-tree status and diff inspection were unavailable; shared changes were preserved through scoped file inspection and edits.

---
schema_version: 1
id: S1-007
title: Implement financial value objects
stage: 1
status: done
priority: 810
type: feature
depends_on: [S1-006]
labels: [domain, finance, value-objects]
created: 2026-08-19
updated: 2026-08-19
---

# S1-007: Implement Financial Value Objects

## Objective

Provide exact, immutable financial primitives with explicit units, currency, precision, and invariant-safe arithmetic.

## Scope

- Implement `Currency`, `Money`, `Price`, `Quantity`, and `Percentage`.
- Define construction, equality, formatting, arithmetic, comparison, and compatible-unit checks.
- Define initial precision and rounding behavior needed by Stage 1.
- Add boundary and invalid-input unit tests.

## Out of Scope

- Foreign-exchange conversion.
- Instrument-specific tick/lot enforcement.
- SQLite conversion.
- Full accounting and P&L policy for later stages.

## Acceptance Criteria

- Financial values use `decimal`, never binary floating point.
- Currency and unit mismatches fail explicitly.
- Negative, zero, and upper-bound behavior is defined per type.
- Equality and formatting are culture-independent for machine-readable use.
- Arithmetic does not silently discard precision.
- Positive, negative, boundary, and incompatible-unit tests pass.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=FinancialValues"
```

## Completion Notes

- Added immutable `Currency`, `Money`, `Price`, `Quantity`, and `Percentage` record classes in `Trading.Core`, with exact `decimal` storage, invariant canonical formatting, value equality, checked arithmetic, comparison operators, and explicit currency/unit compatibility checks.
- Defined Stage 1 boundaries: money supports the full signed `decimal` range; price is non-negative; quantity is strictly positive and carries a lowercase ASCII unit; percentage is 0 through 100 inclusive; currency uses a three-letter uppercase ISO-style code. Arithmetic preserves the runtime's exact decimal result and throws on overflow, division by zero, invalid results, or incompatible units instead of rounding silently.
- Added 10 focused unit tests covering construction, equality, canonical formatting under a non-invariant current culture, exact precision, positive/negative/zero/maximum boundaries, checked overflow, division by zero, and incompatible currency or quantity units.
- Validation passed: `.\dev.ps1 build` (0 warnings, 0 errors); `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=FinancialValues"` (10 passed); `.\dev.ps1 test` (95 passed across Core and architecture suites plus the acceptance infrastructure test, with 47 intentionally deferred Stage 1 acceptance scenarios skipped); and `.\dev.ps1 format`.
- The ignored financial-value Gherkin bindings remain intentionally deferred to `S1-015`, consistent with the established Stage 1 acceptance plan. No other scope deviations, follow-up tasks, or ADRs were required. The workspace exposes no usable Git repository metadata, so working-tree status and diff inspection were unavailable; changes were preserved through direct scoped inspection.

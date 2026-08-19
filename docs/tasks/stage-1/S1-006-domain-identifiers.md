---
schema_version: 1
id: S1-006
title: Implement strongly typed domain identifiers
stage: 1
status: done
priority: 820
type: feature
depends_on: [S1-004, S1-005]
labels: [domain, identifiers, ulid]
created: 2026-08-19
updated: 2026-08-19
---

# S1-006: Implement Strongly Typed Domain Identifiers

## Objective

Represent every Stage 1 aggregate and entity identity with an explicit immutable type that cannot be confused with another identity.

## Scope

- Implement the strongly typed IDs named in the domain model.
- Define generation, parsing, formatting, equality, and empty-value rejection.
- Use a shared internal pattern without exposing one untyped ID API.
- Add deterministic ID generation as a test seam where needed.

## Out of Scope

- EF Core converters.
- Database persistence.
- External broker identifier mapping.

## Acceptance Criteria

- IDs use value equality and stable canonical formatting.
- Empty, malformed, or wrong-length values are rejected.
- APIs requiring one ID type cannot receive another ID type at compile time.
- Parsing and formatting round-trip.
- Domain identifiers do not depend on EF Core or broker SDK types.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=Identifiers"
```

## Completion Notes

Completed 2026-08-19.

- Added 25 explicit immutable identifier types for every aggregate root and owned entity named by the Stage 1 domain model. Each type has its own compile-time identity while sharing only an internal ULID implementation.
- Implemented cryptographically random ULID generation, canonical uppercase Crockford Base32 formatting, case-insensitive parsing, value equality, and rejection of null, empty, all-zero, malformed, wrong-length, and out-of-range values.
- Added the covariant `IIdentifierGenerator<TIdentifier>` seam so tests and application composition can supply deterministic, strongly typed identity generation without exposing an untyped identifier API.
- Added the Core project reference to `Trading.Core.Tests`, refreshed its committed dependency lock, and added 77 focused identifier tests covering every identifier type, formatting/parsing round trips, generation, invalid inputs, value equality, type separation, and deterministic generation.
- Validation passed: one-time lock refresh with `docker compose run --rm --no-deps dev dotnet restore TradingBot.sln --force-evaluate`; `.\dev.ps1 restore`; `.\dev.ps1 build` (0 warnings, 0 errors); `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=Identifiers"` (77 passed); `.\dev.ps1 test` (85 passed and 47 intentionally deferred Stage 1 acceptance scenarios skipped); and `.\dev.ps1 format`.
- Existing identity Gherkin bindings remain intentionally deferred to `S1-015`, consistent with the established Stage 1 test-infrastructure plan. No other scope deviations, follow-up tasks, or ADRs were required. The workspace exposes no `.git` metadata, so working-tree status and diff inspection were unavailable; changes were preserved through direct scoped inspection.

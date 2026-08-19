---
schema_version: 1
id: S2-004
title: Implement canonical persistence converters
stage: 2
status: done
priority: 880
type: feature
depends_on: [S2-003]
labels: [converters, decimals, timestamps, json]
created: 2026-08-19
updated: 2026-08-19
---

# S2-004: Implement Canonical Persistence Converters

## Objective

Implement one tested conversion policy for identifiers, exact decimals, UTC timestamps, enumerations, and canonical JSON.

## Context

Follow [Data Model — SQLite Storage Conventions](../../data-model.md#3-sqlite-storage-conventions) and [EF Core Mapping Rules](../../data-model.md#17-ef-core-mapping-rules).

## Scope

- Add centralized EF Core converters and comparers for every Stage 2 strongly typed identifier and immutable value object.
- Store exact decimals as invariant `TEXT` with maximum precision 24 and maximum scale 8.
- Reject values outside 16 integer digits, 8 fractional digits, or 24 total significant digits before persistence.
- Canonicalize decimal text without exponent notation, normalize zero, and remove insignificant fractional trailing zeros.
- Store UTC `DateTimeOffset` values as Unix-millisecond `INTEGER` values and reject non-UTC values at the persistence boundary.
- Store enumerations as constrained canonical text.
- Add deterministic canonical JSON serialization with integer schema versions and lowercase SHA-256 hashes.

## Acceptance Criteria

- Every Stage 2 identifier round-trips as canonical 26-character ULID text.
- Boundary decimal values round-trip exactly through SQLite.
- Unsupported decimal precision and scale are rejected before SQL execution.
- UTC timestamps round-trip with millisecond precision and preserve ordering.
- Canonical JSON is byte-stable for equivalent content and produces a stable lowercase SHA-256 hash.
- Converter tests execute against the real SQLite provider.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Converters"
.\dev.ps1 build
```

## Completion Notes

Completed 2026-08-19.

- Added centralized EF Core value converters and immutable value comparers for Stage 2 strongly typed identifiers and value objects.
- Added canonical exact-decimal storage with the logical `DECIMAL(24,8)` envelope, invariant non-exponent text, normalized zero, and rejection of unsupported integer digits, scale, precision, and provider text.
- Added strict UTC Unix-millisecond timestamp conversion and canonical enumeration text conversion.
- Added deterministic, schema-versioned canonical JSON serialization and validated lowercase SHA-256 hashing.
- Added real-SQLite converter integration tests covering all 25 domain identifier types, every Stage 2 financial value object, decimal boundaries and rejection, timestamp precision and ordering, enums, JSON byte stability, hashes, schema validation, and value comparison.

Validation:

- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Converters"` — passed, 43 tests.
- `.\dev.ps1 build` — passed in Release with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed: 382 tests; 20 intentionally deferred Stage 2 acceptance scenarios skipped.
- `.\dev.ps1 format` — passed.

Deviations: none.

Follow-up tasks: none.

ADRs: none.

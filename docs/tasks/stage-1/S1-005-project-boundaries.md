---
schema_version: 1
id: S1-005
title: Establish project references and architecture tests
stage: 1
status: done
priority: 880
type: test
depends_on: [S1-003, S1-004]
labels: [architecture, dependencies, cross-platform]
created: 2026-08-19
updated: 2026-08-19
---

# S1-005: Establish Project References and Architecture Tests

## Objective

Encode the permitted dependency direction and platform boundaries as automated tests.

## Scope

- Add only architecture-approved project references.
- Test that `Trading.Core` has no production-project dependency.
- Test that Core, Data, Brokers, Engine, Research, and Host do not reference WPF.
- Test that cross-platform projects do not use prohibited Windows APIs.
- Test that production projects do not reference test-only packages or assemblies.

## Out of Scope

- Domain behavior.
- Runtime dependency-injection composition.
- WPF UI automation.

## Acceptance Criteria

- Allowed project references match the architecture document.
- A fixture containing a forbidden reference causes an architecture-test failure.
- Cross-platform projects target `net10.0` and WPF targets `net10.0-windows`.
- Test-only frameworks remain outside production dependency graphs.
- All architecture tests pass in the unmodified solution.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 reference-list
```

## Completion Notes

Completed 2026-08-19.

- Added the architecture-approved production reference graph: Core remains independent; Data, Brokers, and Research reference Core; Engine references those four projects; and Host and WPF reference Engine.
- Added architecture tests that enforce allowed project references, target frameworks, cross-platform exclusion of WPF and Windows-only namespaces, and exclusion of test packages and assemblies from production projects.
- Added a deliberately forbidden Core-to-WPF project-reference fixture and verified that the shared policy reports the violation.
- Refreshed the affected NuGet lock files after the project-reference changes with `docker compose run --rm --no-deps dev dotnet restore TradingBot.sln --force-evaluate`; the initial locked `.\dev.ps1 restore` correctly rejected stale lock files.
- Validation passed: `.\dev.ps1 build` (0 warnings, 0 errors); `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` (6 passed); `.\dev.ps1 reference-list`; `.\dev.ps1 test` (8 passed and 47 intentionally skipped deferred acceptance scenarios); and `.\dev.ps1 format`.
- No scope deviations, follow-up tasks, or ADRs were required. The workspace exposes no `.git` metadata, so working-tree status and diff inspection were unavailable; changes were preserved through direct scoped inspection.

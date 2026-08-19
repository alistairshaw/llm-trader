---
schema_version: 1
id: S1-003
title: Configure shared build conventions
stage: 1
status: done
priority: 900
type: infrastructure
depends_on: [S1-002]
labels: [build, analyzers, packages]
created: 2026-08-19
updated: 2026-08-19
---

# S1-003: Configure Shared Build Conventions

## Objective

Apply consistent compiler, analyzer, formatting, and dependency-version policy across the solution.

## Scope

- Add shared build properties.
- Enable nullable references, implicit usings, .NET analyzers, latest recommended analysis, deterministic builds, and warnings-as-errors.
- Add central package management and lock-file policy.
- Enable platform compatibility analysis.
- Add repository formatting configuration.

## Out of Scope

- Selecting production infrastructure packages for later stages.
- Suppressing warnings without a documented justification.
- CI workflow creation.

## Acceptance Criteria

- Shared settings apply to all applicable projects without duplication.
- Package versions are centrally declared and restore is repeatable.
- A deliberate compiler or platform-compatibility violation fails the appropriate verification fixture.
- Release build succeeds with zero warnings.
- Formatting validation is documented and runnable.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

Completed 2026-08-19.

- Added `Directory.Build.props` so nullable reference types, implicit usings, .NET analyzers at `latest-recommended`, build-time code-style enforcement, warnings-as-errors, deterministic/CI builds, and locked package restore apply to every project without per-project duplication.
- Added `Directory.Packages.props` for central package version management and transitive pinning, plus committed `packages.lock.json` files for every solution project and build-convention fixture. Package-version items remain empty until S1-004 introduces the first package references.
- Added the repository `.editorconfig` and implemented the documented `.\dev.ps1 format` command as `dotnet format --verify-no-changes --no-restore` inside Docker.
- Added isolated compiler-warning and platform-compatibility fixtures and `.\dev.ps1 verify-build-conventions`; verification proved `CS1030` and `CA1416` are promoted to build errors.
- Validation passed: `.\dev.ps1 restore`; `.\dev.ps1 build` (10 projects, including WPF, 0 warnings and 0 errors); `.\dev.ps1 format`; `.\dev.ps1 verify-build-conventions`; and `.\dev.ps1 test` (exit code 0; test infrastructure remains scoped to S1-004).
- No scope deviations, follow-up tasks, or ADRs were required. The workspace exposed no `.git` metadata, so working-tree status/diff inspection was unavailable; files were preserved through direct scoped inspection and edits.

---
schema_version: 1
id: S1-002
title: Initialize the solution and project skeleton
stage: 1
status: done
priority: 950
type: infrastructure
depends_on: [S1-001]
labels: [solution, dotnet, wpf]
created: 2026-08-19
updated: 2026-08-19
---

# S1-002: Initialize the Solution and Project Skeleton

## Objective

Create the Docker-first development bootstrap, .NET solution, and production and Stage 1 test projects with architecture-defined names and target frameworks.

## Context

Project layout and targets are defined in [Architecture](../../architecture.md#4-solution-structure).

## Scope

- Create `TradingBot.sln` and `src/` and `tests/` projects needed for Stage 1.
- Target cross-platform projects at `net10.0` and WPF at `net10.0-windows`.
- Include headless and WPF entry points that compile without domain behavior.
- Place the Gherkin features from `S1-001` in the acceptance-test project structure.
- Add the Linux .NET 10 development `Dockerfile`, Docker Compose configuration, and `.dockerignore` required by [Local Development](../../local-development.md).
- Add the repository-root `dev.ps1` wrapper with `restore`, `build`, `test`, `solution-list`, and `reference-list` commands. All .NET commands execute in Docker.
- Configure the Linux build to target Windows for WPF compilation without requiring a host .NET installation.

## Out of Scope

- Project references beyond those required for initial compilation.
- Shared compiler settings and package choices beyond the minimum required to compile in the container.
- Domain implementation.

## Acceptance Criteria

- Every architecture-defined production project exists in the solution.
- Stage 1 unit, architecture, and acceptance-test projects exist.
- Target frameworks match the architecture.
- `dev.ps1 solution-list` shows the expected projects.
- Restore and Release build run in Linux Docker without invoking a host .NET installation.
- The full solution, including WPF, compiles in the Linux container with Windows targeting enabled.
- The wrapper returns the underlying Docker/.NET exit code and rejects unknown commands.

## Validation

```powershell
.\dev.ps1 restore
.\dev.ps1 build
.\dev.ps1 solution-list
```

## Completion Notes

Completed 2026-08-19.

- Added `TradingBot.sln` with all seven architecture-defined production projects plus the Stage 1 core unit, architecture, and cross-platform acceptance-test projects.
- Targeted cross-platform projects at `net10.0` and WPF at `net10.0-windows`; added minimal compilable headless and WPF entry points and retained the S1-001 Gherkin features under the acceptance-test project.
- Added a multi-stage Docker build pinned to official .NET SDK `10.0.302` and runtime `10.0.10` Ubuntu Noble images, Compose services with named NuGet/data volumes and simulated-mode defaults, `.dockerignore`, `.gitignore`, and the repository-root `dev.ps1` wrapper.
- Validation passed: `.\dev.ps1 restore`; `.\dev.ps1 build` (10 projects, including WPF, 0 warnings and 0 errors); `.\dev.ps1 solution-list`; `.\dev.ps1 reference-list`; `.\dev.ps1 test`; and `docker compose build trading-host`.
- Invoking `dev.ps1` with an unknown command returned exit code 1. The wrapper also returns Docker/.NET exit codes directly.
- Package management, shared compiler settings, project references, NUnit/Reqnroll configuration, and substantive host/domain behavior remain deferred to S1-003 through S1-005 as scoped. No follow-up task or ADR was required.
- Native Windows and interactive UI validation were not applicable to this bootstrap task; the WPF project was cross-compiled in the Linux SDK container with Windows targeting enabled.

# Trading Platform

A safety-first automated trading platform built with C# and .NET 10. It supports multiple isolated Trading Bots, a shared Research Bot, deterministic risk controls, paper and eventual live broker integrations, a cross-platform headless host, and a Windows WPF operator application.

The platform is designed around a strict authority boundary: language models may research and propose, while deterministic application services authorize schedules, risk decisions, capital reservations, and broker operations.

## Current Status

Stages 1–3 are complete: the repository has its domain and persistence foundation plus a bounded, recoverable multi-Bot runtime and cross-platform headless host. Stage 4 is adding the shared Research Bot, its immutable report catalog, authorization boundaries, and fixture-backed deterministic workflows. Its version-1 Research registry exposes only `SearchWeb`, `FetchWebDocument`, `ListReports`, `GetReport`, `PublishReportDraft`, and `FinishResearch`; every call is schema-checked, budgeted, identity-bound, provenance-aware, and durably audited. Approved-source development uses embedded, versioned fixtures whose byte counts and SHA-256 hashes are verified against a manifest; retrieved text is always wrapped as untrusted evidence. Production behavior is added incrementally through the repository-native tasks.

The current backlog and next eligible task are recorded in [Stage 4 Backlog](docs/tasks/stage-4.md).

## Development Environment

Development is Docker-first on Windows. Do not assume that the host has the .NET SDK, C# build tools, SQLite tools, or project-specific CLIs installed.

The intended workflow is:

1. Edit files on the Windows host.
2. Restore, format, build, and test inside the Linux .NET 10 development container.
3. Run the cross-platform headless host in Docker.
4. Cross-publish the WPF client as a self-contained Windows artifact from the Linux container.
5. Launch the published WPF application on Windows for manual testing.

This lets normal development proceed with Docker Desktop, Git, and an editor. A local .NET installation is optional and is reserved for future interactive WPF debugging or designer work.

The container definitions and repository-root `dev.ps1` wrapper provide `restore`, `build`, `test`, `solution-list`, and `reference-list`. Run these commands from PowerShell; they execute .NET tooling inside Linux Docker. See [Local Development](docs/local-development.md) for the complete environment decisions.

## Documentation Map

### Product and architecture

| Document | Purpose |
| --- | --- |
| [Architecture](docs/architecture.md) | Technology choices, project boundaries, runtime model, dependency direction, persistence, testing, and deployment principles |
| [Domain Model](docs/domain.md) | Authoritative domain language, aggregates, entities, value objects, lifecycles, and invariants |
| [Data Model](docs/data-model.md) | SQLite and EF Core mappings, constraints, transactions, concurrency, migrations, and repository contracts |
| [Trading Bot](docs/trading-bot.md) | Trading Bot authority, configuration, scheduling, tool loop, proposals, execution modes, isolation, and audit requirements |
| [Research Bot](docs/research-bot.md) | Shared research service, report lifecycle, evidence handling, visibility, prompt-injection controls, and audit requirements |

### Delivery and quality

| Document | Purpose |
| --- | --- |
| [Implementation Plan](docs/implementation-plan.md) | Delivery stages, scope, acceptance criteria, demonstrations, and release progression |
| [Test Plan](docs/test-plan.md) | Test layers, fixtures, deterministic substitutes, platform matrix, CI gates, and UI automation strategy |
| [Task Management](docs/task-management.md) | Task selection, metadata, status workflow, execution rules, completion evidence, and stage gates |
| [Local Development](docs/local-development.md) | Docker-first build, test, execution, WPF publishing, host requirements, data, and secrets |
| [Stage 4 Backlog](docs/tasks/stage-4.md) | Ordered Stage 4 task index, dependencies, current next task, and exit gate |

Individual task specifications live under [`docs/tasks/`](docs/tasks/). Each task document is authoritative for its scope, acceptance criteria, validation, and completion notes.

## Architectural Guardrails

- `Trading.Core` remains free of WPF, EF Core, SQLite, broker SDK, and LLM-provider dependencies.
- WPF and the headless host compose the same platform-neutral trading engine.
- Aggregate roots protect domain invariants; persistence models do not define domain behavior.
- SQLite is the initial single-node system of record and is never shared over a network filesystem.
- Every material decision and state transition is auditable and recoverable.
- Trading Bots operate only on their assigned portfolios.
- Research reports are immutable, versioned, provenance-bearing artifacts.
- LLMs cannot submit orders, approve proposals, weaken guardrails, or grant themselves authority.
- Tests use deterministic clocks, identifiers, scripted model clients, simulated brokers, fixtures, and isolated real SQLite databases.
- Commit-gating tests never call real LLMs, the public web, live market data, or live broker accounts.

## Task-Based Development

Work is executed one task at a time from a fresh context when practical:

1. Open the task specification and all documents it references.
2. Confirm its dependencies are complete and change its status to `in_progress`.
3. Implement only the stated scope, including its tests and documentation.
4. Run the required validation through the repository's Docker workflow.
5. Record commands, results, deviations, and follow-up tasks in Completion Notes.
6. Move the task to `done` only when every acceptance criterion passes, and update the stage index.

Instructions for coding agents are in [AGENTS.md](AGENTS.md).

## Safety

The default local mode is simulated, research-only, or paper trading. Live trading is a later, explicitly authorized stage. No test or ordinary development command may submit a live-money order or require production credentials.

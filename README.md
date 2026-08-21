# Trading Platform

Stage 5 adds structured Trade Proposals, deterministic hierarchical guardrails, immutable evaluations, authorized human decisions, and atomic capital reservations to the bounded Trading Bot runtime without exposing order or broker-submission authority.

A safety-first automated trading platform built with C# and .NET 10. It supports multiple isolated Trading Bots, a shared Research Bot, deterministic risk controls, paper and eventual live broker integrations, a cross-platform headless host, and a Windows WPF operator application.

The platform is designed around a strict authority boundary: language models may research and propose, while deterministic application services authorize schedules, risk decisions, capital reservations, and broker operations.

## Current Status

Stages 1–5 are complete: the repository has its domain and persistence foundation, a bounded recoverable multi-Bot runtime, a cross-platform headless host, the shared Research service, and deterministic proposal governance through capital reservation. Research runs with validated fixture-only configuration, bounded global concurrency, startup recovery, durable notifications, immutable refresh versions, and Trading Bot Research access. Its version-1 Research registry exposes only `SearchWeb`, `FetchWebDocument`, `ListReports`, `GetReport`, `PublishReportDraft`, and `FinishResearch`; every call is schema-checked, budgeted, identity-bound, provenance-aware, and durably audited. The scripted Research loop pins model, prompt, tool-set, and report-schema versions, requires both a validated draft and `FinishResearch`, and terminates safely at every resource or failure boundary. Approved-source development uses embedded, versioned fixtures whose byte counts and SHA-256 hashes are verified against a manifest; retrieved text is always wrapped as untrusted evidence. Terminal Research outcomes are delivered per subscription by atomically recording delivery and a source-keyed Trading Bot trigger, so restart and retry cannot duplicate a notification or follow-up run.

Schema-1 reports are deterministically validated, citation-bound to sources retrieved by their run, canonically hashed, and atomically published as immutable series versions. Queued Research work is claimed atomically with increasing attempt numbers, runs outside database transactions under global concurrency limits, and recovers abandoned attempts by retaining them as failed audit history before requeueing the request.

Stage 5 persistence now provides the relational foundation for versioned Hypotheses, structured Trade Proposals, exact Report evidence, append-only guardrail evaluations and approvals, and concurrency-protected capital reservations. SQLite constraints and triggers preserve immutable governance facts and exact decimal storage. The version-1 `ProposeTrade` and `ProposeTargetAllocation` tools record audited immutable proposals bound to the exact run, configuration, Portfolio snapshot, visible evidence, and pinned execution mode; they expose no approval, reservation, order, broker, or policy-mutation authority. Deterministic guardrails compose platform, account, Portfolio, and Trading Bot constraints monotonically and return complete versioned rule results from fresh state without external side effects. Evaluation and proposal disposition commit atomically with a canonical SHA-256 input identity; exact retries reuse the prior artifact, while changed state appends an independently reconstructable sequence. ResearchOnly proposals receive the same structured evaluation and then terminate with an explicit non-executable disposition that blocks approval, reservation, conversion, and broker submission despite later configuration changes. Capital reservation repeats the immutable approval, ownership, currency, fresh-snapshot, and exact-amount checks inside a serializable SQLite transaction; all unexpired same-Portfolio reservations reduce availability, and deterministic release or expiration restores it once. The proposal-governance orchestrator now coordinates initial validation, exact human review, post-approval fresh-state revalidation, reservation, retry, expiration, and bounded failure outcomes without exposing order or broker operations. Authorized no-tracking proposal queries provide stable, bounded review queues and exact governance histories while enforcing actor grants, Bot/Portfolio/account ownership, and every referenced report's visibility before returning facts.

The deterministic headless smoke now composes those Stage 5 services with paper-neutral accounts, scripted Bot sessions, fixture state, fixed time, deterministic identities, and migrated SQLite. It prints stable proposal and evaluation hashes, all hierarchical rule results, approval and reservation identities, an insufficient-capital competitor, the ResearchOnly terminal outcome, projection totals, recoverable shutdown, and an explicit zero broker-submission count.

All 39 Stage 4 Research specifications are active and deterministic against a fresh migrated SQLite file. Thin Stage-specific bindings select explicit business use cases; the scenario-scoped driver composes production request, catalog, publication, tool-loop, notification, recovery, and host services and asserts returned results or durable facts. It never derives expected outcomes from feature wording or scenario titles.

All 32 Stage 5 proposal-governance specifications are active and pass on Windows and Linux. The complete local suite contains 1000 passing tests with zero skipped tests.

The completed Stage 5 backlog and review evidence are recorded in [Stage 5 Backlog](docs/tasks/stage-5.md) and the [Stage 5 Review Record](docs/stage-5-review.md). The approved paper-order execution work is defined in the [Stage 6 Backlog](docs/tasks/stage-6.md); `S6-001` is ready.

## Development Environment

Development is Docker-first on Windows. Do not assume that the host has the .NET SDK, C# build tools, SQLite tools, or project-specific CLIs installed.

The intended workflow is:

1. Edit files on the Windows host.
2. Restore, format, build, and test inside the Linux .NET 10 development container.
3. Run the cross-platform headless host in Docker.
4. Cross-publish the WPF client as a self-contained Windows artifact from the Linux container.
5. Launch the published WPF application on Windows for manual testing.

This lets normal development proceed with Docker Desktop, Git, and an editor. A local .NET installation is optional and is reserved for future interactive WPF debugging or designer work.

The container definitions and repository-root `dev.ps1` wrapper provide `restore`, `build`, `format`, `test`, `run`, `solution-list`, and `reference-list`. `dev.ps1 run` builds a fixture-backed headless image, migrates a fresh database, and prints deterministic smoke outcomes for Trading runs, Research sharing and isolation, immutable refresh versioning, proposal evaluation, approval, capital contention, ResearchOnly non-executability, and graceful shutdown. Run these commands from PowerShell; they execute .NET tooling inside Linux Docker. See [Local Development](docs/local-development.md) for the complete environment decisions.

Temporary SQLite tests use explicit asynchronous ownership for hosts, providers, scopes, contexts, and connections. Teardown releases only the owned database's connection pool and requires its directory to delete successfully on the first attempt on Windows and Linux.

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
| [Stage 5 Backlog](docs/tasks/stage-5.md) | Ordered Stage 5 task index, dependencies, current next task, and exit gate |

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

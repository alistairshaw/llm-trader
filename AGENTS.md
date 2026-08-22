# Agent Instructions

## Purpose and Precedence

These instructions apply to every task executed in this repository, including a task started in a fresh context. Read this file before inspecting or changing implementation files.

The precedence for project decisions is:

1. The active task specification for scope, acceptance criteria, and validation intent.
2. This file for the required execution workflow and repository-wide agent behavior.
3. The authoritative architecture and domain documents linked below.
4. The stage index and implementation plan for sequencing and broader context.

If a task conflicts with an architectural invariant or safety rule, stop and surface the conflict. Do not silently reinterpret or weaken either document.

## Non-Negotiable Local Environment Rules

This is a Docker-first repository developed from Windows. Assume the Windows host does **not** have any of the following installed:

- The .NET SDK or runtime.
- MSBuild, Visual Studio, or C# compiler tooling.
- `dotnet format` or other .NET global tools.
- NUnit, Reqnroll, FlaUI, EF Core tools, or SQLite tools.
- Project-specific package managers or CLIs.

Therefore:

- Run restore, build, format, test, migrations, and .NET tools inside the repository's Linux development container.
- Never invoke `dotnet`, `msbuild`, `csc`, `vstest`, `nunit`, `sqlite3`, or similar project tooling directly on the Windows host.
- Never install local or global C#/.NET/project tooling as a workaround.
- Never ask the user to install the .NET SDK merely to complete a normal task.
- Use the committed Docker image, Docker Compose configuration, dependency lock files, and tool manifest.
- Use the repository-root `dev.ps1` wrapper once it exists; do not duplicate its Docker arguments ad hoc unless diagnosing the wrapper itself.
- Treat raw `dotnet ...` commands in task documents as the operation to run **inside Docker**, not authorization to use host tooling.
- Do not infer success from editor diagnostics. The container build and tests are authoritative.

Docker Desktop, Git, PowerShell, and ordinary filesystem tools are available on the host. Docker must remain in Linux-container mode for the normal workflow.

### Before the container workflow exists

The early solution-initialization tasks are responsible for establishing the container definitions and wrapper commands described in [Local Development](docs/local-development.md). Until that support exists:

- Do not fall back to host .NET tooling.
- Documentation-only tasks can be completed without it.
- A task that creates the solution/build infrastructure must include enough Docker support to run its own required validation, when that work is within the task's scope.
- If an earlier task genuinely cannot perform a required .NET validation without work explicitly assigned to a dependent task, record that limitation accurately; do not claim the command passed.

## Standard Execution Targets

S1-002 defines the repository-root `dev.ps1` interface. Once implemented, use these stable, PowerShell-friendly commands:

- `.\dev.ps1 restore` — restore dependencies in Linux Docker; use locked mode once lock files are established.
- `.\dev.ps1 build` — build the full solution in Release mode in Linux Docker, including Windows targeting for WPF.
- `.\dev.ps1 format` — verify formatting in Linux Docker.
- `.\dev.ps1 test` — run the full locally applicable suite in Linux Docker.
- `.\dev.ps1 test -Project <path> [-Filter <expression>]` — run a focused project or category in Linux Docker.
- `.\dev.ps1 solution-list` — list solution projects in Linux Docker.
- `.\dev.ps1 reference-list` — list project references in Linux Docker.
- `.\dev.ps1 run` — build and run the deterministic fixture-backed Trading, Research, and proposal-governance headless smoke through Docker Compose.
- `.\dev.ps1 publish-wpf` — publish a self-contained `win-x64` artifact from Linux Docker to an ignored host-visible directory when introduced.
- `.\dev.ps1 run-wpf` — launch the published artifact on the Windows host for manual testing when introduced.

The wrapper is orchestration, not a replacement build system: it validates arguments, calls Docker Compose, and returns the underlying exit code. Keep build and dependency truth in the .NET solution, project files, central build files, manifests, and lock files.

The test targets run the current Release outputs without rebuilding them. After adding or changing production code, tests, categories, or projects, run `.\dev.ps1 build` before focused or full tests so validation cannot accidentally exercise stale binaries or report a new filter as unmatched.

Deterministic smoke workflows that resolve scoped persistence services from one host scope must await each Bot run before starting the next. Production multi-bot concurrency uses independent scopes; a shared EF Core context must never be used concurrently.

WPF can be compiled and self-contained-published from Linux by enabling Windows targeting. It runs on the Windows host for manual testing. Do not attempt to launch a GUI inside the Linux development container. Interactive debugging, designer support, and FlaUI/UIA3 journeys require an interactive Windows environment and are not reasons to install host tooling during ordinary tasks.

## Start-of-Task Protocol

For every implementation task:

1. Read the complete task file under `docs/tasks/`.
2. Read every document and section linked by its Context, Scope, Acceptance Criteria, and Validation sections.
3. Read [Task Management](docs/task-management.md), especially task start, action, review, and completion rules.
4. Confirm every `depends_on` task is `done`. Do not bypass dependencies because the requested task looks implementable.
5. Inspect the working tree and preserve unrelated user changes.
6. Change the task metadata and stage index to `in_progress` before implementation, unless the user asked only for analysis or review.
7. Keep the change set within scope. Record materially separate discoveries as follow-up tasks rather than absorbing them silently.

Do not begin by probing for local .NET tools. Inspect the repository's Docker and wrapper files first.

## Completion Protocol

Before reporting a task complete:

1. Run the task's validation through the Docker workflow in every locally applicable environment.
2. Run the narrow affected tests first, then the required broader suite.
3. Distinguish clearly between tests run locally, platform checks delegated to CI, and checks not run.
4. Ensure Release builds have zero warnings and no formatter, analyzer, or architecture violations.
   Generated EF migrations and persistence entity additions are not exempt from formatting; apply the repository formatter to generated output before running the verification-only `format` target.
5. Update documentation that would otherwise be made false.
6. Fill Completion Notes with changes, exact validation commands, results, deviations, follow-up task IDs, and ADRs.
7. Set the task and stage index to `done` only when all acceptance criteria pass. Use `review` or `blocked` honestly when they do not.

Never hide failures by skipping tests, weakening assertions, suppressing warnings without justification, or reporting an unexecuted command as successful.

## Authoritative Documents

Read the documents relevant to the active task; do not rely on remembered summaries.

| Area | Authority |
| --- | --- |
| Technology, boundaries, runtime, dependencies | [Architecture](docs/architecture.md) |
| Domain language, aggregates, invariants | [Domain Model](docs/domain.md) |
| EF Core, SQLite, repositories, transactions | [Data Model](docs/data-model.md) |
| Trading Bot behavior and authority | [Trading Bot](docs/trading-bot.md) |
| Research Bot behavior and authority | [Research Bot](docs/research-bot.md) |
| Test layers, fixtures, platform matrix | [Test Plan](docs/test-plan.md) |
| Stages and delivery gates | [Implementation Plan](docs/implementation-plan.md) |
| Task workflow and evidence | [Task Management](docs/task-management.md) |
| Local tools, Docker, WPF publishing, secrets | [Local Development](docs/local-development.md) |
| Current task order | [Stage 1 Backlog](docs/tasks/stage-1.md) and later stage indexes |

If documents disagree, identify the disagreement explicitly and resolve it in documentation or through an ADR/task before encoding an arbitrary choice in code.

## Architecture and Safety Guardrails

- Keep `Trading.Core` platform-neutral and free of WPF, EF Core, SQLite, broker SDK, and LLM-provider dependencies.
- Preserve the dependency direction defined by the architecture; do not solve cycles with service locators or hidden runtime lookup.
- Represent domain entities, aggregates, and value objects explicitly in C#; do not move domain behavior into EF mappings, DTOs, prompts, UI models, or dictionaries.
- Load and save aggregate roots through repository abstractions. Do not leak `DbSet`, persistence entities, or `IQueryable` across the data boundary.
- Use `decimal` for financial values and `DateTimeOffset` in UTC for timestamps unless an authoritative document specifies otherwise.
- Treat SQLite as a single-node local store. Do not place a live database in the OneDrive source tree or share it over a network filesystem.
- Keep runtime data, secrets, logs, and generated artifacts out of source control.
- Default every local execution path to simulated, research-only, or paper mode—never live trading.
- LLMs may research and create structured proposals. They may not approve, reserve capital, submit orders, weaken policy, alter published reports, or expand their own permissions.
- Preserve bot, portfolio, account, report-visibility, and artifact isolation.
- Make material transitions explicit, idempotent, auditable, and recoverable.
- Keep durable tool-audit arguments, results, usage, timings, and errors canonical and bounded; redact diagnostic detail rather than persisting secrets or unbounded provider payloads.
- Canonicalize and hash Research report content before persistence; validate every citation against provenance retrieved by the same run, and publish report, provenance, request completion, and refresh supersession in one transaction.
- Mark a Research subscription delivered only in the same transaction that creates its source-keyed Trading Bot trigger; retry subscriber delivery independently and never expose report facts outside the subscription's visibility.
- Execute Research model and tool I/O only after the claim transaction commits. On restart, terminalize each abandoned active attempt with the stable recovery reason before requeueing its request; never overwrite or reuse the abandoned attempt.
- Keep Trading Bot Research access asynchronous and limited to the versioned `RequestResearch`, `ListReports`, and exact-version `GetReport` tools. Authorize from the Bot Run's pinned identity, configuration, policy, budgets, and report visibility; record the exact consumed version in durable tool audit.
- Keep Trading Bot proposal access limited to the versioned `ProposeTrade` and `ProposeTargetAllocation` tools. Validate canonical exact-decimal arguments and authorize pinned run, configuration, Portfolio snapshot, evidence visibility, policy, and budgets before idempotently recording a proposal; never expose approval, reservation, order, broker, or policy-mutation authority through these tools.
- Persist each guardrail evaluation and its proposal disposition atomically. Bind its canonical hash to the exact proposal content, configuration, fresh snapshot, and ordered policy versions; exact retries reuse the artifact, while revalidation appends and never overwrites an earlier rule result.
- Reserve capital only through the atomic reservation repository. Recheck approved content, Portfolio/Bot ownership, fresh snapshot identity and hash, exact currency, and every unexpired same-Portfolio reservation inside the serializable transaction; expose contention and insufficient capital as stable outcomes.
- Pin execution mode on every recorded proposal. A `ResearchOnly` proposal must still receive its complete
  structured guardrail evaluation, then terminate with `proposal_governance.research_only`; reject approval,
  reservation, order conversion, and broker submission from that pinned mode regardless of later configuration.
- Never add a test that contacts a real LLM, the public web, live market data, or a live broker to the default or commit-gating suite.
- Never submit a live-money order during development or automated validation.
- Keep paper and live broker identities structurally distinct. Every paper broker operation must require the typed paper
  environment, stable client and correlation identities, UTC timestamps, and cancellation; an unknown submission must
  reconcile by client identity before any resubmission decision.

## Testing Rules

- NUnit is the unit and integration test framework.
- Reqnroll expresses executable Gherkin acceptance behavior.
- EF Core integration tests use the real SQLite provider with isolated temporary databases, never the EF in-memory provider as a relational substitute.
- Every temporary SQLite fixture must own and asynchronously dispose its scopes, contexts, connections, service providers, and hosts before deleting its directory. Windows cleanup must succeed immediately without sleeps, retries, swallowed exceptions, or skipped assertions; clear only the applicable closed SQLite pool when documented ownership requires it.
- Cross-platform acceptance tests exercise application services and run in Linux Docker and Windows CI.
- Keep Reqnroll steps thin: route Stage-specific vocabulary through scenario-scoped application drivers. Drivers own production composition, temporary migrated SQLite files, deterministic substitutes, persistence inspection, and stable diagnostics; feature steps must not call EF, repositories, or external providers directly.
- WPF journeys use Reqnroll and FlaUI UIA3 only in an interactive Windows environment.
- Tests use injected clocks, deterministic identifiers where required, scripted LLM clients, simulated brokers, fixture-backed providers, and captured dispatchers.
- Tests must not depend on execution order, local time zone, locale, network access, developer credentials, or mutable external data.
- A code change includes tests at the lowest useful layer and acceptance coverage when it changes user-visible behavior.

## Repository Hygiene

- Preserve unrelated edits and do not rewrite user work.
- Keep generated `bin/`, `obj/`, test results, publish artifacts, databases, logs, secrets, and IDE state untracked.
- Use central package management and committed lock files. Do not add floating package versions.
- Use the repository tool manifest instead of global .NET tools.
- Do not add a dependency or suppress an analyzer warning without a concrete need and documented rationale.
- Update the stage index whenever task status or metadata changes.
- Use ADRs for durable choices that materially change architecture, safety, storage, integration, or operational behavior.

## Working Style

Make reasonable implementation-level decisions within the active task, but do not guess across authority, safety, financial-integrity, persistence, or deployment boundaries. When blocked by a genuine missing decision, record the evidence and required decision precisely.

Lead task handoff with the outcome, followed by validation evidence and any remaining limitations. Do not imply that Windows CI or interactive UI tests passed unless their results were actually observed.

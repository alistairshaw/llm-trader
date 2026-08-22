# Trading Bot Architecture

## 1. Purpose

This document defines the initial architecture for a cross-platform automated trading platform with two runtime entry points:

- A Windows desktop application built with WPF.
- A headless host that can run on Windows or Linux.

The architecture prioritizes safe order execution, deterministic controls, auditable LLM-assisted research and decision-making, multi-bot isolation, testability, recoverability, and strict separation between platform-neutral trading code and Windows-specific UI code.

The platform supports multiple Trading Bot instances with distinct investment mandates and risk tolerances. They share a Research Bot service and immutable research reports, but each Trading Bot has isolated configuration, runtime state, portfolio ownership, schedules, budgets, proposals, and audit history. LLMs may research and propose; deterministic services retain authority over scheduling policy, risk validation, capital allocation, and broker execution.

## 2. Technology

- C# / .NET 10
- WPF for the Windows desktop UI
- MVVM for UI architecture
- SQLite for local persistence
- Entity Framework Core for database access
- .NET Generic Host for dependency injection, configuration, logging, bot execution, and lifecycle management
- NUnit for unit, integration, and acceptance-test execution
- Reqnroll for executable Gherkin specifications
- FlaUI UIA3 for Windows-only WPF journey automation

Package versions should be centrally managed and pinned. Nullable reference types, implicit usings, analyzers, and warnings-as-errors should be enabled across the solution.

## 3. Architectural Principles

1. **Safety before throughput.** Risk checks and order-state validation must occur before an order reaches a broker.
2. **One trading engine, multiple hosts.** WPF and headless processes compose and operate the same engine services.
3. **Platform-neutral domain.** Trading rules and domain models must not depend on WPF, Windows APIs, EF Core, SQLite, or a specific broker SDK.
4. **Explicit side effects.** Broker calls, persistence, clocks, market-data feeds, and notifications are accessed through abstractions.
5. **Recoverable execution.** Durable state and broker reconciliation allow the application to recover after a restart or network failure.
6. **Testable time and concurrency.** Scheduling uses an injected clock and cancellation tokens; strategy behavior can be replayed deterministically.
7. **Observable operation.** Important lifecycle, risk, order, and connectivity events are logged with structured context.
8. **LLMs propose; deterministic systems authorize.** An LLM cannot place an order, weaken a guardrail, modify an approved report, or grant itself additional authority.
9. **Bots are isolated investment mandates.** Bot identity, portfolio ownership, and broker accounts are modeled separately, with explicit capital ownership and hierarchical risk limits.
10. **Research is shared and versioned.** Research reports are immutable artifacts with provenance, freshness, and visibility metadata and may be reused by multiple Trading Bots.
11. **Persistence follows aggregate boundaries.** Application workflows load and save aggregate roots through repositories; persistence entities and query mechanisms do not leak into the domain.
12. **User behavior is executable.** Business journeys are specified in Gherkin and automated primarily through application services, with a smaller WPF automation suite for critical presentation paths.

The canonical entities, aggregates, invariants, and value objects are defined in [Domain Model](domain.md). Every domain aggregate, entity, and value object must be represented by an explicit C# class or record class in `Trading.Core`. Domain behavior and invariants must not exist only in EF Core configurations, database records, UI models, prompts, or untyped dictionaries.

## 4. Solution Structure

```text
TradingBot.sln

src/
    Trading.Core/
        Strategies/
        Orders/
        Positions/
        Risk/
        Portfolio/
        Lifecycle/
        Abstractions/

    Trading.Data/
        MarketData/
        HistoricalData/
        Persistence/
        Migrations/

    Trading.Brokers/
        Abstractions/
        Implementations/
        Mapping/

    Trading.Engine/
        Agents/
        Execution/
        Scheduling/
        Services/
        Reconciliation/

    Trading.Research/
        Orchestration/
        Reports/
        Sources/
        Tools/

    Trading.Host/
        Configuration/
        Program.cs

    Trading.UI.Wpf/
        Views/
        ViewModels/
        Commands/
        Converters/
        Services/
        App.xaml

tests/
    Trading.Core.Tests/
    Trading.Architecture.Tests/
    Trading.Data.Tests/
    Trading.Brokers.Tests/
    Trading.Engine.Tests/
    Trading.Research.Tests/
    Trading.IntegrationTests/
    Trading.AcceptanceTests/
        Features/
        Steps/
        Drivers/
        Support/
        Fixtures/
    Trading.UI.Wpf.AcceptanceTests/
        Features/
        Steps/
        Drivers/
        Pages/
        Support/
```

Test projects mirror production boundaries. `Trading.AcceptanceTests` contains cross-platform Reqnroll journeys executed through application services. `Trading.UI.Wpf.AcceptanceTests` contains the smaller Windows-only Reqnroll/FlaUI suite. View models remain testable without starting WPF.

## 5. Target Frameworks

```text
Trading.Core       net10.0
Trading.Data       net10.0
Trading.Brokers    net10.0
Trading.Engine     net10.0
Trading.Research   net10.0
Trading.Host       net10.0
Trading.UI.Wpf     net10.0-windows
Trading.AcceptanceTests            net10.0
Trading.UI.Wpf.AcceptanceTests     net10.0-windows
```

Only `Trading.UI.Wpf` and its Windows-only acceptance-test project may depend on Windows-specific APIs.

## 6. Project Responsibilities

### 6.1 Trading.Core

`Trading.Core` contains the domain model and business policies. It must remain free of infrastructure and UI dependencies.

Responsibilities:

- Strategy contracts and strategy decision models.
- Trading Bot definitions, mandates, portfolio assignments, and versioned risk profiles.
- Orders, executions, fills, positions, instruments, portfolios, and money/value objects.
- Order and bot lifecycle state machines.
- Risk policies, limits, validation results, and rejection reasons.
- Domain events and domain-specific exceptions.
- Interfaces required by domain logic, such as a clock or portfolio snapshot provider.

Guidelines:

- Use `decimal` for prices, quantities, balances, fees, and P&L calculations.
- Store timestamps as UTC using `DateTimeOffset` unless a more specialized type is adopted consistently.
- Keep broker-specific identifiers as mapped external references rather than primary domain identity.
- Prefer immutable records and explicit state transitions for commands and events.
- Do not reference EF Core attributes or broker SDK types.

### 6.2 Trading.Data

`Trading.Data` owns local data acquisition, storage, and retrieval infrastructure.

Responsibilities:

- EF Core `DbContext`, entity configurations, migrations, and SQLite configuration.
- Repositories or query services that implement interfaces consumed by the engine.
- Persistence for bot configuration, checkpoints, orders, fills, positions, and audit records.
- Persistence for bot runs, tool calls, portfolio snapshots, trade proposals, schedules, research requests, report metadata, and immutable report versions.
- Historical market-data import, storage, and queries.
- Optional normalized market-data cache.
- Transaction and database initialization services.
- Repository implementations for domain aggregate roots and dedicated read/query services.

The data layer maps persistence entities to domain objects. EF entities must not leak into `Trading.Core` or UI view models.

The platform uses the repository pattern at aggregate-root boundaries. Repository abstractions expose domain objects and intent-oriented operations and must not expose EF Core `DbSet<T>`, persistence entities, or `IQueryable`. Child entities and value objects do not receive independent repositories. Cross-aggregate transactions are coordinated through an explicit unit-of-work abstraction where atomicity is required. Read-heavy UI, reporting, catalog, and snapshot workflows may use dedicated query services rather than loading aggregates solely for projection.

### 6.3 Trading.Brokers

`Trading.Brokers` isolates external brokerage and live market-data APIs.

Responsibilities:

- Broker-neutral interfaces for connectivity, accounts, orders, executions, and market-data subscriptions.
- Broker implementations and SDK adapters.
- Translation between broker payloads and domain models.
- Authentication/configuration handling through injected options and secret providers.
- Rate-limit handling, retries for safe operations, connectivity status, and error normalization.

The engine must program against broker-neutral interfaces. Broker exceptions and DTOs must be translated before crossing the project boundary.

The provider-neutral `IPaperBrokerGateway` port belongs to Core so Engine can consume it and `Trading.Brokers` can
implement it without reversing the dependency graph. Paper adapters require `PaperBrokerOperationContext`; live
adapter ports remain structurally separate.

### 6.4 Trading.Engine

`Trading.Engine` is the application orchestration layer shared by both hosts.

Responsibilities:

- Bot start, stop, pause, resume, and shutdown coordination.
- Multi-bot supervision, per-bot execution leases, and run-budget enforcement.
- Trading Bot agent loops, tool dispatch, and immutable run records.
- Scheduling for manual, baseline, bot-requested, report-completion, and operational triggers.
- Market-data subscription coordination.
- Hierarchical risk-check pipeline, capital reservation, and order submission workflow.
- Order/fill processing, portfolio updates, and broker reconciliation.
- Background services registered with .NET Generic Host.
- Application use cases exposed to WPF or the headless host.

The engine coordinates domain and infrastructure abstractions but must not depend on WPF or other Windows-only APIs.

Application services in the engine load and persist aggregate roots through repository abstractions, coordinate domain behavior, and define transaction boundaries. They must not use EF Core directly.

Paper submission is an outbox-driven Engine application service. Its persistence port prepares an immutable broker
command from durable authorization facts and atomically finalizes the Order and claimed work item after normalized
broker I/O. The adapter call is bounded and occurs outside every database transaction. Accepted and duplicate-known
results bind one broker identity; unknown results require reconciliation before any further submit attempt.
Reconciliation claims durable work, commits the claim, and queries by the pinned paper account, environment, and stable
client order ID outside the database transaction. Found state is applied atomically with an immutable audit artifact.
Authoritative absence must survive a bounded grace period and repeated lookup before the original stable submit work is
made pending again; ambiguity, outage, identity mismatch, cancellation, and attempt exhaustion never submit directly.

Paper-execution startup recovers expired durable claims before workers become ready. An expired claimed submission is
atomically converted to an `Unknown` Order and source-keyed reconciliation work; it is never returned directly to the
submit worker. Required paper accounts reconcile before outbox or inbox claims begin. Recovery drains durable outbox
work before deferred broker events, persists one bounded account-scoped recovery audit, and leaves terminal poison work
isolated. Cancellation stops new drain cycles while the processors release claimed-but-unfinished work for restart.

### 6.5 Trading.Research

`Trading.Research` provides the shared Research Bot and research artifact service.

Responsibilities:

- Accept bounded research requests from any Trading Bot or authorized user.
- Run Research Bot tool loops with explicit time, token, cost, and tool-call budgets.
- Search approved sources and preserve evidence provenance and retrieval timestamps.
- Generate immutable, versioned research reports with freshness and visibility metadata.
- Catalog, deduplicate, retrieve, expire, and refresh reports.
- Notify interested Trading Bots when requested reports complete or fail.
- Treat external content as untrusted evidence rather than executable instructions.

The Research Bot cannot propose or place trades. Detailed behavior is defined in [Research Bot](research-bot.md).

### 6.6 Trading.Host

`Trading.Host` is the cross-platform console/headless composition root.

Responsibilities:

- Build and run the .NET Generic Host.
- Load configuration and environment-specific settings.
- Register engine, data, broker, logging, and health services.
- Handle process signals and graceful shutdown.
- Select configured bots and run them without a desktop UI.

It should contain little business logic. Operational commands should call engine use cases.

### 6.7 Trading.UI.Wpf

`Trading.UI.Wpf` is the Windows-only composition root and presentation layer.

Responsibilities:

- Views, view models, commands, converters, and navigation.
- Display bot status, positions, orders, fills, risk state, logs, and connectivity.
- Send user actions to engine application services.
- Marshal UI-bound notifications onto the WPF dispatcher.
- Host the same Generic Host services used by `Trading.Host` and stop them cleanly during application exit.
- Expose stable UI Automation IDs, accessible names, roles, and state for critical interactive controls.

View models must not call broker SDKs or `DbContext` directly. Long-running operations must be asynchronous, cancellable, and surfaced to users with progress and error state.

Critical WPF journeys are automated through FlaUI UIA3 page/component objects. UI tests must not depend on screen coordinates, color alone, animation timing, or fragile display-text selectors when a stable automation identifier can be provided.

## 7. Dependency Direction

The intended high-level dependency direction is:

```text
Trading.UI.Wpf ─┐
                ├──> Trading.Engine ─────> Trading.Core
Trading.Host ───┘          │
                           ├──> Trading.Data ───────> Trading.Core
                           ├──> Trading.Brokers ────> Trading.Core
                           └──> Trading.Research ───> Trading.Core
```

Allowed project references:

| Project | May reference |
| --- | --- |
| `Trading.Core` | No other production project |
| `Trading.Data` | `Trading.Core` |
| `Trading.Brokers` | `Trading.Core` |
| `Trading.Research` | `Trading.Core`, plus application-owned LLM and source abstractions |
| `Trading.Engine` | `Trading.Core`, `Trading.Data`, `Trading.Brokers`, `Trading.Research` |
| `Trading.Host` | `Trading.Engine` and projects needed for composition/registration |
| `Trading.UI.Wpf` | `Trading.Engine` and projects needed for composition/registration |

`Trading.Core`, `Trading.Data`, `Trading.Brokers`, `Trading.Engine`, and `Trading.Host` must never reference `Trading.UI.Wpf`.

Where practical, dependency-injection registration can be exposed by each infrastructure project through methods such as `AddTradingData` and `AddTradingBrokers`. This keeps host startup code explicit without moving composition into the domain.

## 8. Runtime Model

### 8.1 Windows Desktop

```text
Trading.UI.Wpf
      │
      ├── starts/stops Generic Host
      ▼
Trading.Engine
      │
      ├── Trading.Data
      └── Trading.Brokers
```

WPF starts the Generic Host during application startup. Closing the application requests graceful engine shutdown, waits for in-flight state persistence within a bounded timeout, and then disposes the host.

### 8.2 Headless Windows or Linux

```text
Trading.Host
      │
      ├── runs Generic Host
      ▼
Trading.Engine
      │
      ├── Trading.Data
      └── Trading.Brokers
```

The headless process responds to `Ctrl+C`, `SIGINT`, and `SIGTERM` through Generic Host lifetime handling. It composes the Trading and shared Research supervisors, proposal governance, and paper execution services. Database migrations and recovery complete before readiness or new claims; shutdown stops accepting work, propagates cancellation, and leaves durable work recoverable. The local smoke uses the deterministic simulated broker to demonstrate conversion, timeout-after-acceptance reconciliation, acknowledgement, partial and final Fill accounting, duplicate protection, projections, and clean shutdown without any live adapter or network authority.

## 9. Core Execution Flow

Trading Bots are scheduled, stateful agents rather than processes that must react continuously to market ticks. A run begins from a controlled trigger and may consult market data or shared research as tools:

```text
Manual, scheduled, report-completion, or operational trigger
    -> acquire exclusive per-bot run lease
    -> reconcile broker account and portfolio state
    -> build immutable point-in-time portfolio snapshot
    -> run bounded Trading Bot LLM tool loop
         -> fetch market data
         -> request/list/read research reports
         -> write sandboxed working artifacts
         -> create zero or more structured trade proposals
         -> finish with rationale and requested next-run time
    -> validate requested schedule against platform policy
    -> validate proposals using fresh execution-time state
    -> reserve capital and persist guardrail decisions
    -> submit approved orders through broker adapter
    -> process acknowledgements, rejections, executions, and fills
    -> update portfolio state and immutable run record
```

The Trading Bot workflow is specified in [Trading Bot](trading-bot.md). Research requests are handled by the shared workflow in [Research Bot](research-bot.md).

Key rules:

- An LLM creates a structured trade proposal; it never receives a broker-order submission tool.
- Order execution is available only through deterministic Engine ports. The production paper broker port accepts a
  typed paper-environment context on every operation; a live-environment identity is not assignable to it. Broker
  adapters return bounded provider-neutral results and stable codes, while provider DTOs and exceptions remain inside
  `Trading.Brokers`.
- The versioned proposal-tool dispatcher records only immutable proposals after validating canonical
  arguments against the Bot Run's pinned identity, configuration, Portfolio snapshot, evidence
  visibility, tool policy, and budgets; it has no approval, reservation, order, or broker port.
- The deterministic engine converts an approved proposal into an order intent only after validation against fresh state.
- Stage 5 proposal orchestration ends at an atomic capital reservation: it performs initial validation, exact human review, post-approval fresh-state revalidation, and recoverable expiration without resolving an order or broker service.
- A proposal pins its configuration's execution mode when recorded. `ResearchOnly` proposals use the same
  identity, evidence, persistence, and structured guardrail evaluation pipeline, then terminate with the stable
  `proposal_governance.research_only` disposition; their application graph cannot approve, reserve, convert, or
  submit them, even if the Bot is later promoted to another mode.
- The risk pipeline is mandatory and cannot be bypassed by a strategy or UI command.
- A bot-requested next-run time is advisory and is bounded by platform scheduling policy and a baseline schedule.
- Each run has deterministic wall-clock, token, cost, tool-call, research-request, and proposal limits.
- A timed-out or incomplete run creates no implicit proposal or order.
- Every submission uses a stable client order ID/idempotency key where supported.
- Unknown outcomes caused by timeouts are reconciled before a retry is attempted.
- Order and fill handlers must tolerate duplicated and out-of-order broker messages.
- Material transitions are persisted and logged with bot, strategy, account, instrument, and correlation identifiers.

## 10. Bot and Run Lifecycle

Recommended lifecycle states:

```text
Created -> Starting -> Running <-> Paused -> Stopping -> Stopped
                        │                        │
                        └--------> Faulted <----┘
```

Transitions must be explicit and validated. Starting should include configuration validation, database readiness, broker connectivity, account reconciliation, and readiness of any data providers required by the bot's mandate. A bot does not require a continuous market-data subscription unless its configured workflow uses one, and it must not enter `Running` until its own prerequisites succeed.

A persistent Trading Bot may execute many isolated runs. Recommended run states are:

```text
Pending -> AcquiringLease -> PreparingSnapshot -> Reasoning -> Completed
                                                │          ├-> TimedOut
                                                │          ├-> BudgetExceeded
                                                │          ├-> Cancelled
                                                │          └-> Faulted
                                                └-> WaitingForTool
```

Only one run per Trading Bot may be active. Concurrent triggers are durably coalesced as pending trigger reasons. Research Bots may execute concurrently because they publish immutable reports rather than mutate portfolios.

Shutdown order:

1. Reject new start and order requests.
2. Cancel schedules and active bot/tool loops.
3. Decide how open orders are handled according to configured shutdown policy.
4. Drain or checkpoint event processing.
5. Persist final known state.
6. Disconnect market-data and broker sessions.

## 11. Multi-Bot, Portfolio, and Account Isolation

The domain distinguishes four identities:

```text
BrokerConnection
    -> BrokerAccount
        -> Portfolio
            -> TradingBot
```

- A `TradingBot` is a versioned investment mandate with a goal, time horizon, eligible universe, risk tolerance, budgets, prompt/model configuration, and scheduling policy.
- A `Portfolio` owns assigned capital, positions, accounting entries, proposals, and performance history.
- A `BrokerAccount` represents the broker's authoritative account state.
- A `BrokerConnection` contains connectivity and authentication configuration and may expose multiple accounts.

Initial live-trading constraints:

- Exactly one active Trading Bot manages a portfolio.
- Prefer one portfolio per broker account or broker subaccount.
- A bot cannot access another bot's working files, run context, proposals, portfolio, or private reports.
- Shared reports are read-only and immutable.
- One bot's failure, timeout, or budget exhaustion must not stall another bot.

Virtual portfolios sharing one broker account are deferred until the platform has an internal double-entry-style allocation ledger, cash reservations, fill/fee/dividend allocation, account-level coordination, and an explicit conflict/netting policy. If shared accounts are later enabled, the account coordinator must serialize capital-affecting operations and reconcile internal ownership against the broker's net positions.

## 12. Risk Architecture

Risk evaluation should be a composable hierarchy operating on the proposal plus fresh account and portfolio state:

```text
Platform limits
    -> Broker-account limits
        -> Portfolio limits
            -> Trading Bot mandate
                -> Proposal and execution-time checks
```

A lower layer may make a constraint stricter but cannot weaken a parent constraint. Kill switches exist at platform, account, portfolio, and bot levels.

Initial risk controls should include:

- Trading-enabled and kill-switch checks.
- Allowed account, broker, instrument, and market checks.
- Market-hours and stale-market-data checks.
- Maximum order quantity and notional value.
- Price deviation and limit-price sanity checks.
- Maximum position and concentration.
- Available cash, buying power, or margin.
- Maximum open orders and order frequency.
- Daily realized/unrealized loss limits.
- Duplicate intent/idempotency checks.

Risk decisions should produce structured results containing the rule, outcome, reason, measured value, limit, and timestamp. Rejections must be durable and visible in both hosts.

## 13. Persistence Design

SQLite is the local system of record for application-owned state. Initial logical tables should cover:

The canonical physical schema, EF Core mapping rules, repository contracts, indexes, and transaction boundaries are defined in [Data Model](data-model.md).

- Bot definitions and runtime checkpoints.
- Versioned bot mandates, risk profiles, model/prompt settings, tool permissions, and budgets.
- Accounts and broker mappings.
- Portfolio ownership, capital assignments, and accounting entries.
- Instruments and symbol mappings.
- Order intents, broker orders, status transitions, executions, and fills.
- Position and portfolio snapshots.
- Risk decisions and trading halts.
- Bot runs, triggers, leases, input snapshots, tool calls/results, finish records, and accepted/requested wake times.
- Structured trade proposals and links to the snapshots and reports used.
- Immutable proposal approvals, guardrail evaluations, and capital reservations.
- Research requests, subscriptions, source provenance, report versions, visibility, freshness, and expiration.
- Historical bars/ticks where local retention is required.
- Application audit events.

Persistence rules:

- Enable SQLite WAL mode when compatible with the deployment environment.
- Keep transactions short and never hold one open during a network call.
- Use unique constraints for client order IDs, broker execution IDs, and other idempotency keys.
- Apply migrations explicitly during startup or deployment; do not silently mutate production schema without logging and failure handling.
- Store UTC timestamps and preserve source timestamps when they are operationally relevant.
- Configure database paths through options and resolve them with cross-platform filesystem APIs.
- Back up the database before destructive migrations and document restore procedures.

SQLite supports a single-node local runtime well. If multiple processes must write shared trading state, move persistence behind a service or adopt a client/server database rather than sharing a SQLite file over a network filesystem.

## 14. Concurrency and Messaging

- Use async APIs end-to-end for I/O.
- Pass `CancellationToken` through engine, data, and broker operations.
- Prefer bounded `Channel<T>` queues for internal event streams so backpressure is explicit.
- Define ownership for mutable state; avoid multiple services mutating the same order or portfolio object.
- Process events requiring strict ordering through a single logical partition, such as account plus instrument or order ID.
- Never block asynchronous code with `.Result`, `.Wait()`, or dispatcher waits.
- Keep strategy execution isolated so one slow or faulty strategy cannot stall unrelated bots.
- Use a durable lease to enforce one active run per Trading Bot.
- Enforce global and per-bot concurrency limits for LLM, research, market-data, and broker resources.
- Coalesce simultaneous triggers without losing their reasons.
- Deduplicate equivalent research requests and notify all interested bots when the shared report completes.

If an in-process mediator or message library is introduced, its contracts must remain application-owned and must not replace clear domain boundaries.

## 15. Configuration and Secrets

Use the standard .NET configuration pipeline:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables
4. Command-line arguments for the headless host
5. Local development secrets where appropriate

Configuration should be bound to validated options at startup. API keys and tokens must not be committed to source control or written to logs. Secret storage is supplied through a cross-platform abstraction; the headless Linux runtime should support environment variables, mounted secret files, or an external secret store.

## 16. Logging, Metrics, and Health

Use `Microsoft.Extensions.Logging` with structured properties. At minimum, record:

- Host and bot lifecycle transitions.
- Broker connections, reconnects, throttling, and failures.
- Strategy decisions at an appropriate configurable level.
- Bot-run triggers, snapshot IDs, model/prompt versions, tool calls, budgets, report dependencies, finish rationales, and scheduling decisions.
- Risk approvals and rejections.
- Order submissions, acknowledgements, status changes, cancellations, and fills.
- Reconciliation mismatches and corrective actions.
- Database migration and persistence failures.

Sensitive credentials and unnecessary personal/account data must be redacted. Include correlation IDs so a decision can be traced from market event through order and fill.

The headless host should expose or publish readiness/liveness information appropriate to its deployment. Readiness should remain false until database initialization and required broker connectivity/reconciliation complete.

## 17. Resilience and Recovery

- Retry only operations known to be safe or idempotent, with bounded exponential backoff and jitter.
- Apply timeouts to all external broker and market-data calls.
- Use reconnect state machines rather than unbounded retry loops.
- Stop or pause affected bots when market data is stale, account state is uncertain, or reconciliation fails.
- On startup, load durable state and reconcile open orders, recent executions, balances, and positions with the broker before trading.
- Treat the broker as authoritative for broker-side execution state and retain discrepancies in an audit trail.
- Provide an operator-accessible kill switch that prevents new orders and follows a separately configured policy for existing orders.
- Recover expired bot-run leases after a host crash without repeating trade proposals or submissions.
- Preserve partial Research Bot results as failed-run artifacts without publishing them as completed reports.

## 18. Cross-Platform Rules

- Keep all non-UI projects on `net10.0`.
- Keep Windows-specific code inside `Trading.UI.Wpf`.
- Do not use Windows Registry, COM, WMI, Windows Event Log, Windows Credential Manager, or native Windows DLLs in cross-platform projects.
- Use `Path.Combine`, `Path.GetFullPath`, `Environment.SpecialFolder`, and other cross-platform .NET APIs for filesystem access.
- Treat filenames and configuration keys derived from filenames as case-sensitive.
- Ensure every broker SDK used by `Trading.Brokers` supports Linux; otherwise isolate it in a separately deployed adapter rather than contaminating the engine.
- Enable .NET platform compatibility analysis.
- Build and test all non-UI projects on both Windows and Linux on every commit.
- Avoid assumptions about path separators, drive letters, newline format, locale, decimal formatting, and local time zone.
- Use invariant culture for machine-readable values and explicit culture only for UI display.

Recommended project settings:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

The WPF project replaces the target framework with `net10.0-windows` and enables WPF.

## 19. Testing Strategy

The canonical testing layers, journey catalog, fixtures, CI selection, and definition of done are specified in [Test Plan](test-plan.md).

### Test Layers

- Unit tests cover domain invariants, exact financial calculations, state machines, policies, budgets, scheduling, and view-model behavior.
- Data integration tests use the real EF Core SQLite provider with isolated temporary databases; the EF in-memory provider is not a substitute for relational behavior.
- Component/integration tests compose the Generic Host with production engine services and deterministic external substitutes.
- Reqnroll/NUnit acceptance tests express user-visible behavior in Gherkin and execute primarily through application services on Windows and Linux.
- A smaller Reqnroll/FlaUI UIA3 suite exercises critical WPF presentation journeys on Windows.
- Broker contract tests validate every adapter against common behavior using simulators, recorded fixtures, or broker sandboxes.
- Reliability tests reproduce failure windows around leases, transactions, order submission, fills, inbox/outbox processing, and restart recovery.
- Real-model evaluations are separate from deterministic acceptance tests and never receive broker execution authority.

### Deterministic Test Environment

Commit-gating tests must not call real LLMs, the public web, live market data, or live broker accounts. Tests use dependency injection to provide a fake clock, deterministic ID generator, scripted LLM client, simulated broker, fixture-backed market and research providers, temporary SQLite database, and captured inbox/outbox dispatchers.

Gherkin step definitions remain thin and call reusable domain drivers. They must not call repositories, `DbContext`, or broker SDKs directly. WPF selectors and interaction details belong in page/component objects.

Every aggregate invariant, risk-rule boundary, financial calculation, material idempotency key, and migration path requires explicit verification. Live-money orders are prohibited from every automated test suite.

## 20. CI/CD and Quality Gates

Every commit should run:

- Restore with locked/pinned dependencies.
- Format and analyzer validation.
- Unit, SQLite integration, component, and non-UI Reqnroll acceptance tests.
- `net10.0` build and deterministic tests on Windows and Linux.
- WPF build and a small FlaUI smoke suite on Windows in an interactive test environment.
- Architecture tests that enforce forbidden project references and Windows API usage boundaries.
- Dependency vulnerability and secret scanning.

Scheduled or pre-release validation should add the full WPF journey suite, broker-sandbox contracts, reliability/recovery scenarios, migration upgrade fixtures, performance trends, and separately reported real-model evaluations.

Release artifacts should be produced separately for the WPF desktop application and the headless host. Deployment configuration and secrets must remain outside the artifacts.

## 21. Implementation Plan

The canonical staged delivery sequence and exit criteria are defined in [Implementation Plan](implementation-plan.md). Each stage produces an independently testable increment and must satisfy its automated acceptance criteria before the next stage begins.

The delivery milestones are:

1. Solution foundation and domain model.
2. Persistence and Portfolio state.
3. Multi-bot runtime and scheduling.
4. Shared Research Bot.
5. Trade Proposals, Approvals, and risk.
6. Paper-order execution.
7. WPF operator interface.
8. Recovery and production hardening, producing the paper-trading MVP.
9. First live-broker integration as a separately authorized stage.

## 22. Early Architecture Decisions to Record

Create Architecture Decision Records under `docs/adr/` as decisions are made. The first decisions should cover:

1. Domain event and in-process messaging approach.
2. Order-state persistence and reconciliation semantics.
3. Market-data representation, retention, and replay format.
4. Strategy plugin/loading model and isolation boundary.
5. SQLite migration and backup policy.
6. Secret-provider strategy for Windows and Linux.
7. First broker and the evidence for its Linux compatibility.
8. Deployment model for the headless host.
9. LLM provider abstraction, prompt/version retention, budgets, and tool-loop semantics.
10. Trading Bot-to-portfolio ownership and future virtual-portfolio accounting model.
11. Research report schema, provenance requirements, visibility, expiration, and deduplication.
12. Human approval and promotion policy across research-only, paper, and live execution modes.

## 23. Out of Scope for the Initial Slice

- Multi-node engine coordination.
- Shared SQLite access from multiple application processes.
- High-frequency or ultra-low-latency execution.
- Arbitrary third-party strategy code running without isolation.
- Automatic live trading before paper-trading, reconciliation, and recovery paths are proven.
- Multiple Trading Bots independently managing the same portfolio.
- Virtual portfolios sharing one broker account without a complete allocation ledger and account coordinator.
- Unrestricted LLM filesystem access, arbitrary generated code execution, or direct LLM access to broker submission APIs.

These capabilities can be revisited when operational requirements justify the added complexity.

# Test Plan

## 1. Purpose

This document defines the verification strategy for the trading platform. It covers unit tests, integration tests, executable Gherkin acceptance tests, WPF UI automation, broker contract tests, reliability tests, migrations, and LLM evaluations.

The objective is not merely high code coverage. The test system must provide evidence that:

- Domain invariants hold.
- Financial calculations are exact and reproducible.
- LLMs cannot bypass deterministic authority boundaries.
- Multiple Trading Bots remain isolated.
- Research and trade decisions are traceable to immutable inputs.
- Orders, fills, reservations, and ledger entries are idempotent.
- The application recovers safely from failures and restarts.
- Critical user journeys work through both application services and the WPF interface.

## 2. Testing Principles

1. **Test behavior at the lowest useful level.** Most rules belong in fast unit tests; cross-component behavior belongs in integration or acceptance tests.
2. **Keep acceptance language business-facing.** Gherkin describes goals, actions, and observable outcomes rather than controls, classes, SQL, or implementation steps.
3. **Keep deterministic tests deterministic.** Commit-gating tests do not call real LLMs, the public web, live market data, or live broker accounts.
4. **Treat financial side effects as hostile to duplication.** Submission, fill processing, ledger posting, notifications, and scheduling receive explicit idempotency tests.
5. **Test authority boundaries.** Negative tests prove that LLM tools, UI commands, and infrastructure adapters cannot bypass guardrails.
6. **Preserve production composition.** Integration and acceptance tests replace external boundaries through dependency injection rather than reimplementing engine behavior.
7. **Use real infrastructure where its behavior matters.** EF Core integration tests use SQLite rather than an EF in-memory provider.
8. **Make failures diagnosable.** Tests retain relevant logs, identifiers, database paths, screenshots, and scenario context.

## 3. Test Layers

```text
                  WPF journey tests
               Small, Windows-only suite
                         ▲
                         │
              Gherkin acceptance tests
          Business workflows through application APIs
                         ▲
                         │
        Integration, adapter, and contract tests
        EF Core, SQLite, host, broker, scheduling
                         ▲
                         │
                    Unit tests
        Domain rules, value objects, state machines
```

The upper layers are intentionally smaller. A behavior should not be retested through WPF merely because it already has unit and application-level coverage; UI tests prove presentation wiring and critical journeys.

## 4. Test Project Structure

```text
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
            BotManagement/
            Portfolio/
            Research/
            TradeProposals/
            Execution/
            Risk/
            Recovery/
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

Target frameworks:

```text
Trading.Core.Tests                 net10.0
Trading.Architecture.Tests         net10.0
Trading.Data.Tests                 net10.0
Trading.Brokers.Tests              net10.0
Trading.Engine.Tests               net10.0
Trading.Research.Tests             net10.0
Trading.IntegrationTests           net10.0
Trading.AcceptanceTests            net10.0
Trading.UI.Wpf.AcceptanceTests     net10.0-windows
```

Test-only framework choices:

- NUnit as the common test runner.
- Reqnroll with its NUnit integration for executable Gherkin.
- FlaUI UIA3 for Windows UI Automation of WPF.
- The built-in .NET test SDK and coverage collector.
- Assertion and mocking libraries may be selected through central package management, but tests should not obscure behavior behind excessive mocking.

Production projects must not reference Reqnroll, NUnit, FlaUI, or test-support assemblies.

## 5. Unit Tests

Unit tests run entirely in process without a database, filesystem, network, WPF dispatcher, wall clock, or LLM provider. They test one domain behavior or application policy using explicit inputs.

### 5.1 Domain Model

Required coverage includes:

- Construction and equality of strongly typed IDs and value objects.
- `Money`, `Price`, `Quantity`, percentage, currency, precision, and rounding rules.
- Bot and Bot Run lifecycle transitions.
- Configuration version immutability and activation.
- Portfolio ownership and one-bot-per-portfolio rules.
- Position quantity, average cost, fees, realized P&L, and corporate-action calculations.
- Research request, report, and hypothesis lifecycle rules.
- Proposal state transitions and proposal expiration.
- Approval binding to proposal version and state snapshot.
- Capital Reservation creation, consumption, release, expiration, and idempotency.
- Order transition state machine, partial fills, cancellation, rejection, and unknown states.
- Hierarchical risk-policy composition and rule-result construction.

Every aggregate invariant must have at least one positive and one negative test. State machines should use table-driven cases for allowed and forbidden transitions.

### 5.2 Engine and Application Policies

- Trigger eligibility and trigger coalescing.
- Requested versus accepted scheduling decisions.
- Per-run token, cost, time, tool, research, and proposal budgets.
- Tool authorization and schema validation.
- Market-data freshness decisions.
- Proposal-to-order construction.
- Capital availability including active reservations.
- Retry classification for safe, unsafe, and unknown operations.
- Report visibility and freshness policy.
- Redaction and audit-field selection.

### 5.3 Test Doubles

Prefer small hand-written fakes for stable domain ports:

- `FakeClock`
- `DeterministicIdGenerator`
- `ScriptedLlmClient`
- `StubMarketDataProvider`
- `SimulatedBroker`
- `InMemoryResearchSource`
- `FixtureResearchSource` with an embedded versioned manifest, exact UTF-8 byte counts, SHA-256 hashes, and adversarial untrusted-content payloads
- `CapturingOutboxDispatcher`

Mocks are appropriate for verifying a narrow collaboration, but tests should prefer assertions on returned state, domain events, and durable outcomes over large invocation scripts.

## 6. Data Integration Tests

`Trading.Data.Tests` uses temporary, isolated SQLite databases and the real EF Core SQLite provider. Do not use EF Core's in-memory provider as a substitute for relational behavior.

Each test receives a unique database path, applies migrations, and disposes the database afterward. Failed CI tests may retain the database as an artifact.

Required tests:

- Round-trip every aggregate, entity, and value object.
- Strongly typed ID converters.
- Exact decimal canonicalization, bounds, and unsupported precision.
- UTC timestamp conversion and ordering.
- JSON schema version and canonical hash stability.
- Foreign keys and `ON DELETE RESTRICT` behavior.
- Unique and partial unique indexes.
- Repository mapping without leaking EF types.
- No-tracking read projections.
- Optimistic concurrency conflicts.
- One active Bot Run lease per Trading Bot.
- One active Capital Reservation per proposal.
- Immutable configuration, snapshot, report, fill, approval, evaluation, and ledger records.
- Duplicate inbox messages, broker executions, and fill applications.
- Atomic outbox creation with state changes.
- Transaction rollback during approval, order creation, and fill application.
- WAL and busy-timeout configuration where supported by the test environment.

## 7. Migration Tests

Every migration is tested in two paths:

1. Apply all migrations to an empty database.
2. Upgrade a fixture created from the previous released schema.

Migration assertions verify:

- Expected schema objects and indexes exist.
- IDs, exact decimals, timestamps, hashes, and relationships are preserved.
- Immutable audit history remains readable.
- Required backfills are deterministic and idempotent.
- Destructive changes fail safely or require an explicit migration procedure.
- The application starts successfully on the upgraded database.

Production startup must not use `EnsureCreated`.

## 8. Component and Integration Tests

`Trading.IntegrationTests` composes the Generic Host with production engine and data services while replacing external providers.

Core workflows:

- Manual and scheduled Bot Run creation.
- Broker reconciliation followed by decision-snapshot generation.
- Bounded scripted LLM tool loop.
- Table-driven Research model-loop tests exercise each wall-clock, token, cost, total/per-tool call, document, retained-byte, consecutive-failure, transcript, cancellation, malformed-response, missing-draft, and missing-finish boundary without network access or wall-clock waits.
- Research request, deduplication, report publication, and subscriber notification.
- Proposal recording followed by hierarchical validation.
- Human approval and capital reservation.
- Paper-order submission through the outbox.
- Broker acknowledgement, partial fill, final fill, position update, and ledger posting.
- Trigger arrival while a bot is already running.
- Host shutdown and restart with active leases or pending outbox work.
- Multiple Trading Bots using shared research while preserving portfolio and artifact isolation.

Integration tests use a fake clock where possible and explicitly advance it. Time-based background services expose a test seam so tests do not wait for real-world timer intervals.

## 9. Broker Adapter Contract Tests

Every broker adapter must pass a common contract suite against a simulator, recorded fixtures, or the broker's sandbox.

The contract covers:

- Capability discovery.
- Instrument mapping.
- Account, cash, position, and open-order normalization.
- Stable client order IDs.
- Order submission, rejection, cancellation, and expiration.
- Partial and complete fills.
- Duplicate and out-of-order events.
- Rate-limit normalization.
- Authentication and connectivity failures.
- Timeout producing an unknown submission outcome.
- Reconciliation after disconnection.
- Redaction of credentials and sensitive payloads.

Sandbox tests are tagged and do not gate every commit because they depend on external availability. No automated test uses live money.

## 10. Gherkin Acceptance Tests

Reqnroll executes Gherkin feature files through NUnit. Most scenarios run against application services in `Trading.AcceptanceTests` without launching WPF.

### 10.1 Feature Style

Feature files use domain language and describe observable outcomes:

```gherkin
Feature: Human approval of trade proposals

  Scenario: Approve a valid paper-trading proposal
    Given the "Retirement Growth" bot uses human approval mode
    And its portfolio has 10000 USD available
    And the bot has proposed buying 10 shares of AAPL at 200 USD
    When the user approves the proposal
    Then the proposal should be validated against current portfolio state
    And 2000 USD should be reserved
    And a paper order should be created
    And the approval should identify the approving user
```

Avoid scenarios such as “click the blue button” or “row X exists in table Y.” Those details belong in drivers and WPF page objects.

### 10.2 Initial Journey Catalog

#### Bot Management

- Create a Trading Bot with a valid mandate.
- Reject an invalid or incomplete mandate.
- Assign a bot to an unowned portfolio.
- Reject assignment when another active bot owns the portfolio.
- Pause, resume, and retire a bot.
- Promote a bot from research-only to paper trading.
- Prevent unauthorized promotion to live trading.

#### Bot Runs and Scheduling

- Trigger a run manually.
- Start a scheduled run.
- Reconcile state and create an immutable decision snapshot.
- Finish without proposing a trade.
- Accept a valid requested wake time.
- Bound an invalid or excessively frequent wake request.
- Coalesce triggers received during an active run.
- Recover an expired lease after a host restart.
- Stop safely when tool, token, cost, or time budget is exhausted.

#### Research

- Request a bounded company report.
- Reject an unbounded or unauthorized request.
- Reuse a fresh equivalent shared report.
- Deduplicate equivalent concurrent requests.
- Keep a private report inaccessible to other bots.
- Publish an immutable report with source provenance.
- Create a new report version on refresh.
- Notify every authorized subscriber of completion or failure.
- Reject instructions embedded in retrieved source material.

#### Trade Proposals and Risk

- Record a direct-trade proposal without placing an order.
- Record a target-allocation proposal.
- Reject a proposal for an unassigned portfolio or prohibited instrument.
- Reject an expired proposal.
- Reject stale market data.
- Apply platform, account, portfolio, and bot limits in order.
- Require human approval in the configured mode.
- Bind approval to the reviewed proposal version.
- Revalidate against fresh state after approval.
- Preserve structured rejection reasons.

#### Capital and Concurrency

- Reserve capital for an approved proposal.
- Prevent two proposals from reserving the same available capital.
- Release a reservation after rejection, cancellation, or expiration.
- Consume a reservation as an order fills.
- Isolate capital between portfolios and Trading Bots.

#### Orders and Fills

- Create an order only from an approved proposal.
- Submit an order once despite outbox retries.
- Reconcile an unknown submission outcome before retry.
- Apply partial and final fills.
- Ignore duplicate executions.
- Reject an invalid order transition.
- Update Position and ledger atomically with a fill.
- Recover pending order work after restart.

#### Operational Safety

- Halt one bot without halting unrelated bots.
- Apply bot, portfolio, account, and platform kill switches.
- Refuse new orders when account reconciliation is uncertain.
- Shut down gracefully with active Bot Runs.

### 10.3 Steps and Drivers

```text
Gherkin step
    -> thin step definition
        -> domain driver
            -> application service or query service
```

Step definitions translate domain language and manage scenario context. Drivers perform reusable operations. Steps must not call repositories, `DbContext`, or broker SDKs directly.

Each scenario receives:

- A fresh host or explicitly reset scenario scope.
- A unique temporary SQLite database.
- A fake clock with an explicit initial instant.
- Deterministic IDs where assertions require them.
- Scripted LLM and external-provider responses.
- Captured structured logs and outbox activity.

Acceptance drivers own production composition and persistence inspection. Failure diagnostics identify the scenario database and the stable request, run, report, source, subscriber, notification, trigger, and Bot Run context without exposing provider payloads. Feature steps remain limited to application-facing actions and observable query results.

## 11. WPF UI Acceptance Tests

`Trading.UI.Wpf.AcceptanceTests` uses Reqnroll and FlaUI UIA3 on Windows. It contains only critical presentation journeys:

- Create and configure a Trading Bot.
- Pause and resume a bot.
- View portfolio and reconciliation state.
- Request and read a research report.
- Review, approve, and reject a proposal.
- Observe a guardrail rejection.
- Distinguish research, paper, and live execution modes.
- Activate a kill switch.
- Inspect order and fill status.
- See stale-data, disconnected-broker, and failed-run warnings.

Example:

```gherkin
@ui @windows
Scenario: Approve a proposal from the proposal queue
  Given a valid proposal is awaiting my approval
  When I open the proposal queue
  And I review the proposal
  And I approve it
  Then the proposal should be shown as approved
  And its paper order should be visible
```

### 11.1 UI Testability Requirements

- Every interactive or asserted control has a stable `AutomationId`.
- Controls expose accessible names, roles, and state.
- Tests never depend on screen coordinates, color alone, animation timing, or localized display text when a stable ID is available.
- Page/component objects own FlaUI selectors and interaction details.
- Every UI wait has a bounded timeout and useful failure description.
- The test startup profile uses a temporary database and simulated services.
- Dialogs and notifications are deterministic and automation-accessible.
- Failure artifacts include a screenshot, UI Automation tree where practical, application logs, and scenario name.
- Tests launch and close the application through a shared fixture that cleans up orphaned processes.

## 12. LLM Testing and Evaluation

Deterministic product tests use `ScriptedLlmClient`, not a real model. A script defines expected tool calls and responses, for example:

```text
For Bot Run R:
    call GetReport with report RPT-1
    call ProposeTrade with proposal P-1
    call Finish with nextRunAt 2026-08-21T13:00:00Z
```

Tests verify orchestration, schema validation, permissions, budgets, proposal recording, and safe termination.

Real-model evaluations are a separate, non-deterministic suite. They may measure:

- Tool-selection correctness.
- Schema adherence.
- Citation and report-grounding behavior.
- Resistance to prompt injection in source content.
- Abstention when evidence is missing.
- Consistency of proposed hypotheses.
- Cost, latency, and tool-use distributions.

Model evaluations use fixed datasets and scored rubrics, report distributions rather than binary guarantees, and do not gate ordinary commits until explicit stability thresholds are adopted. They never receive broker execution authority.

## 13. Reliability and Recovery Tests

Required fault scenarios include:

- Process termination after order intent commit but before broker submission.
- Broker acceptance followed by client timeout.
- Duplicate, delayed, and out-of-order broker events.
- Database lock contention and optimistic-concurrency conflicts.
- Host restart with expired Bot Run leases.
- Host restart with unprocessed inbox or outbox messages.
- Research run failure after partial artifacts are written.
- LLM timeout without `Finish`.
- Market data becoming stale between proposal and execution.
- Approval followed by changed account buying power.
- Partial fill followed by cancellation.
- Graceful shutdown with active runs and queued work.

Use deterministic failpoints around transaction commits and external calls so each failure window can be reproduced.

## 14. Security and Authorization Tests

- A Trading Bot cannot access another bot's private report or artifacts.
- A bot cannot operate on an unassigned portfolio.
- Research tools cannot invoke trade or broker operations.
- LLM-generated content cannot change tool permissions, prompts, risk limits, or execution mode.
- External content is treated as evidence, not instruction.
- Credentials and tokens are absent from logs, database payloads, reports, screenshots, and test artifacts.
- Approval records require an authorized actor.
- Live-mode promotion and kill-switch actions are audited.
- Read projections enforce report and portfolio visibility.

## 15. Performance Tests

Performance tests begin after the paper-trading vertical slice is stable. Initial objectives should be expressed as measured budgets rather than premature production guarantees:

- Scheduler scan and trigger-claim time for the expected bot count.
- Snapshot generation time for the expected portfolio size.
- Proposal validation latency excluding external data retrieval.
- Fill-processing transaction latency.
- Report catalog query latency.
- Outbox drain rate and recovery backlog time.
- WPF responsiveness while background updates arrive.

Performance tests use representative data volumes and publish trend results. They should not weaken correctness, durability, or exact-decimal behavior to satisfy a benchmark.

## 16. Tags and Test Selection

Suggested Reqnroll tags:

```text
@acceptance
@ui
@windows
@research
@paper-trading
@broker-sandbox
@requires-network
@slow
@recovery
```

Tags describe infrastructure and selection needs, not release status. Tests tagged `@requires-network` or `@broker-sandbox` never run in the deterministic commit gate.

## 17. CI Matrix

### Every Commit: Windows and Linux

- Restore with locked dependencies.
- Build all `net10.0` production and test projects.
- Run format and analyzer checks.
- Run unit tests.
- Run SQLite data integration tests.
- Run component/integration tests.
- Run non-UI Reqnroll acceptance tests.
- Run architecture-boundary tests.
- Publish test results and coverage.

### Every Commit: Windows

- Build `Trading.UI.Wpf` and its test project.
- Run a small WPF FlaUI smoke suite in an interactive Windows test environment.

### Scheduled or Pre-Release

- Full WPF journey suite.
- Broker sandbox contract tests.
- Reliability and recovery suite.
- Migration tests against all supported upgrade fixtures.
- Performance trend suite.
- Real-model evaluation suite where credentials and budgets are available.

No automated suite submits live-money orders.

## 18. Coverage and Quality Gates

Coverage is diagnostic, not proof. Initial gates should require:

- All tests pass with no unexpected retries.
- Every aggregate invariant and state transition has explicit tests.
- Every risk rule has approve, reject, and boundary cases.
- Every financial calculation has precision and rounding cases.
- Every material external operation has idempotency and unknown-outcome coverage.
- New user-visible behavior includes or updates a Gherkin scenario.
- New WPF controls used by critical journeys include accessibility metadata and stable automation IDs.
- New migrations include fresh-database and upgrade tests.
- Flaky tests are treated as defects; they are not hidden behind unlimited retries.

A numerical line-coverage threshold may be introduced after baseline measurement, but it must not incentivize low-value assertions or replace behavior-based gates.

## 19. Test Data and Artifact Policy

- Use synthetic accounts, instruments, reports, users, and credentials.
- Do not copy production account data into fixtures.
- Keep scenario fixtures small, readable, versioned, and deterministic.
- Record random seeds when property-based or generated data is used.
- Store large recorded broker payloads in redacted fixture files with schema and source metadata.
- Retain failed-test logs, screenshots, temporary databases, and relevant run identifiers according to CI retention policy.
- Never publish secrets or unredacted broker payloads as CI artifacts.

## 20. Definition of Done

A feature is complete when:

1. Domain invariants and financial calculations have unit tests.
2. Persistence mappings and constraints have SQLite integration tests.
3. Cross-component workflows have integration tests.
4. User-visible behavior has an application-level Gherkin scenario.
5. Critical presentation wiring has a WPF journey test where justified.
6. Failure, authorization, idempotency, and recovery paths are tested in proportion to risk.
7. Tests pass on every applicable target platform.
8. Required diagnostics make failures actionable.
9. Documentation and fixtures reflect the implemented behavior.

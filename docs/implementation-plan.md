# Implementation Plan

## 1. Purpose

This document defines the staged delivery plan for the trading platform. Each stage produces an independently testable increment with explicit acceptance criteria. A stage is complete only when its outcomes are automated, demonstrable, and safe to build upon.

The first release target is a hardened paper-trading MVP. Live-broker integration is a separate, explicitly authorized stage.

## 2. Acceptance Rules for Every Stage

Every stage must satisfy these exit conditions:

- Production code builds with warnings treated as errors.
- Applicable automated tests pass on Windows and Linux.
- WPF-specific builds and tests pass on Windows.
- New database migrations work against a fresh database and the previous-stage upgrade fixture.
- New user-visible behavior has automated acceptance coverage.
- Failure paths leave durable state consistent.
- Commit-gating tests do not depend on a real LLM, public web service, live market data, or live-money broker account.
- No automated test submits a live-money order.
- Documentation reflects architectural, domain, data, and behavioral decisions made during implementation.
- The increment can be demonstrated independently through the headless host, application-facing test driver, or WPF interface as appropriate.
- No known critical or high-severity defect remains in the stage's delivered scope.
- The stage's Reqnroll BDD scenarios run and pass in every applicable target environment.

Starting the next stage requires all mandatory criteria from the preceding stage. A documented exception must identify the unmet criterion, risk, owner, and resolution stage; exceptions involving financial correctness, authorization, idempotency, or data integrity are not permitted.

### Stage Planning Rule

The first implementation task in every stage is to write or refine that stage's Gherkin features from its acceptance criteria. Those scenarios begin as executable specifications and are implemented as the stage progresses. A stage cannot complete while any required scenario is undefined, pending, skipped without an approved platform reason, or failing.

## 3. Stage 1: Solution Foundation and Domain Model

### Goal

Establish the executable solution, build conventions, dependency boundaries, and core domain without external integrations.

### Deliverables

- .NET solution and production/test projects.
- Shared build properties and central package management.
- Nullable reference types, analyzers, and warnings-as-errors.
- Initial domain aggregate, entity, and value-object classes.
- Strongly typed domain IDs.
- Exact financial primitives for money, prices, quantities, percentages, and currencies.
- Initial aggregate state machines.
- Architecture-boundary tests.
- Windows and Linux CI.
- Documented commands for restore, build, test, and format validation.

### Acceptance Criteria

- All Stage 1 Reqnroll BDD scenarios run and pass on Windows and Linux.
- The solution restores and builds from a clean checkout.
- All non-WPF production and test projects build on Windows and Linux.
- The WPF project builds on Windows.
- `Trading.Core` contains no EF Core, SQLite, WPF, broker SDK, or LLM-provider dependency.
- Invalid money, quantity, price, percentage, and currency values cannot be constructed.
- Strongly typed IDs prevent interchange between unrelated domain identities.
- Allowed and forbidden Bot Run, Trade Proposal, Capital Reservation, and Order transitions have unit tests.
- Each implemented aggregate invariant has positive and negative unit tests.
- Architecture tests reject prohibited project references and Windows-only APIs in cross-platform projects.
- A developer can run the complete applicable test suite with one documented command.

### Demonstration

Run the build and unit-test suite on a clean workspace and show representative domain state-machine and financial-value tests passing.

### Explicitly Not Included

- Database persistence.
- Executable Trading Bot runs.
- Research workflows.
- Broker operations.

## 4. Stage 2: Persistence and Portfolio State

### Goal

Persist the initial aggregates and create reproducible portfolio decision snapshots.

### Deliverables

- SQLite and EF Core infrastructure.
- Initial migrations and migration test fixtures.
- Aggregate repositories and unit of work.
- Broker Connection and Broker Account persistence.
- Instrument identity and broker mappings.
- Portfolios, Positions, and Portfolio Ledger Entries.
- Portfolio Decision Snapshots.
- Read-only portfolio projections.
- Optimistic concurrency handling.

### Acceptance Scenario

```gherkin
Scenario: Persist and reload a portfolio
  Given a portfolio with cash and positions
  When the application is restarted
  Then the portfolio should retain its exact financial state
```

### Acceptance Criteria

- All Stage 2 Reqnroll BDD scenarios run and pass on Windows and Linux.
- Exact financial decimals round-trip without floating-point storage.
- UTC timestamps retain defined precision and ordering.
- Strongly typed IDs round-trip through EF Core converters.
- One Portfolio cannot be assigned to multiple active Trading Bots.
- One Broker Account cannot own multiple active Portfolios in the MVP.
- Duplicate ledger sources do not create duplicate entries.
- Ledger corrections use compensating entries rather than overwriting history.
- Decision Snapshots are immutable and have stable canonical content hashes.
- Stale optimistic-concurrency writes are rejected with an application-level concurrency result.
- Repositories expose domain aggregates rather than EF entities, `DbSet<T>`, or `IQueryable`.
- Read-heavy projections use no-tracking query services.
- Migrations succeed against an empty database and a previous-stage upgrade fixture.
- Delete behavior cannot cascade into financial or audit history.
- Repository and transaction tests use the real SQLite provider rather than the EF in-memory provider.

### Demonstration

Create a Broker Account, Instrument, Portfolio, ledger entries, and Positions; persist them; restart the host; and display an identical immutable decision snapshot.

### Explicitly Not Included

- LLM agent execution.
- Shared research.
- Trade proposals or orders.

## 5. Stage 3: Multi-Bot Runtime and Scheduling

### Goal

Run multiple isolated Trading Bots through a bounded agent loop using a scripted LLM.

### Deliverables

- Trading Bot configuration and immutable configuration versions.
- Bot Run lifecycle and audit records.
- Manual and scheduled triggers.
- Durable per-bot run leases.
- Trigger coalescing.
- Bounded LLM tool loop.
- Scripted LLM provider for deterministic tests.
- `Finish` tool and requested-wake-time handling.
- Run and tool-invocation audit records.
- Cross-platform headless host.

### Acceptance Scenario

```gherkin
Scenario: Run two isolated Trading Bots
  Given two bots manage different portfolios
  When both bots are triggered
  Then each bot should receive only its own portfolio snapshot
  And both runs should complete independently
```

### Acceptance Criteria

- All Stage 3 Reqnroll BDD scenarios run and pass on Windows and Linux.
- Only one active run may exist for a particular Trading Bot.
- Different Trading Bots may run concurrently within global resource limits.
- Triggers arriving during an active run are durably retained and coalesced.
- Every run pins exactly one immutable Bot configuration version.
- Every run receives an immutable reconciled Portfolio Decision Snapshot.
- A tool call is rejected when absent from the pinned Tool Policy.
- Time, token, cost, tool-call, research-request, and proposal budgets are enforced.
- A malformed model response or missing `Finish` produces a safe terminal state.
- A requested wake time is accepted, bounded, or rejected by deterministic scheduling policy.
- The baseline schedule cannot be silently disabled by an LLM request.
- Restarting the host safely recovers expired leases.
- One bot cannot access another bot's run context, artifacts, configuration, or Portfolio.
- The headless host starts configured bots and shuts down gracefully.
- Every run can be reconstructed from its configuration, snapshot, tool calls, result, and schedule decision.

### Demonstration

Trigger two scripted bots simultaneously, show isolated inputs and audit histories, then demonstrate trigger coalescing and safe timeout behavior.

### Explicitly Not Included

- Shared Research Bot.
- Trade proposals that can enter execution.
- Broker order submission.

## 6. Stage 4: Shared Research Bot

### Goal

Allow Trading Bots to request and consume reusable, immutable research through a shared Research Bot service.

### Deliverables

- Research Requests and subscriptions.
- Research Bot run lifecycle.
- Shared Report catalog.
- Fixture-backed research sources.
- Report publication and source provenance.
- Report visibility, freshness, expiration, and versioning.
- Equivalent-request deduplication.
- Report-completion triggers and subscriber notification.
- Prompt-injection boundary tests.

### Acceptance Scenario

```gherkin
Scenario: Share one research report between Trading Bots
  Given two bots request equivalent public research
  When the Research Bot completes the request
  Then one report should be published
  And both bots should be notified
  And both bots should be able to read the same report version
```

### Acceptance Criteria

- All Stage 4 Reqnroll BDD scenarios run and pass on Windows and Linux.
- Equivalent concurrent requests produce one Research Bot run.
- A sufficiently fresh equivalent Report can satisfy a later request without another run.
- Private Reports remain inaccessible to unauthorized bots.
- Visibility cannot be broadened after private inputs are provided.
- Published Reports cannot be modified.
- Refreshing a Report creates a new immutable version.
- Reports include source provenance, data cutoff, generation time, schema version, generator metadata, and content hash.
- Partial or failed research is retained for audit but not published as completed.
- Retrieved content cannot alter prompts, tool permissions, visibility, budgets, or agent policy.
- Research Bot tools do not include proposal, approval, reservation, order, or broker operations.
- Report completion can trigger subscribed Trading Bots without creating duplicate runs.
- Every subscriber receives a durable completion or failure notification.

### Demonstration

Have two Trading Bots request the same fixture-backed company analysis, show one Research Bot run and one shared Report, then demonstrate private visibility and report refresh/versioning.

### Explicitly Not Included

- Unrestricted public-web research.
- Live LLM dependencies in acceptance tests.
- Deterministic hypothesis backtesting.
- Trade execution.

## 7. Stage 5: Trade Proposals, Approvals, and Risk

### Goal

Convert agent suggestions into structured Trade Proposals governed by deterministic hierarchical policy.

### Deliverables

- `ProposeTrade` tool.
- `ProposeTargetAllocation` tool.
- Trade Proposal lifecycle.
- Hierarchical guardrail pipeline.
- Immutable Guardrail Evaluations.
- Human Proposal Approvals.
- Capital Reservations.
- Proposal queue and read projections.
- Exact Report and Hypothesis evidence references.

### Acceptance Scenario

```gherkin
Scenario: Approve a valid proposal
  Given a bot using human approval mode proposes a valid trade
  When an authorized user approves it
  Then the proposal should be revalidated
  And the required capital should be reserved
  And no broker order should be submitted directly by the bot
```

### Acceptance Criteria

- All Stage 5 Reqnroll BDD scenarios run and pass on Windows and Linux.
- The LLM has no order-submission, approval, reservation, or guardrail-management tool.
- Proposal tools accept only structured, schema-validated arguments.
- A Proposal references its bot, run, configuration, Portfolio, decision snapshot, and exact evidence versions.
- A Trading Bot cannot propose an action for an unassigned Portfolio.
- Platform, account, Portfolio, and bot guardrails execute in the defined hierarchy.
- A lower policy level cannot weaken a parent policy.
- Every decision produces structured immutable rule results with policy versions and state references.
- Human Approval identifies the actor, Proposal version, reviewed state, decision, reason, and timestamp.
- Approval cannot authorize changed Proposal content.
- Expired Proposals cannot be approved.
- Approved Proposals are revalidated against fresh state before order creation.
- Two Proposals cannot reserve the same available capital.
- Reservations release after rejection, cancellation, or expiration.
- Research-only mode records Proposals without making them executable.
- No path in this stage reaches a broker submission API.

### Demonstration

Create valid and invalid Proposals from a scripted bot, display structured risk outcomes, approve one Proposal, and demonstrate that a concurrent Proposal cannot consume the reserved capital.

### Explicitly Not Included

- Broker order submission.
- Fill processing.
- Live trading.

## 8. Stage 6: Paper-Order Execution

### Goal

Complete the Proposal-to-Fill workflow through a simulated broker.

### Deliverables

- Simulated broker adapter.
- Order creation and submission workflow.
- Durable inbox and outbox processing.
- Broker acknowledgements and rejections.
- Partial and complete fills.
- Order reconciliation.
- Position and ledger updates.
- Reservation consumption and release.
- Complete headless paper-trading vertical slice.

### Acceptance Scenario

```gherkin
Scenario: Execute an approved paper trade
  Given an approved proposal with reserved capital
  When the order is submitted to the simulated broker
  And the broker reports a fill
  Then the order should be marked filled
  And the position should be updated
  And the portfolio ledger should contain the trade and fee entries
  And the reservation should be consumed
```

### Acceptance Criteria

- All Stage 6 Reqnroll BDD scenarios run and pass on Windows and Linux.
- An Order can originate only from an approved, unexpired, freshly validated Proposal.
- Order intent and its submission outbox message commit atomically.
- Outbox retries do not create duplicate broker Orders.
- Duplicate broker messages and Fills do not change state twice.
- Partial Fills update Order, Position, ledger, and Reservation consistently.
- An unknown submission outcome is reconciled before retry.
- Invalid or out-of-order broker events are rejected, deferred, or reconciled safely.
- Applying a Fill is atomic across Order, Position, ledger, applied-fill marker, and Reservation.
- Restarting with pending inbox or outbox work resumes safely.
- Paper and live broker environments cannot be confused.
- Stable client order IDs provide end-to-end idempotency.
- The complete workflow runs through the headless host.

### Demonstration

Run a scripted Trading Bot through research, Proposal, Approval, Reservation, paper Order, partial Fill, and final Fill; show the resulting Position, ledger, and complete audit chain.

### Stage Outcome

This is the first complete vertical trading slice.

## 9. Stage 7: WPF Operator Interface

### Goal

Provide a usable Windows interface over the completed paper-trading workflow.

### Deliverables

- Generic Host integration in WPF.
- Application shell and navigation.
- Trading Bot and Portfolio management views.
- Research catalog and Report viewer.
- Proposal review and Approval queue.
- Orders, Fills, and risk-event views.
- Broker connection and reconciliation status.
- Bot, Portfolio, account, and platform kill switches as applicable.
- Stable Automation IDs and accessibility metadata.
- Reqnroll/FlaUI critical journey suite.

### Acceptance Criteria

- All Stage 7 non-UI Reqnroll scenarios pass on Windows and Linux, and all Stage 7 WPF scenarios pass on Windows.
- A user can create, configure, pause, resume, and inspect a Trading Bot.
- A user can assign an eligible Portfolio.
- A user can trigger a Bot Run and observe its status and terminal outcome.
- A user can request and read a Research Report.
- A user can inspect Proposal rationale, evidence, guardrail results, and data freshness.
- An authorized user can approve or reject a Proposal.
- Paper Orders and Fills appear without restarting the application.
- Research-only, human-approval, paper, and live modes are visually distinct.
- Stale data, failed reconciliation, disconnected brokers, and failed runs are prominent.
- Kill switches are accessible, authorized, audited, and appropriately confirmed.
- Critical controls expose stable Automation IDs, accessible names, roles, and state.
- View models are tested without launching WPF.
- Critical FlaUI smoke journeys pass on Windows without coordinate-based selectors.
- Closing WPF stops the Generic Host cleanly without corrupting active state.

### Acceptance Scenario

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

### Demonstration

Complete the paper-trading journey through WPF using simulated services and show the resulting audit trail.

## 10. Stage 8: Recovery and Production Hardening

### Goal

Prove that the paper-trading platform remains safe and recoverable under expected failure conditions.

### Deliverables

- Deterministic failure-injection points.
- Recovery and reconciliation workflows.
- Backup and restore procedure.
- Health and readiness reporting.
- Structured operational logging and correlation.
- Retention and redaction controls.
- WPF and headless packaging.
- Full reliability and recovery suite.

### Acceptance Criteria

- All Stage 8 Reqnroll BDD and recovery scenarios run and pass on every applicable platform.
- The host recovers after termination at every material order-submission and fill-processing boundary.
- Broker acceptance followed by client timeout cannot create a duplicate Order.
- Expired Bot Run leases are recovered without duplicating completed work.
- Database contention produces bounded retry or clear failure without corruption.
- Stale market data prevents submission.
- Changed buying power after Approval causes safe revalidation failure.
- Graceful shutdown stops new work and checkpoints or safely terminates active work.
- Backup and restore reproduce Portfolio, Proposal, Order, Report, and audit state.
- Logs trace a decision from Bot Run through Report, Proposal, Approval, Order, and Fill.
- Credentials and sensitive information are absent from stored logs and generated test artifacts.
- Readiness remains false until migrations and required reconciliation complete.
- Windows and Linux headless packages pass clean-machine smoke tests.
- The complete deterministic acceptance and recovery suite passes repeatedly without flaky retries.

### Demonstration

Run selected failure-injection scenarios, restore from backup, and show that financial state and the audit chain remain complete and internally consistent.

### Stage Outcome

Completion produces the paper-trading MVP release candidate suitable for sustained sandbox use.

## 11. Stage 9: First Live-Broker Integration

### Goal

Integrate one real broker without changing the domain or engine authority boundaries.

This stage requires explicit authorization and is not an automatic continuation of the paper-trading MVP.

### Deliverables

- One Linux-compatible broker adapter.
- Broker capability mapping.
- Sandbox contract suite.
- Credential-provider integration.
- Rate-limit and error normalization.
- Live reconciliation workflows.
- Operational checklist and restricted initial limits.
- Explicit live-mode promotion workflow.

### Acceptance Criteria

- All Stage 9 deterministic Reqnroll scenarios pass on Windows and Linux, and the tagged broker-sandbox scenarios pass in the approved sandbox environment.
- The adapter passes the common Broker contract suite.
- The selected SDK and runtime operate on Linux.
- Account, Position, cash, Order, and execution normalization are verified against the broker sandbox.
- Rate limits and normalized error categories are tested.
- Timeout and unknown-outcome reconciliation are proven.
- Stable client order IDs prevent duplicate submission.
- Credential handling passes security review and no credential is persisted in plaintext.
- Extended sandbox soak testing completes without unresolved discrepancy.
- Live mode requires explicit, audited administrative promotion.
- Initial live limits are stricter than paper limits.
- A platform kill switch prevents all new submissions.
- The first live operation uses deliberately limited capital and an approved operational checklist.
- No automated test submits a live-money Order.

### Demonstration

Run the complete workflow in the broker sandbox, demonstrate disconnect and reconciliation, and show that live promotion remains disabled until explicitly authorized.

## 12. Milestones

```text
Stages 1-2
    Foundation

Stages 3-4
    Agent and research platform

Stages 5-6
    Complete paper-trading engine

Stage 7
    Usable desktop product

Stage 8
    Paper-trading MVP release

Stage 9
    Live-trading readiness
```

## 13. Stage Review Record

Each completed stage should produce a short review record containing:

- Stage number and completion date.
- Delivered scope.
- Acceptance-test and CI result links.
- Migration version.
- Demonstration notes.
- Known non-blocking limitations.
- Architecture Decision Records created or changed.
- Approval to begin the next stage.

The review record must not waive unresolved financial-integrity, authorization, idempotency, or audit defects.

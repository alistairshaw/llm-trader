# Domain Model

## 1. Purpose

This document defines the minimum viable domain model using Domain-Driven Design. It is authoritative for domain language, bounded contexts, entities, aggregate boundaries, value objects, lifecycles, and invariants. The physical EF Core and SQLite design is defined in [Data Model](data-model.md) and must preserve these boundaries.

## 2. Modeling Rules

- An entity has stable identity and a lifecycle; a value object is defined by its values.
- An aggregate is a transactional consistency boundary with one aggregate root.
- Only aggregate roots are loaded or saved directly through repositories.
- Cross-aggregate references use strongly typed IDs, not navigable object graphs.
- Historical facts such as fills, published reports, evaluations, and completed runs are immutable.
- Aggregate behavior protects invariants; public setters must not permit invalid state.
- Every aggregate, entity, and value object is represented by an explicit C# class or record class in `Trading.Core`.
- Domain classes contain no EF Core attributes, persistence DTOs, broker SDK types, WPF types, or LLM-provider types.

## 3. Bounded Contexts and Aggregates

```text
Bot Management
    TradingBot
        └── TradingBotConfigurationVersion
    BotRun
        ├── BotRunTrigger
        └── ToolInvocation

Portfolio
    Portfolio
    Position
    PortfolioDecisionSnapshot
    PortfolioLedgerEntry

Broker Integration
    BrokerConnection
    BrokerAccount
    Instrument
        └── InstrumentBrokerMapping

Research
    ResearchRequest
        └── ResearchSubscription
    ResearchReport
    Hypothesis
        └── HypothesisVersion

Trade Proposals
    TradeProposal
        ├── GuardrailEvaluation
        └── ProposalApproval
    CapitalReservation

Execution
    Order
        ├── OrderTransition
        └── Fill
```

Each non-indented type is an aggregate root. Indented types are entities owned by the root above them.

## 4. Bot Management

### 4.1 TradingBot Aggregate

A persistent investment agent with one assigned portfolio and a versioned mandate.

```text
TradingBot
    Id: TradingBotId
    Name
    Status
    PortfolioId: PortfolioId?
    ActiveConfigurationVersionId
    CreatedAt
    UpdatedAt

TradingBotConfigurationVersion
    Id: TradingBotConfigurationVersionId
    VersionNumber
    InvestmentMandate
    RiskPolicy
    ToolPolicy
    RunBudget
    SchedulingPolicy
    ExecutionMode
    ModelConfiguration
    PromptVersion
    CreatedAt
    ActivatedAt
    SupersededAt
```

Behavior includes creating and activating configuration versions, assigning a portfolio, pausing or enabling the bot, changing execution mode, and determining whether a trigger may run.

Invariants:

- At most one configuration version is active.
- Historical configuration versions are immutable.
- Every run pins exactly one configuration version.
- `LiveTrading` requires explicit promotion.
- Bot policy cannot weaken inherited platform, account, or portfolio limits.
- At most one active Trading Bot manages a portfolio in the MVP.

### 4.2 BotRun Aggregate

One bounded, auditable invocation of a Trading Bot.

```text
BotRun
    Id: BotRunId
    TradingBotId
    ConfigurationVersionId
    PortfolioSnapshotId
    Status
    StartedAt
    CompletedAt
    LeaseOwner
    LeaseExpiresAt
    FinishResult
    RequestedNextRunAt
    AcceptedNextRunAt
    Usage

BotRunTrigger
    Id: BotRunTriggerId
    TriggerType
    Reason
    OccurredAt
    SourceId

ToolInvocation
    Id: ToolInvocationId
    ToolName
    Arguments
    Status
    StartedAt
    CompletedAt
    ResultReference
    Error
    Usage
```

Behavior includes lease management, trigger coalescing, tool-call recording, proposal/reference recording, terminal transitions, and requested-versus-accepted scheduling.

Invariants:

- Only one active run exists per Trading Bot.
- Terminal runs cannot resume.
- A run uses one configuration and one decision snapshot.
- Tools must be permitted and deterministic budgets enforced.
- Incomplete reasoning creates no implicit proposal or order.
- Only the scheduler sets `AcceptedNextRunAt`.

`ToolInvocation` is logically append-only; persistence need not materialize the entire history on every load.

## 5. Portfolio

### 5.1 Portfolio Aggregate

A governed pool of capital.

```text
Portfolio
    Id: PortfolioId
    Name
    BaseCurrency
    BrokerAccountId: BrokerAccountId?
    AssignedTradingBotId: TradingBotId?
    Status
    CapitalAllocation
    CashReservePolicy
    CreatedAt
```

Behavior includes bot assignment, broker-account association, lifecycle changes, audited capital changes, and authorization of decision-snapshot creation.

Invariants:

- At most one active Trading Bot manages a portfolio.
- The MVP permits at most one portfolio per broker account or subaccount.
- Base currency cannot change after financial activity begins.
- Closed portfolios accept no new proposals.
- Capital cannot be silently transferred between portfolios.

Positions, orders, and ledger history are separate aggregates to avoid making `Portfolio` a large concurrency bottleneck.

### 5.2 Position Aggregate

Ownership state for one portfolio and instrument.

```text
Position
    Id: PositionId
    PortfolioId
    InstrumentId
    Quantity
    AverageCost
    RealizedProfitLoss
    Version
    OpenedAt
    UpdatedAt
    ClosedAt
```

Behavior includes applying fills, allocated fees, and corporate actions. There is at most one position per portfolio/instrument; fills are idempotent; calculations use deterministic decimal arithmetic; changes originate from executions or audited adjustments.

### 5.3 PortfolioDecisionSnapshot Aggregate

The immutable point-in-time input to a Trading Bot run.

```text
PortfolioDecisionSnapshot
    Id: PortfolioDecisionSnapshotId
    PortfolioId
    TradingBotId
    ConfigurationVersionId
    AsOf
    ReconciliationStatus
    Cash
    BuyingPower
    ReservedCapital
    PositionSnapshots
    OpenOrderSnapshots
    RiskUtilization
    RelevantCashFlows
    DataFreshness
    ContentHash
```

Published snapshots are immutable, identify their exact configuration and reconciliation state, carry explicit timestamps/freshness, and use a canonical content hash. They are never refreshed in place.

### 5.4 PortfolioLedgerEntry Aggregate

An immutable accounting fact.

```text
PortfolioLedgerEntry
    Id: PortfolioLedgerEntryId
    PortfolioId
    EntryType
    Amount
    Currency
    InstrumentId
    Quantity
    EffectiveAt
    SourceType
    SourceId
```

Initial types include deposits, withdrawals, settlement, fees, dividends, interest, taxes, corporate actions, and manual corrections. Entries are append-only and idempotent by source identity. Corrections use compensating entries.

## 6. Broker Integration

### 6.1 BrokerConnection Aggregate

```text
BrokerConnection
    Id: BrokerConnectionId
    BrokerType
    DisplayName
    Environment
    CredentialReference
    Status
    Capabilities
    CreatedAt
```

It owns adapter configuration and normalized connectivity state. Credentials are references, paper/live environments are explicit, and disabled connections reject broker operations.

### 6.2 BrokerAccount Aggregate

```text
BrokerAccount
    Id: BrokerAccountId
    BrokerConnectionId
    ExternalAccountId
    DisplayName
    AccountType
    BaseCurrency
    Status
    LastReconciledAt
```

It owns normalized broker-account identity, restrictions, and reconciliation state. External identity is unique within a connection. Unreconciled, restricted, or disabled accounts reject new orders. The MVP does not assign multiple active portfolios to one account.

### 6.3 Instrument Aggregate

Stable tradable-asset identity independent of ticker symbols.

```text
Instrument
    Id: InstrumentId
    InstrumentType
    PrimarySymbol
    DisplayName
    Currency
    Exchange
    Status

InstrumentBrokerMapping
    Id: InstrumentBrokerMappingId
    BrokerConnectionId
    ExternalInstrumentId
    Symbol
    Exchange
    EffectiveFrom
    EffectiveTo
```

Trading uses `InstrumentId`, never ticker alone. Mappings are effective-time aware; inactive or unresolved instruments cannot be traded; currency and precision are explicit; mappings cannot overlap ambiguously.

## 7. Research

### 7.1 ResearchRequest Aggregate

```text
ResearchRequest
    Id: ResearchRequestId
    Subject
    Question
    AsOf
    Status
    Visibility
    FreshnessRequirement
    NormalizedResearchKey
    RequestedAt
    StartedAt
    CompletedAt
    ResultReportId

ResearchSubscription
    Id: ResearchSubscriptionId
    TradingBotId
    SubscribedAt
    NotificationStatus
```

It owns authorization, deduplication, subscribers, asynchronous state, and its result link. Questions must be bounded. Visibility cannot broaden after private inputs arrive. Subscribers must be authorized. Only completed requests link to a published report.

### 7.2 ResearchReport Aggregate

One immutable published report version.

```text
ResearchReport
    Id: ResearchReportId
    ReportSeriesId
    VersionNumber
    ResearchRequestId
    Subject
    Question
    Visibility
    DataCutoff
    GeneratedAt
    ExpiresAt
    SupersedesReportId
    Content
    ContentHash
    Provenance
    GeneratorMetadata
```

It can be marked expired, superseded, corrected, or retracted without editing it. Revisions create new versions. Provenance remains independent of generated prose. Visibility cannot broaden accidentally, and reports referenced by retained proposals cannot be deleted.

### 7.3 Hypothesis Aggregate

A falsifiable investment claim with immutable versions.

```text
Hypothesis
    Id: HypothesisId
    Name
    Status
    CurrentVersionId
    CreatedAt
    Version

HypothesisVersion
    Id: HypothesisVersionId
    VersionNumber
    Claim
    UniverseDefinition
    InputDefinitions
    SignalRules
    EvaluationPlan
    SuccessCriteria
    InvalidationCriteria
    EvidenceReportIds
    CreatedAt
    FrozenAt
```

```text
Draft -> Frozen -> Testing -> Validated
                         └-> Rejected
Validated -> Retired
```

Frozen versions cannot change. Tests reference exact hypothesis, code, parameter, and dataset versions. Changes made after observing results create a new version. Proposals reference exact hypothesis versions.

## 8. Trade Proposals

### 8.1 TradeProposal Aggregate

An action suggested by a Trading Bot; it is not an order.

```text
TradeProposal
    Id: TradeProposalId
    TradingBotId
    BotRunId
    PortfolioId
    PortfolioSnapshotId
    ConfigurationVersionId
    InstrumentId
    ProposalType
    RequestedAction
    ContentVersion
    Rationale
    HypothesisEvidence (exact version and content hash)
    ReportEvidence (exact report identity, series version, and content hash)
    Status
    CreatedAt
    ValidUntil

GuardrailEvaluation
    Id: GuardrailEvaluationId
    EvaluationStage
    PolicyVersion
    Outcome
    RuleResults
    EvaluatedAt
    StateSnapshotId
    PolicyReference (level, identity, and version)
    FreshStateReference (snapshot, observed time, and content hash)

ProposalApproval
    Id: ProposalApprovalId
    Decision
    ActorType
    ActorId
    Reason
    DecidedAt
    ProposalVersion
    StateSnapshotId
    ReviewedContentVersion
    ReviewedStateReference
```

Proposal types are versioned `DirectTrade` and `TargetAllocation` actions with exact decimal financial primitives and bounded canonical values. Lifecycle states include `Recorded`, `Validating`, `Rejected`, `AwaitingHumanApproval`, `Approved`, `Expired`, `Cancelled`, and `ConvertedToOrder`. Governance results use stable `proposal_governance.*` codes across domain and application boundaries.

Invariants:

- A proposal belongs to one bot, run, portfolio, configuration, and snapshot.
- The portfolio is assigned to the proposing bot.
- Recorded proposals are immutable; changes create new proposals.
- Expired proposals cannot be approved.
- Required human approval cannot be bypassed.
- An approval applies only to the exact proposal version and state snapshot reviewed.
- Approval and rejection history is immutable and identifies the responsible actor.
- Fresh-state revalidation occurs immediately before order creation.
- Conversion to orders is idempotent.
- Evidence references exact report and hypothesis versions.

This aggregate is the authority boundary between probabilistic LLM reasoning and deterministic execution governance.

### 8.2 CapitalReservation Aggregate

A `CapitalReservation` temporarily claims portfolio buying capacity for an approved proposal so concurrent proposals cannot spend the same capital.

```text
CapitalReservation
    Id: CapitalReservationId
    PortfolioId
    TradeProposalId
    OrderId
    Amount
    Currency
    Status
    CreatedAt
    ExpiresAt
    ConsumedAt
    ReleasedAt
    Version
```

Lifecycle states:

```text
Active -> Consumed
       ├-> Released
       └-> Expired
```

Behavior includes creating a reservation after validation, attaching it to an order, consuming it as funds become committed, and releasing or expiring unused capacity.

Invariants:

- A proposal has at most one active capital reservation.
- A reservation belongs to the same portfolio as its proposal.
- Amount is positive, has an explicit currency, and cannot exceed policy-authorized capacity.
- Available capital calculations include all active reservations.
- A terminal reservation cannot become active again.
- Consumption and release are idempotent.
- Creating a reservation and approving the proposal occur atomically.

## 9. Execution

### 9.1 Order Aggregate

```text
Order
    Id: OrderId
    ClientOrderId
    PortfolioId
    BrokerAccountId
    TradeProposalId
    InstrumentId
    Side
    Quantity
    OrderType
    LimitPrice
    TimeInForce
    Status
    BrokerOrderId
    CreatedAt
    SubmittedAt
    CompletedAt
    Version

OrderTransition
    Id: OrderTransitionId
    PreviousStatus
    NewStatus
    Reason
    Source
    OccurredAt

Fill
    Id: FillId
    BrokerExecutionId
    Quantity
    Price
    Fee
    ExecutedAt
    ReceivedAt
```

```text
Created -> Submitting -> Submitted -> Acknowledged -> PartiallyFilled -> Filled
                │             │              └-> CancelPending -> Cancelled
                │             ├-> Rejected
                │             └-> Expired
                └-> Unknown -> reconciled state
```

`ClientOrderId` is globally unique. Proposal conversion and fills are idempotent. Filled quantity cannot exceed ordered quantity. State changes follow the state machine. Unknown submissions are reconciled before retry. Terminal orders cannot return to active states.

`Fill` is an `Order` child for the MVP and may become an aggregate root if it later develops an independent lifecycle.

## 10. Risk Model

Risk is divided into versioned policies and immutable evaluations:

```text
PlatformRiskPolicy
    -> AccountRiskPolicy
        -> PortfolioRiskPolicy
            -> TradingBotRiskPolicy
                -> GuardrailEvaluation
```

Lower layers may tighten but not weaken parent constraints. Kill switches exist at each level. Evaluations record policy versions, input snapshots, measured values, limits, outcomes, reasons, and timestamps. Passing evaluation establishes policy compliance, not investment quality.

The effective policy is composed in the fixed `Platform -> Account -> Portfolio -> TradingBot` order. Maximum values use the lowest inherited value, minimum reserves and liquidity use the highest inherited value, eligible-instrument sets intersect, and kill switches and open-market requirements combine restrictively. Evaluation uses the stable `guardrail.*` rule and reason namespaces for authority, kill switch, mandate, instrument eligibility, expiry, position notional, concentration, available capital, price freshness, liquidity, and market hours. Missing or uncertain price, liquidity, market, identity, or mandate state fails restrictively. A rejection makes the proposal non-executable immediately, while the pure evaluator still returns the complete ordered result set required for audit.

Each application-level evaluation is an immutable, monotonically sequenced artifact bound to the proposal content hash and version, configuration version, fresh Portfolio snapshot and hash, and every effective policy version. Its canonical input hash is the idempotency boundary: an exact retry reuses the artifact, while changed fresh state or policy versions append a new evaluation. A passing evaluation advances to the approval boundary; any failed rule rejects the proposal. Optimistic concurrency commits the lifecycle transition and artifact together.

## 11. Core Value Objects

Explicit immutable value-object classes or record classes include:

- Strongly typed IDs for every aggregate and entity.
- `Money`, `Currency`, `Price`, `Quantity`, and `Percentage`.
- `DateRange` and `DataFreshness`.
- `InvestmentMandate` and `UniverseDefinition`.
- `RiskLimit`, `RiskPolicy`, and `CashReservePolicy`.
- `RunBudget`, `Usage`, and `ToolPolicy`.
- `SchedulingPolicy` and `FinishResult`.
- `ModelConfiguration` and `GeneratorMetadata`.
- `ExecutionInstructions` and `AllocationTarget`.
- `SourceCitation` and report provenance.
- `ReconciliationResult`.

Value objects validate construction, use value equality, are immutable, carry explicit units/currency/time/precision, and prevent financially dangerous primitive interchange.

## 12. Repository Boundaries

Repository interfaces exist only for aggregate roots and are implemented in `Trading.Data`:

```text
ITradingBotRepository
IBotRunRepository
IPortfolioRepository
IPositionRepository
IPortfolioDecisionSnapshotRepository
IPortfolioLedgerRepository
IBrokerConnectionRepository
IBrokerAccountRepository
IInstrumentRepository
IResearchRequestRepository
IResearchReportRepository
IHypothesisRepository
ITradeProposalRepository
ICapitalReservationRepository
IOrderRepository
```

Rules:

- Repositories accept and return domain aggregate roots, not EF entities.
- Child entities such as `Fill`, `ToolInvocation`, and `GuardrailEvaluation` have no repositories.
- Repositories do not expose `DbSet<T>` or `IQueryable`.
- Commands load and save aggregates through repositories.
- Read-heavy UI, catalog, report, and snapshot projections use dedicated query services.
- Application services coordinate cross-aggregate workflows and an explicit unit of work where atomicity is required.
- Mutable aggregates use optimistic concurrency.
- Repository methods express domain intent rather than storage mechanics.

Repository contracts, mappings, tables, keys, indexes, concurrency tokens, and transaction boundaries are specified in [Data Model](data-model.md).

## 13. MVP Aggregate Roots

| Context | Aggregate root | Independent lifecycle |
| --- | --- | --- |
| Bot Management | `TradingBot` | Mandate and configuration history |
| Bot Management | `BotRun` | Isolated LLM invocation and audit trail |
| Portfolio | `Portfolio` | Capital assignment and bot ownership |
| Portfolio | `Position` | Per-instrument fill processing |
| Portfolio | `PortfolioDecisionSnapshot` | Reproducible decision input |
| Portfolio | `PortfolioLedgerEntry` | Immutable accounting fact |
| Broker Integration | `BrokerConnection` | Adapter configuration and status |
| Broker Integration | `BrokerAccount` | Account identity and reconciliation |
| Broker Integration | `Instrument` | Stable tradable-instrument identity |
| Research | `ResearchRequest` | Research lifecycle and subscribers |
| Research | `ResearchReport` | Immutable published report version |
| Research | `Hypothesis` | Versioned testable claim |
| Trade Proposals | `TradeProposal` | Proposal authorization lifecycle |
| Trade Proposals | `CapitalReservation` | Exclusive claim on portfolio buying capacity |
| Execution | `Order` | Broker order state and fills |

## 14. Suggested Implementation Order

1. `Instrument`.
2. `BrokerConnection` and `BrokerAccount`.
3. `Portfolio`, `Position`, and `PortfolioLedgerEntry`.
4. `TradingBot` and configuration versions.
5. `PortfolioDecisionSnapshot`.
6. `BotRun`.
7. `ResearchRequest` and `ResearchReport`.
8. `TradeProposal`.
9. `CapitalReservation` and `Order`.
10. `Hypothesis` and deterministic evaluation integration.

`Hypothesis` is last only because the first vertical slice can create evidence-backed paper proposals from reports before backtesting exists. Its identity and version references should be reserved from the beginning.

## 15. Deferred Concepts

- Multiple Trading Bots independently managing one portfolio.
- Virtual portfolios sharing a netted broker account.
- A complete double-entry general ledger.
- Tax-lot selection and jurisdiction-specific tax accounting.
- Multi-node aggregate coordination.
- Complex derivatives and multi-leg orders.
- Arbitrary generated strategy code.

These capabilities require explicit revisions to aggregate boundaries and Architecture Decision Records.

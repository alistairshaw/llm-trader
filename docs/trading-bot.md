# Trading Bot

## 1. Purpose

A Trading Bot is a persistent, independently configured investment agent. It evaluates one assigned portfolio according to a versioned mandate, consults shared research and market data through controlled tools, and may create structured trade proposals. It never places orders directly.

The platform may host many Trading Bot instances. Each instance has independent goals, risk tolerance, configuration, runtime history, schedule, budgets, proposal history, working artifacts, and portfolio access.

## 2. Authority Boundary

The Trading Bot may:

- Inspect its deterministic portfolio snapshot and mandate.
- Retrieve explicitly permitted market, fundamental, and corporate-event data.
- Request, list, and read authorized research reports.
- Create sandboxed notes and artifacts within its own namespace.
- Propose trades or target portfolio allocations using strict schemas.
- Explain its reasoning and request a future evaluation time.

The Trading Bot may not:

- Submit, replace, or cancel broker orders directly.
- Access another bot's private state, portfolio, proposals, files, or reports.
- Change its mandate, capital allocation, risk limits, tool permissions, model settings, or budgets.
- Weaken platform, account, portfolio, or bot guardrails.
- Modify a published research report.
- Execute arbitrary code or write outside its artifact sandbox.
- Grant itself credentials, authority, or a different execution mode.

## 3. Identity and Configuration

Each bot has a stable `TradingBotId` and immutable configuration versions. A configuration version should include:

- Name, description, owner, and enabled/paused state.
- Investment objective and benchmark.
- Assigned `PortfolioId` and permitted broker account.
- Eligible instruments, markets, asset classes, and currencies.
- Investment horizon and review cadence.
- Risk tolerance and bot-level constraints.
- Required cash reserve and capital allocation.
- Research standards and permitted source/report scopes.
- LLM provider/model reference and versioned system prompt.
- Allowed tools and per-tool limits.
- Run token, cost, tool-call, proposal, and wall-clock budgets.
- Baseline schedule and allowed scheduling window.
- Execution mode: `ResearchOnly`, `HumanApproval`, `PaperTrading`, or `LiveTrading`.

Every run pins one configuration version. Editing a bot creates a new version and never changes the meaning of a historical run.

## 4. Portfolio Ownership

Initially, exactly one active Trading Bot may manage a portfolio. A portfolio should map to a dedicated broker account or broker subaccount for live trading.

The bot sees its portfolio, not an undifferentiated broker connection. The portfolio contains assigned capital, positions, cash, orders, accounting history, and performance. Broker state is reconciled before a decision snapshot is created.

Sharing one broker account across virtual portfolios is not supported initially. It requires a separate allocation ledger and account coordinator to reserve cash, allocate fills and fees, resolve conflicting trades, and enforce aggregate margin and exposure.

## 5. Triggers and Scheduling

A bot run may be triggered by:

- An authorized manual request.
- Its baseline schedule.
- An accepted next-run request from a previous run.
- Completion or failure of a subscribed research request.
- A deposit, withdrawal, dividend, or other portfolio event.
- Portfolio drift or a risk/reconciliation event.
- A future approved event type.

Only one run per bot may be active. The scheduler acquires a durable lease before starting a run. Additional triggers are durably coalesced into pending trigger reasons and cause at most one follow-up run unless policy requires otherwise.

The bot's requested next-run time is advisory. The deterministic scheduler enforces minimum/maximum delay, permitted hours, rate and cost limits, disabled/paused state, baseline schedules, and administrative overrides. A malformed or extreme request cannot disable the bot or create an uncontrolled loop.

## 6. Deterministic Input Snapshot

Before invoking the LLM, the engine reconciles relevant broker state and creates an immutable `PortfolioDecisionSnapshot`. It should contain:

- Snapshot ID, bot ID, portfolio ID, configuration version, and UTC timestamp.
- Trigger types and reasons.
- Data freshness and reconciliation status.
- Base currency, cash, buying power, and reserved capital.
- Positions, quantities, cost basis, current valuation, and allocation.
- Open, pending, and recently completed orders.
- Pending proposals and approval status.
- Realized and unrealized P&L.
- Concentration, exposure, and current risk-limit utilization.
- Recent cash flows, dividends, splits, and fees.
- Existing hypotheses, review dates, and relevant report catalog entries.
- Previous run outcome, rationale, and accepted next-run time.

The LLM receives a deterministic textual rendering plus stable identifiers. Every proposal references the snapshot used to create it.

## 7. Run Workflow

```text
Trigger received
    -> acquire per-bot lease
    -> pin configuration version
    -> reconcile broker and portfolio
    -> build immutable decision snapshot
    -> open bounded LLM session
    -> execute authorized tool loop
    -> accept structured proposals
    -> receive Finish result or terminate on policy limit
    -> validate requested schedule
    -> release bot run for deterministic proposal processing
    -> persist complete run record
    -> release lease and coalesce pending triggers
```

The LLM can complete successfully with no proposal. If it does not call `Finish`, the engine records `TimedOut`, `BudgetExceeded`, `Cancelled`, or `Faulted`; it does not infer an action from incomplete reasoning.

## 8. Tool Contract

### 8.1 `ProposeTrade` version 1

Creates a proposal only. Required fields should include:

```json
{
  "instrumentId": "US:AAPL",
  "side": "Buy",
  "quantity": 10,
  "orderType": "Limit",
  "limitPrice": 210.00,
  "timeInForce": "Day",
  "portfolioSnapshotId": "snapshot-456",
  "hypothesisId": "hypothesis-123",
  "evidenceReportIds": ["report-789-v2"],
  "rationale": "Position remains within the approved allocation.",
  "validUntil": "2026-08-19T16:00:00Z"
}
```

The closed schema uses canonical JSON and exact decimal strings. It pins `proposalId`, `portfolioId`,
`portfolioSnapshotId`, exact Report identities/series/versions, and an optional frozen Hypothesis version.
The dispatcher authorizes those identities against the active Bot Run's pinned Bot, configuration,
Portfolio assignment, decision snapshot, tool policy, proposal/call budgets, and Report visibility. A
`ProposalNotional` risk limit in the snapshot currency bounds a priced direct proposal; the platform
ceiling applies when that pinned policy does not define a narrower value. The durable result is always
a `Recorded` proposal and never implies approval, capital reservation, order conversion, or execution.

### 8.2 `ProposeTargetAllocation` version 1

Expresses one instrument's desired percentage as a canonical exact-decimal string; cash is the
remainder. Its identity, evidence, expiration, authorization, audit, and idempotency rules are the same
as `ProposeTrade`. A deterministic portfolio constructor calculates any required trades downstream and
submits them to the same guardrail pipeline. This is preferred for long-horizon investment bots.

### 8.3 Market and reference data

Prefer explicit tools such as `GetQuote`, `GetHistoricalBars`, `GetFundamentals`, `GetCorporateEvents`, and `GetMarketCalendar`. Responses include provider, source timestamp, retrieval timestamp, currency/units, adjustment policy, real-time/delayed status, and freshness warnings.

### 8.4 Research tools

- `RequestResearch` version `1` submits a bounded research question through the shared request service and returns its asynchronous queued, subscribed, or reused status. It never runs Research synchronously.
- `ListReports` returns authorized report metadata, status, version, freshness, and expiration.
- `GetReport` retrieves one immutable exact report ID, series, and version with canonical content and provenance.

These three version `1` contracts are the complete Trading Bot Research tool surface. Their calls are authorized against the Bot Run's pinned identity, configuration, tool policy, and budgets. Request status is returned by `RequestResearch`; durable completion or failure notifications wake subscribers through Bot Run triggers.

Report generation is asynchronous. A bot may finish and request a wake-up when a report completes.

### 8.5 Sandboxed artifacts

Use scoped tools such as `WriteResearchNote`, `ReadResearchNote`, and `ListResearchArtifacts`. General filesystem access is not exposed. Artifacts cannot override prompts, configuration, guardrails, executable code, or published reports.

### 8.6 `Finish`

```json
{
  "status": "Completed",
  "summary": "No trade proposed; awaiting an updated earnings report.",
  "nextRunAt": "2026-08-21T13:00:00Z",
  "wakeReason": "Review the report after the earnings release."
}
```

`nextRunAt` is optional and advisory. `Finish` closes the reasoning loop; it does not approve proposals.

## 9. Run Budgets and Failure Policy

Each run has deterministic limits for:

- Wall-clock duration.
- Model tokens and monetary cost.
- Total and per-tool call counts.
- Market-data and web requests.
- Concurrent and total research requests.
- Number and notional size of proposals.
- Consecutive tool failures.

Tool inputs are schema validated. Timeouts, provider failures, malformed output, and cancellation are recorded. The safest terminal behavior is no new action. Retries are bounded and only used where they cannot duplicate material side effects.

## 10. Proposal Validation and Execution

Proposal processing occurs outside the LLM session:

```text
Recorded proposal
    -> schema, identity, mandate, and permission validation
    -> retrieve fresh account, portfolio, and market state
    -> platform guardrails
    -> account guardrails
    -> portfolio guardrails
    -> bot mandate guardrails
    -> price, liquidity, market-hours, and proposal-expiry checks
    -> duplicate/idempotency check
    -> human approval when required
    -> reserve capital
    -> create durable order intent
    -> submit and reconcile with broker
```

Validation produces structured approvals or rejections. A proposal that was valid during reasoning may be rejected if state or prices changed. Passing deterministic rules establishes policy compliance, not investment quality.

Policy composition is monotonic: child maximums can only decrease, child minimums can only increase, eligible universes can only intersect, and inherited kill switches or market-open requirements cannot be cleared. Each of the four policy levels emits the same stable ordered rule set with its exact policy version, observed value, threshold, outcome, and reason code. Unknown price, liquidity, market-hours, identity, or mandate state is a rejection rather than an implicit pass. The evaluator is provider-neutral, deterministic, and side-effect free; persistence of its immutable decision is a separate application operation.

Each proposal pins an immutable content version, its Bot Run/configuration/Portfolio/decision snapshot, and exact Report and Hypothesis evidence versions. Guardrail inputs name the platform, account, Portfolio, and Trading Bot policy versions in that fixed order. Human decisions bind the actor, exact proposal content version, and reviewed state reference; reservation requests bind a subsequent fresh-state reference. These provider-neutral contracts expose no order-submission or broker authority.

## 11. Execution Modes

- `ResearchOnly`: store proposals; never submit orders.
- `HumanApproval`: validated proposals await explicit approval and are revalidated afterward.
- `PaperTrading`: approved proposals go to a simulated broker.
- `LiveTrading`: approved proposals may reach a configured live broker.

New bots default to `ResearchOnly` or `PaperTrading`. Promotion is an explicit, audited administrative action.

## 12. Isolation and Concurrency

- One active run per bot, protected by a durable lease.
- Many different bots may run concurrently within global resource limits.
- Bot working artifacts and private reports are namespaced by bot ID.
- Shared reports are immutable and read-only.
- A bot cannot reserve or spend another portfolio's capital.
- Broker operations use stable idempotency keys based on proposal/order identity.
- One bot's model, tool, or report failure does not pause unrelated bots.

## 13. Audit Record

Every `BotRun` retains:

- Run ID, trigger reasons, lease history, and timestamps.
- Bot configuration, prompt, tool-set, and model versions.
- Input snapshot ID and deterministic rendering version.
- Model messages required for audit, subject to retention and privacy policy.
- Tool calls, validated arguments, results, errors, and latency.
- Reports and data versions consulted.
- Proposals created and evidence links.
- Finish result and requested/accepted schedules.
- Guardrail decisions, approvals, orders, and broker responses.
- Token, cost, time, and tool usage.

This must make each decision explainable without relying on the model's memory.

## 14. Initial Acceptance Criteria

The first Trading Bot implementation is complete when it can:

1. Run multiple isolated bot definitions under one host.
2. Prevent simultaneous runs of the same bot while allowing different bots to run concurrently.
3. Build a reproducible portfolio snapshot from a simulated account.
4. Execute a bounded tool loop and safely handle a missing `Finish` call.
5. Request and consume immutable shared research reports.
6. Record structured proposals without exposing an order-submission tool.
7. Apply hierarchical guardrails and route approved proposals to paper trading.
8. Preserve an end-to-end audit trail and recover safely after restart.

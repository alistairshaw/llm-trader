# Data Model

## 1. Purpose

This document maps the domain model in [Domain Model](domain.md) to SQLite and Entity Framework Core for the minimum viable product. It defines persistence conventions, tables, keys, relationships, indexes, repository contracts, transaction boundaries, concurrency, retention, and migration order.

The domain model remains authoritative for behavior and invariants. This schema supports the domain; it does not replace it. EF Core entities and mappings must not leak into `Trading.Core`, `Trading.Engine`, or the UI.

## 2. Design Principles

- Normalize mutable operational and financial state.
- Store evolving immutable agent artifacts as versioned canonical JSON where they are normally read as a whole.
- Preserve every decision input, policy version, report version, proposal, approval, order transition, and fill required for audit.
- Enforce important uniqueness and ownership rules in both domain code and database constraints.
- Use application-generated identities and idempotency keys before external calls.
- Use optimistic concurrency for mutable aggregates.
- Use repositories only for aggregate roots and dedicated query services for read projections.
- Use an inbox/outbox pattern for reliable interaction with brokers, Research Bots, and background workers.
- Default financial and audit relationships to `ON DELETE RESTRICT`.

## 3. SQLite Storage Conventions

| Concept | SQLite representation |
| --- | --- |
| IDs | `TEXT` containing application-generated ULIDs |
| UTC timestamps | `INTEGER` containing Unix milliseconds |
| Exact decimal values | Canonical invariant-culture decimal `TEXT` |
| Enumerations | Constrained `TEXT` |
| Boolean | `INTEGER` constrained to `0` or `1` |
| Immutable structured content | Canonical JSON `TEXT` with a schema version |
| Content hashes | Lowercase SHA-256 hexadecimal `TEXT` |
| Optimistic concurrency | Application-maintained `INTEGER version` |

An Order is initially persisted exactly at version `0`. Each accepted Order aggregate transition increments the
version by one. Repository updates compare the caller's expected version and write the strictly greater aggregate
version, returning the stable concurrency outcome when another writer has already advanced the row. SQLite rejects
negative Order versions; persistence does not manufacture a transition merely to make a new Order storable.
| External identifiers | `TEXT` with scope-appropriate unique indexes |

### 3.1 Identifiers

Every aggregate and entity uses a strongly typed C# ID backed by a ULID. ULIDs are generated in the application before persistence, serialize as 26-character text, and provide useful chronological locality without exposing database-generated keys.

The database must not treat IDs for different domain types as interchangeable merely because they share a storage representation.

### 3.2 Timestamps

Domain timestamps use UTC `DateTimeOffset`. EF Core converters store Unix milliseconds as `INTEGER`. Source timestamps and retrieval timestamps remain distinct. Business-effective timestamps and database-recorded timestamps must not be conflated.

### 3.3 Financial Decimals

SQLite `REAL` must not be used for prices, quantities, balances, fees, allocation percentages, or profit/loss. The MVP stores exact decimals as canonical strings and converts them centrally to `decimal` in .NET.

Examples:

```text
210.125
100000.00
0.075
```

Canonicalization rules must define sign, decimal separator, trailing-zero policy, maximum precision, and maximum scale. Financial aggregation and comparison occur in domain code or purpose-built read models, not through floating-point SQL casts. A later migration may use scaled integers for fields whose bounds and precision are proven.

### 3.4 JSON

Every JSON column contains a top-level `schemaVersion` or is paired with a schema-version column. JSON is canonicalized before hashing. JSON is appropriate for immutable configurations, snapshots, tool payloads, report bodies, policy results, and hypothesis specifications; it is not used to hide relational ownership or lifecycle state.

## 4. Schema Overview

```text
Bot Management
    trading_bots
    trading_bot_configuration_versions
    bot_run_triggers
    bot_runs
    bot_tool_invocations

Portfolio
    portfolios
    positions
    position_applied_fills
    portfolio_ledger_entries
    portfolio_decision_snapshots

Broker Integration
    broker_connections
    broker_accounts
    broker_reconciliations
    instruments
    instrument_broker_mappings

Research
    research_requests
    research_subscriptions
    research_runs
    research_tool_invocations
    research_reports
    research_report_sources
    hypotheses
    hypothesis_versions
    hypothesis_evidence_reports
    hypothesis_test_results

Trade Proposals
    trade_proposals
    trade_proposal_evidence_reports
    guardrail_evaluations
    proposal_approvals
    capital_reservations

Execution
    orders
    order_transitions
    fills

Infrastructure
    outbox_messages
    inbox_messages
    schema_metadata
```

## 5. Bot Management Tables

### 5.1 `trading_bots`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK; `TradingBotId` |
| `name` | Required; initially unique |
| `status` | Required: `Enabled`, `Paused`, or `Retired` |
| `active_configuration_version_id` | Nullable FK until configured |
| `requested_next_run_at` | Nullable; latest LLM request |
| `accepted_next_run_at` | Nullable; scheduler-authoritative time |
| `last_completed_run_id` | Nullable reference to `bot_runs` |
| `created_at` | Required UTC |
| `updated_at` | Required UTC |
| `version` | Required concurrency token |

Portfolio assignment is stored canonically on `portfolios`; it must not also be persisted as an independently mutable `portfolio_id` here. Repositories expose the relationship without duplicating its source of truth.

Indexes:

- Unique `name` for the MVP.
- `(status, accepted_next_run_at)` for scheduler scans.

### 5.2 `trading_bot_configuration_versions`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `trading_bot_id` | Required FK |
| `version_number` | Required, increasing per bot |
| `investment_mandate_json` | Required, versioned JSON |
| `risk_policy_json` | Required, versioned JSON |
| `tool_policy_json` | Required, versioned JSON |
| `run_budget_json` | Required, versioned JSON |
| `scheduling_policy_json` | Required, versioned JSON |
| `execution_mode` | Required constrained enum |
| `model_configuration_json` | Required; contains no credentials |
| `prompt_version` | Required stable identifier |
| `content_hash` | Required SHA-256 |
| `created_at` | Required UTC |
| `activated_at` | Nullable UTC |
| `superseded_at` | Nullable UTC |

Constraints and indexes:

- Unique `(trading_bot_id, version_number)`.
- Optional unique `(trading_bot_id, content_hash)` to prevent duplicate versions.
- Published configuration content is immutable.

The active-configuration reference forms an insertion cycle. Create the bot and initial version in one transaction, then set `active_configuration_version_id` after both identities exist.

### 5.3 `bot_run_triggers`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `trading_bot_id` | Required FK |
| `trigger_type` | Manual, baseline schedule, requested schedule, report, portfolio, or operational event |
| `reason` | Required readable context |
| `source_type` | Nullable external source category |
| `source_id` | Nullable idempotency source |
| `occurred_at` | Required UTC |
| `consumed_by_run_id` | Nullable FK |
| `created_at` | Required UTC |

Use a unique filtered index on `(trading_bot_id, source_type, source_id)` when `source_id` is not null. Unconsumed triggers are coalesced into the next run without losing their individual reasons.

### 5.4 `bot_runs`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `trading_bot_id` | Required FK |
| `configuration_version_id` | Required FK |
| `portfolio_snapshot_id` | Nullable until snapshot preparation completes |
| `status` | Required run state |
| `lease_owner` | Nullable host-instance ID |
| `lease_expires_at` | Nullable UTC |
| `started_at` | Required UTC |
| `completed_at` | Nullable UTC |
| `finish_status` | Nullable |
| `finish_summary` | Nullable |
| `requested_next_run_at` | Nullable UTC |
| `requested_wake_reason` | Nullable |
| `accepted_next_run_at` | Nullable UTC |
| `terminal_reason` | Nullable normalized reason |
| `usage_json` | Required versioned resource totals |
| `version` | Required concurrency token |

A unique partial index enforces one active run per bot where status is `AcquiringLease`, `PreparingSnapshot`, `Reasoning`, or `WaitingForTool`. Lease acquisition uses one conditional database operation in a transaction rather than load/check/save.

Indexes:

- `(trading_bot_id, started_at DESC)`.
- `(status, lease_expires_at)`.

### 5.5 `bot_tool_invocations`

Append-only `BotRun` children:

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `bot_run_id` | Required FK |
| `sequence_number` | Required deterministic order |
| `tool_name` | Required application-owned name |
| `tool_schema_version` | Required |
| `arguments_json` | Required validated arguments |
| `status` | Started, completed, failed, or cancelled |
| `started_at` | Required UTC |
| `completed_at` | Nullable UTC |
| `result_json` | Nullable small result |
| `result_artifact_id` | Nullable large-result reference |
| `error_code` | Nullable normalized error |
| `error_detail` | Nullable redacted detail |
| `usage_json` | Nullable |

Unique `(bot_run_id, sequence_number)`.

## 6. Portfolio Tables

### 6.1 `portfolios`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `name` | Required |
| `base_currency` | Required ISO currency code |
| `broker_account_id` | Nullable FK |
| `assigned_trading_bot_id` | Nullable FK |
| `status` | Active, paused, or closed |
| `capital_allocation_amount` | Required exact decimal |
| `cash_reserve_policy_json` | Required versioned value object |
| `created_at` | Required UTC |
| `updated_at` | Required UTC |
| `version` | Required concurrency token |

Use unique partial indexes for non-null `broker_account_id` and `assigned_trading_bot_id` while the MVP enforces one-to-one ownership.

### 6.2 `positions`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `portfolio_id` | Required FK |
| `instrument_id` | Required FK |
| `quantity` | Required exact decimal |
| `average_cost_amount` | Required exact decimal |
| `average_cost_currency` | Required currency |
| `realized_pnl_amount` | Required exact decimal |
| `realized_pnl_currency` | Required currency |
| `opened_at` | Required UTC |
| `updated_at` | Required UTC |
| `closed_at` | Nullable UTC |
| `version` | Required concurrency token |

Unique `(portfolio_id, instrument_id)`. A zero-quantity position is retained rather than deleted.

### 6.3 `position_applied_fills`

An implementation-level idempotency table, not an aggregate:

| Column | Constraints and purpose |
| --- | --- |
| `position_id` | Composite PK and FK |
| `fill_id` | Composite PK and FK |
| `applied_at` | Required UTC |

### 6.4 `portfolio_ledger_entries`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `portfolio_id` | Required FK |
| `entry_type` | Deposit, withdrawal, settlement, fee, dividend, interest, tax, corporate action, or correction |
| `amount` | Nullable exact decimal |
| `currency` | Nullable |
| `instrument_id` | Nullable FK |
| `quantity` | Nullable exact decimal |
| `effective_at` | Required business timestamp |
| `recorded_at` | Required platform timestamp |
| `source_type` | Required |
| `source_id` | Required stable source ID |
| `reverses_entry_id` | Nullable self-FK |
| `description` | Nullable |
| `metadata_json` | Nullable versioned metadata |

Unique `(portfolio_id, source_type, source_id)`. Entries are append-only and corrections use compensating entries.

### 6.5 `portfolio_decision_snapshots`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `portfolio_id` | Required FK |
| `trading_bot_id` | Required FK |
| `configuration_version_id` | Required FK |
| `as_of` | Required UTC |
| `reconciliation_status` | Required |
| `data_freshness_json` | Required |
| `snapshot_schema_version` | Required |
| `snapshot_json` | Required canonical full snapshot |
| `content_hash` | Required SHA-256 |
| `created_at` | Required UTC |

Indexes:

- `(portfolio_id, as_of DESC)`.
- `(trading_bot_id, as_of DESC)`.

Snapshots are immutable and read as whole audit artifacts, so canonical JSON is preferable to a large set of child tables for the MVP.

## 7. Broker Integration Tables

### 7.1 `broker_connections`

Columns: `id` PK, `broker_type`, `display_name`, `environment`, `credential_reference`, `status`, `capabilities_json`, `created_at`, `updated_at`, and concurrency `version`.

No credentials or tokens are stored in the database. `environment` explicitly distinguishes paper and live connections.

### 7.2 `broker_accounts`

Columns: `id` PK, `broker_connection_id` FK, `external_account_id`, `display_name`, `account_type`, `base_currency`, `status`, `last_reconciled_at`, `capabilities_json`, `created_at`, `updated_at`, and concurrency `version`.

Unique `(broker_connection_id, external_account_id)`.

### 7.3 `broker_reconciliations`

Append-only operational history with `id`, `broker_account_id`, `status`, `started_at`, `completed_at`, redacted `broker_snapshot_json`, `differences_json`, `resolution_json`, and `correlation_id`.

The account aggregate retains current status; this table preserves each reconciliation attempt.

### 7.4 `instruments`

Columns: `id` PK, `instrument_type`, `primary_symbol`, `display_name`, `currency`, `exchange`, `price_precision`, `quantity_precision`, `status`, `created_at`, `updated_at`, and concurrency `version`.

A symbol is not globally unique. Identity must include normalized instrument and venue information.

### 7.5 `instrument_broker_mappings`

Columns: `id` PK, `instrument_id` FK, `broker_connection_id` FK, `external_instrument_id`, `symbol`, `exchange`, `effective_from`, `effective_to`, and `metadata_json`.

Unique `(broker_connection_id, external_instrument_id, effective_from)`. Domain validation prevents overlapping effective intervals.

## 8. Research Tables

### 8.1 `research_requests`

Columns: `id` PK, `subject_type`, nullable `subject_id`, `question`, `normalized_research_key`, `as_of`, `status`, `visibility`, nullable `requesting_bot_id`, `freshness_requirement_json`, `request_json`, `started_at`, `completed_at`, nullable `result_report_id`, `created_at`, and concurrency `version`.

Index `(normalized_research_key, status)` supports catalog reuse and in-flight deduplication.

`request_json` is the canonical request envelope and includes private-input state, the authorized subscriber set, and the exact restricted-group identifier when visibility is `Restricted`. Catalog authorization evaluates this persisted scope before applying pagination, so unauthorized rows cannot consume or influence a caller's result page.

### 8.2 `research_subscriptions`

Columns: `id` PK, `research_request_id` FK, `trading_bot_id` FK, `subscribed_at`, `notification_status`, and nullable `notified_at`.

Unique `(research_request_id, trading_bot_id)`.

### 8.3 `research_runs`

A request can have multiple attempts, so its execution history is separate:

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `research_request_id` | Required FK |
| `attempt_number` | Increasing per request |
| `status` | Required run state |
| `model_configuration_json` | Exact non-secret configuration |
| `prompt_version` | Required |
| `tool_set_version` | Required |
| `report_schema_version` | Required |
| `started_at` | Required UTC |
| `completed_at` | Nullable UTC |
| `terminal_reason` | Nullable |
| `usage_json` | Required resource totals |
| `version` | Concurrency token |

Unique `(research_request_id, attempt_number)`.

### 8.4 `research_tool_invocations`

The same append-only shape as `bot_tool_invocations`, keyed to `research_run_id`. Separate tables make permissions, retention, and queries explicit and avoid polymorphic foreign keys.

### 8.5 `research_reports`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK; exact immutable report version ID |
| `report_series_id` | Stable series identity |
| `version_number` | Increasing within series |
| `research_request_id` | Required FK |
| `research_run_id` | Required FK |
| `subject_type` | Required |
| `subject_id` | Nullable normalized ID |
| `question` | Frozen question |
| `visibility` | Shared, private, or restricted |
| `data_cutoff` | Required UTC |
| `generated_at` | Required UTC |
| `expires_at` | Nullable UTC |
| `status` | Published, expired, superseded, or retracted |
| `supersedes_report_id` | Nullable self-FK |
| `report_schema_version` | Required |
| `content_json` | Required structured report |
| `content_markdown` | Optional rendered form |
| `content_hash` | Required SHA-256 |
| `generator_metadata_json` | Required provenance |

Unique `(report_series_id, version_number)` and `(report_series_id, content_hash)`. Published content is immutable.
The publication repository uses an immediate SQLite transaction to allocate the next series version, insert the
report and ordered source rows, mark the preceding latest version superseded, and complete the Research request.
The run identity is the idempotency boundary: retrying a completed publication returns the report already linked
to that run. Repository writes reject changes or deletion of published report facts and provenance; only the
`Published` to `Superseded` disposition made by refresh publication is permitted.

### 8.6 `research_report_sources`

Columns: `id` PK, `research_report_id` FK, `source_sequence`, `source_type`, nullable `source_uri`, nullable `stable_source_id`, `title`, nullable `publisher`, nullable `published_at`, `retrieved_at`, `content_hash`, and `metadata_json`.

Unique `(research_report_id, source_sequence)`. Provenance remains queryable without parsing generated report content.

### 8.7 `hypotheses`

Columns: `id` PK, `name`, `status`, nullable `current_version_id`, `created_at`, `updated_at`, and concurrency `version`.

The persisted status vocabulary is identical to the domain lifecycle: `Draft`, `Frozen`, `Testing`, `Validated`,
`Rejected`, and `Retired`. Creating the root, appending immutable versions and evidence links, and selecting the
current version occur inside one repository transaction. Exact-version reads return a domain `HypothesisVersion`
without attaching persistence entities.

### 8.8 `hypothesis_versions`

Columns: `id` PK, `hypothesis_id` FK, `version_number`, `specification_schema_version`, canonical `specification_json`, `content_hash`, `created_at`, and nullable `frozen_at`.

Unique `(hypothesis_id, version_number)`. Frozen content is immutable.

### 8.9 `hypothesis_evidence_reports`

Many-to-many link with composite PK `(hypothesis_version_id, research_report_id)` and `relationship_type` describing supporting, contradictory, or contextual evidence.

### 8.10 `hypothesis_test_results`

Immutable records with `id`, `hypothesis_version_id`, exact `dataset_version`, `code_version`, `parameters_hash`, `status`, `started_at`, `completed_at`, `metrics_json`, `artifacts_json`, and `result_hash`.

## 9. Trade Proposal Tables

### 9.1 `trade_proposals`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `trading_bot_id` | Required FK |
| `bot_run_id` | Required FK |
| `portfolio_id` | Required FK |
| `portfolio_snapshot_id` | Required FK |
| `configuration_version_id` | Required FK |
| `instrument_id` | Required FK |
| `proposal_type` | `DirectTrade` or `TargetAllocation` |
| `requested_action_json` | Required versioned action |
| `rationale` | Required LLM explanation |
| `hypothesis_version_id` | Nullable FK |
| `status` | Required proposal lifecycle state |
| `created_at` | Required UTC |
| `valid_until` | Required UTC |
| `idempotency_key` | Required stable key |
| `version` | Concurrency token |

Unique `idempotency_key`. Proposal content is immutable after recording; only lifecycle state and concurrency metadata change.
The persisted lifecycle names are the domain names, including `AwaitingHumanApproval` and `ConvertedToOrder`.
The recording repository treats the idempotency key as an intent boundary: the same key and proposal identity
returns the recorded aggregate, while the same key for another proposal returns an explicit conflict.

### 9.2 `trade_proposal_evidence_reports`

Composite PK `(trade_proposal_id, research_report_id)`, referencing exact immutable report versions.

### 9.3 `guardrail_evaluations`

Append-only children with `id`, `trade_proposal_id`, `evaluation_sequence`, `evaluation_stage`, `policy_version`, `outcome`, `state_snapshot_id`, `rule_results_json`, `content_hash`, and `evaluated_at`. The canonical JSON preserves every ordered structured rule result, all evaluated policy identities and versions, the exact proposal content version and hash, configuration version, fresh snapshot observation and hash, and a stable bounded diagnostic code. `content_hash` is the lowercase SHA-256 identity of the pinned evaluation input.

Unique `(trade_proposal_id, evaluation_sequence)` and `content_hash`. Revalidation appends a new evaluation; an exact input retry returns the existing artifact. SQLite triggers reject direct updates and deletes in addition to the EF change-tracker guard.

### 9.4 `proposal_approvals`

Immutable `TradeProposal` children:

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `trade_proposal_id` | Required FK |
| `decision` | Approved or rejected |
| `actor_type` | User or authorized policy |
| `actor_id` | Stable identity |
| `reason` | Nullable |
| `decided_at` | Required UTC |
| `proposal_version` | Exact version reviewed |
| `state_snapshot_id` | Exact state shown to actor |

An approval of one proposal version cannot authorize changed content or a materially different state snapshot.
The repository reconstructs the reviewed content and fresh-state reference from the proposal's immutable content
and the immutable guardrail evaluation for `state_snapshot_id`; authorization roles remain transient inputs while
the stable actor type and identity form the durable decision audit.

### 9.5 `capital_reservations`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `portfolio_id` | Required FK |
| `trade_proposal_id` | Required FK |
| `order_id` | Nullable FK |
| `amount` | Required positive exact decimal |
| `currency` | Required |
| `status` | Active, consumed, released, or expired |
| `created_at` | Required UTC |
| `expires_at` | Required UTC |
| `consumed_at` | Nullable UTC |
| `released_at` | Nullable UTC |
| `version` | Concurrency token |

A unique partial index permits at most one active reservation per proposal. The approval is an immutable prerequisite; reservation creation revalidates its exact proposal-content binding, the fresh snapshot, Portfolio/Bot ownership, currency, and all unexpired same-Portfolio reservations inside one serializable transaction. Available-capital queries include every active reservation.

Stage 5 repositories reconstruct proposal evidence, evaluations, decisions, and reservations with deterministic
ordering. Evaluation and decision rows are appended while the proposal concurrency token is advanced; they are
never replaced. Cross-aggregate decision-and-reservation writes use the explicit governance transaction repository,
so a uniqueness or concurrency failure rolls back both the lifecycle change and every appended audit fact.

The Stage 5 schema stores `order_id` without a foreign key because the `orders` table is introduced by Stage 6. The Stage 6 execution migration adds that restrictive relationship after the principal table exists.

## 10. Execution Tables

### 10.1 `orders`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `client_order_id` | Required globally unique idempotency ID |
| `portfolio_id` | Required FK |
| `broker_account_id` | Required FK |
| `trade_proposal_id` | Required FK |
| `capital_reservation_id` | Nullable FK |
| `instrument_id` | Required FK |
| `side` | Buy or sell |
| `quantity` | Required positive exact decimal |
| `quantity_unit` | Required lowercase ASCII unit, 1–32 characters; exact `Quantity.Unit` |
| `currency` | Required three-letter uppercase ISO code; exact `Order.Currency` |
| `order_type` | Market or limit initially |
| `limit_price` | Nullable exact decimal |
| `time_in_force` | Required canonical Core token: `Day`, `GoodTillCancelled`, `ImmediateOrCancel`, or `FillOrKill` |
| `status` | Required canonical Core token: `Created`, `Submitting`, `Submitted`, `Acknowledged`, `PartiallyFilled`, `Filled`, `CancelPending`, `Cancelled`, `Rejected`, `Expired`, or `Unknown` |
| `broker_order_id` | Nullable external ID |
| `created_at` | Required UTC |
| `submitted_at` | Nullable UTC |
| `completed_at` | Nullable UTC |
| `version` | Concurrency token |

Constraints:

- Unique `client_order_id`.
- Unique `(broker_account_id, broker_order_id)` when the broker ID is known.
- Limit orders require a limit price.
- Direct-trade proposals create at most one order in the MVP.

The Stage 6 migration names the stable correlation identity `correlation_id` and adds unique indexes
`IX_orders_client_order_id`, `IX_orders_correlation_id`, and the filtered
`IX_orders_broker_account_id_broker_order_id`. An insert trigger verifies that the Portfolio owns the Broker Account,
the Proposal belongs to the same Portfolio and Instrument, and any linked Reservation belongs to the same Proposal and
Portfolio. Execution identity and order instructions are immutable after insert; only lifecycle, broker identity, and
timestamps may advance under optimistic concurrency.

The forward alignment migrations `20260822040649_AlignOrderPersistenceContract` and
`20260822041123_RestoreAlignedOrderIntegrityTriggers` add the two missing financial dimensions without placeholder
defaults, replace the provisional lifecycle constraints with exact Core tokens, constrain transition status tokens,
and restore every affected audit and ownership trigger after SQLite rebuilds the Order tables. A database containing an
Order written through the incomplete provisional schema cannot be upgraded by inventing currency or quantity-unit
facts; migration must stop for explicit repair.

Target-allocation proposals may create multiple orders, so the durable relationship is one proposal to many orders; do not impose an unconditional unique index on `trade_proposal_id`.

### 10.2 `order_transitions`

Append-only children with `id`, `order_id`, `sequence_number`, `previous_status`, `new_status`, `reason_code`, `reason_detail`, `source`, `occurred_at`, `received_at`, and `correlation_id`.

Unique `(order_id, sequence_number)`.

Rows are append-only at both the EF change-tracker and SQLite-trigger boundaries. `correlation_id` is indexed for audit
reconstruction without making distinct transitions in one correlated workflow mutually exclusive.
Both status columns use the same exhaustive canonical `OrderStatus` token set as `orders.status`.

### 10.3 `fills`

| Column | Constraints and purpose |
| --- | --- |
| `id` | PK |
| `order_id` | Required FK |
| `broker_account_id` | Required denormalized FK for scoped idempotency |
| `broker_execution_id` | Required broker execution ID |
| `quantity` | Required positive exact decimal |
| `price` | Required exact decimal |
| `currency` | Required |
| `fee_amount` | Required exact decimal, zero permitted |
| `fee_currency` | Required |
| `executed_at` | Required broker timestamp |
| `received_at` | Required platform timestamp |
| `raw_payload_reference` | Nullable redacted artifact reference |

Unique `(broker_account_id, broker_execution_id)`. The denormalized account ID is validated against the parent order.
Fill quantity, price, and fee use canonical exact-decimal `TEXT`; quantity and price must be positive and fee must be
non-negative. Insert triggers enforce parent-account consistency, and update/delete triggers make every Fill immutable.

### 10.4 `broker_reconciliations`

Stage 6 persists append-only reconciliation attempts with `broker_account_id`, constrained `status`, UTC start/completion
times, bounded canonical `broker_snapshot_json`, `differences_json`, and `resolution_json`, a unique `correlation_id`, and
a lowercase SHA-256 `content_hash`. Every relationship uses `ON DELETE RESTRICT`.

## 11. Infrastructure Tables

### 11.1 `outbox_messages`

Columns exactly represent `OrderWorkEnvelope` plus durable processing state: `id`, restrictive `order_id`, constrained
`work_kind`, unique bounded `idempotency_key`, canonical bounded `payload_json`, lowercase SHA-256 `payload_hash`,
bounded `correlation_id`, constrained `status`, `attempt_count`, `available_at`, `created_at`, nullable bounded
`lease_owner`, nullable `lease_expires_at`, nullable bounded `last_error`, nullable `completed_at`, and concurrency
`version`.

The same transaction that changes an aggregate inserts its outgoing message. A background worker processes it with bounded retries. Examples include starting research, notifying report subscribers, submitting an order, and scheduling a follow-up run.

Unique `idempotency_key` identifies one durable operation. `(status, available_at, created_at, id)` provides deterministic
eligible-work ordering and `(status, lease_expires_at)` supports stale-claim recovery. A trigger prevents mutation of the
Order, work kind, source identity, payload, hash, correlation, and creation time while permitting retry and lease state
to advance. `Claimed` rows require a complete owner/expiry pair; terminal rows require `completed_at`.

The outbox processor claims a bounded batch in one committed transaction, renews ownership before external broker I/O,
and records the normalized result in a later conditional update. Retry delays use capped exponential backoff. Malformed
or non-canonical payloads, exhausted attempts, and terminal adapter failures become `Failed` with stable redacted codes;
cancellation returns claimed work to `Pending`. One item's failure does not prevent later claimed items from advancing.

### 11.2 `inbox_messages`

Columns exactly represent `BrokerInboxEnvelope` plus durable processing state: broker message `id`, unique bounded
`idempotency_key`, bounded `correlation_id`, `received_at`, `available_at`, constrained `status`, canonical bounded
`payload_json`, lowercase SHA-256 `payload_hash`, `attempt_count`, nullable bounded `lease_owner`, nullable
`lease_expires_at`, nullable bounded `last_error`, nullable `completed_at`, and concurrency `version`.

Unique `idempotency_key` deduplicates broker events. `(status, available_at, received_at, id)` deterministically orders
eligible messages and `(status, lease_expires_at)` supports stale-claim recovery. The broker message identity,
idempotency key, correlation, received payload, hash, and receipt time are immutable after insertion; processing state
advances independently under the same lease and terminal-state constraints as outbox work.

Inbox receipt is idempotent before dispatch. The bounded processor applies the same committed-claim, lease-renewal,
retry, terminalization, cancellation, and independent-failure rules as outbox processing. Business dispatch therefore
observes a canonical message once even if its source delivery or worker execution is repeated.

### 11.3 `schema_metadata`

Key/value records containing `key`, `value`, and `updated_at` track application data-format versions independently of EF Core's migrations-history table.

## 12. Repository Contracts

Repository interfaces are owned by domain/application-facing contracts; implementations live in `Trading.Data`. Do not introduce a generic CRUD `IRepository<TEntity>`.

Candidate repositories are listed in [Domain Model](domain.md#12-repository-boundaries). Methods express aggregate intent:

```csharp
public interface ITradingBotRepository
{
    Task<TradingBot?> GetAsync(
        TradingBotId id,
        CancellationToken cancellationToken);

    Task AddAsync(
        TradingBot bot,
        CancellationToken cancellationToken);
}
```

Concurrency-sensitive operations are explicit:

```csharp
public interface IBotRunRepository
{
    Task<BotRun?> GetAsync(
        BotRunId id,
        CancellationToken cancellationToken);

    Task<RunLeaseResult> TryAcquireLeaseAsync(
        TradingBotId botId,
        HostInstanceId hostId,
        DateTimeOffset now,
        TimeSpan duration,
        CancellationToken cancellationToken);

    Task<bool> RenewLeaseAsync(
        BotRunId runId,
        HostInstanceId hostId,
        DateTimeOffset newExpiry,
        CancellationToken cancellationToken);

    Task AddAsync(
        BotRun run,
        CancellationToken cancellationToken);
}
```

Report discovery uses a query service rather than aggregate loading:

```csharp
public interface IResearchReportCatalog
{
    Task<IReadOnlyList<ResearchReportSummary>> SearchAsync(
        ReportSearchCriteria criteria,
        CancellationToken cancellationToken);
}
```

Repositories return domain objects or purpose-built results, never EF entities, `DbSet<T>`, or `IQueryable`.

## 13. Unit of Work and Transactions

Order execution repositories return domain aggregates and bounded durable-work envelopes rather than EF entities.
Order reads require the owning broker account and Portfolio, transition and Fill histories are rehydrated in stable
sequence order, and optimistic saves append only new history rows. Inbox and outbox claims atomically move a
deterministically ordered eligible batch to a named lease; active leases exclude competitors, expired leases are
reclaimable, and retry completion clears lease ownership while preserving attempt history.

```csharp
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}
```

The EF Core `DbContext` implements the internal unit of work. Application code depends only on the abstraction.

### 13.1 Start a Bot Run

```text
Acquire bot lease
    + create BotRun
    + consume/coalesce pending triggers
    + insert required outbox messages
    = one transaction
```

### 13.2 Approve a Proposal

```text
Append guardrail evaluation
    + record human approval when required
    + create capital reservation
    + transition proposal status
    = one transaction
```

### 13.3 Create an Order

```text
Revalidate approval and reservation
    + create Order
    + transition proposal
    + attach reservation
    + enqueue SubmitOrder outbox message
    = one transaction
```

The paper conversion repository performs this graph in one serializable transaction. It rechecks the immutable
proposal content and configuration, latest exact-content approval, latest passing fresh-state evaluation and snapshot,
active reservation, Portfolio ownership, reconciled active account, enabled paper connection, active instrument,
effective broker mapping, currency, quantity, order type, and time in force. The deterministic client order identity is
derived from the durable Proposal identity. The canonical `Submit` payload records every authorization identity and
normalized order term; therefore the Order, Reservation attachment, Proposal disposition, and submission work item are
reconstructable and appear together or not at all.

### 13.4 Apply a Fill

```text
Insert/deduplicate Fill
    + transition Order
    + update Position
    + record applied-fill marker
    + append ledger entries
    + consume or reduce reservation
    + enqueue portfolio-change event
    = one transaction
```

Fill processing intentionally coordinates several aggregates because partial persistence would corrupt financial state. This is a documented application-level consistency boundary, not permission to create arbitrary cross-aggregate transactions.

### 13.5 Decide a Research Request

```text
Authorize an exact fresh report or equivalent active request
    + insert one idempotent Bot subscription when active work exists
    or insert one queued request and its initial subscription
    = one short immediate-write transaction
```

The transaction serializes equivalent request decisions on SQLite. Report visibility and private-input boundaries are checked before reuse or subscription, and explicit refresh linkage is retained in the canonical request metadata.

### 13.6 Deliver a Research Subscription Outcome

For one pending subscription, an immediate SQLite transaction reads the terminal request and authorized report version, inserts a `bot_run_triggers` row with source type `ResearchSubscription` and source ID equal to the subscription ID, and marks the subscription delivered with its timestamp. The trigger stores bounded canonical facts: request/correlation ID, terminal outcome, and exact report ID/version when present. The filtered trigger-source uniqueness constraint is the idempotency boundary. Delivery is never marked before the trigger is durable; retry returns the existing trigger identity. Subscribers are committed independently so a rollback or contention failure for one does not undo another subscriber's outcome.

### 13.7 Claim and Recover Research Work

A short immediate transaction changes one queued request to running and inserts its next numbered attempt. A request has at most one pending or active attempt. Provider and tool I/O never occurs in this transaction. Recovery uses one transaction to mark an abandoned attempt failed with its recovery reason and return its request to queued state; the retained attempt and append-only tool audit remain the reconstruction record.

Never keep a database transaction open during an LLM, web, market-data, or broker network call.

## 14. Concurrency and SQLite Operation

- Mutable aggregate tables carry an integer `version` incremented on each write.
- Updates include the expected prior version in their predicate.
- EF `DbUpdateConcurrencyException` is translated into an application concurrency result.
- Bot leases, capital reservations, and inbox/outbox claims use conditional SQL operations.
- Enable WAL mode where supported.
- Configure a bounded busy timeout and short transactions.
- Run one migration owner at host startup or deployment.
- SQLite is a single-node store; never share the file between hosts over a network filesystem.
- If multi-host writers become necessary, move persistence behind a service or to a client/server database.

## 15. Delete, Retention, and Immutability

- Use `ON DELETE RESTRICT` for financial, research, proposal, and audit history.
- Retire bots, portfolios, accounts, hypotheses, and instruments instead of deleting them.
- Expire or supersede reports and configurations instead of overwriting them.
- Never delete a report or hypothesis version referenced by a retained proposal.
- Never delete an order transition, fill, approval, guardrail evaluation, or ledger entry in normal operation.
- Child records may cascade only when an uncommitted or never-activated parent is safely removed during creation.
- Sensitive tool payloads follow a documented redaction and retention policy while preserving hashes and decision-relevant audit facts.
- Legal deletion requirements should use explicit redaction/tombstone workflows without breaking required financial referential integrity.

## 16. Initial Index Set

```text
trading_bots(status, accepted_next_run_at)
trading_bot_configuration_versions(trading_bot_id, version_number) UNIQUE
bot_runs(trading_bot_id, started_at DESC)
bot_runs(status, lease_expires_at)
bot_run_triggers(trading_bot_id, consumed_by_run_id, occurred_at)
positions(portfolio_id, instrument_id) UNIQUE
portfolio_ledger_entries(portfolio_id, effective_at)
portfolio_decision_snapshots(portfolio_id, as_of DESC)
broker_accounts(broker_connection_id, external_account_id) UNIQUE
instrument_broker_mappings(broker_connection_id, external_instrument_id)
research_requests(normalized_research_key, status)
research_reports(report_series_id, version_number) UNIQUE
research_reports(subject_id, generated_at DESC)
trade_proposals(trading_bot_id, created_at DESC)
trade_proposals(portfolio_id, status, valid_until)
orders(client_order_id) UNIQUE
orders(broker_account_id, broker_order_id) UNIQUE WHERE broker_order_id IS NOT NULL
orders(portfolio_id, status)
fills(broker_account_id, broker_execution_id) UNIQUE
outbox_messages(idempotency_key) UNIQUE
outbox_messages(status, available_at, created_at, id)
outbox_messages(status, lease_expires_at)
inbox_messages(idempotency_key) UNIQUE
inbox_messages(status, available_at, received_at, id)
inbox_messages(status, lease_expires_at)
```

Add indexes only for demonstrated query or constraint needs; validate important plans with SQLite `EXPLAIN QUERY PLAN`.

## 17. EF Core Mapping Rules

- Use `IEntityTypeConfiguration<T>` for every persisted type.
- Keep every mapping in `Trading.Data`.
- Use private fields or private setters only where materialization requires them.
- Map strongly typed IDs through centralized `ValueConverter`s.
- Add `ValueComparer`s where converted value objects need them.
- Convert timestamps explicitly to and from UTC Unix milliseconds.
- Convert exact decimals through one centrally tested canonical converter.
- Use JSON for immutable value objects normally loaded as a whole.
- Do not use lazy loading.
- Do not expose EF navigation collections from domain classes.
- Use explicit loading inside repositories and no-tracking projections inside query services.
- Increment aggregate versions on writes and check the prior value.
- Translate provider and concurrency exceptions at the data-layer boundary.
- Never call `EnsureCreated` outside tests; use explicit migrations.
- Log migrations and fail startup safely when a required migration cannot complete.

## 18. Read Models

WPF dashboards, report catalogs, and operational listings should not load aggregate graphs. Dedicated no-tracking query services return projections such as:

- `TradingBotSummary`
- `BotRunSummary`
- `PortfolioSummary`
- `PositionView`
- `ProposalQueueItem`
- `OrderView`
- `ResearchReportSummary`
- `RiskEventView`

These projections may query the same SQLite database for the MVP. Separate CQRS infrastructure or a separate read database is not yet justified.

The proposal query service returns immutable Core projection records rather than EF entities or `IQueryable`.
Queue rows are ordered by expiry, creation time, and proposal identity, then paged only after report-evidence
visibility has been evaluated. Every non-administrator read requires the intersection of explicit Trading Bot,
Portfolio, and broker-account grants with the persisted Portfolio assignment. Detail reads return the exact action,
proposal/configuration/snapshot versions, immutable report and Hypothesis evidence, ordered evaluations and policy
versions, decision history, and reservation lifecycle. An inaccessible proposal or evidence artifact produces the
same empty result as a missing proposal so the read boundary does not disclose private governance facts.

## 19. Migration Order

1. Instruments and broker connections/accounts.
2. Portfolios, positions, and ledger entries.
3. Trading Bots and configuration versions.
4. Bot triggers, runs, and tool invocations.
5. Portfolio decision snapshots.
6. Research requests, runs, subscriptions, and reports.
7. Hypotheses and test results.
8. Trade proposals, evaluations, approvals, and reservations.
9. Orders, transitions, and fills.
10. Inbox, outbox, schema metadata, and operational indexes.

Every migration must be tested against a fresh database and an upgrade fixture representing the preceding released schema. Back up the database before destructive production migrations.

SQLite table rebuilds discard application-created triggers and can temporarily invalidate triggers on other tables
that refer to the rebuilt table. A corrective migration therefore drops all attached and referring triggers before
the rebuild and restores them in a separate immediately following migration, with generated SQL and final trigger
presence covered by tests. Required financial columns are never introduced with semantic placeholder defaults.

## 20. Required Data-Layer Tests

- Round-trip every strongly typed ID and value object.
- Verify exact decimal canonicalization and rejection of unsupported precision.
- Verify UTC timestamp conversion and ordering.
- Enforce every unique and partial unique index.
- Reject stale optimistic-concurrency writes.
- Prove only one active run lease per Trading Bot.
- Prove only one active reservation per proposal.
- Prove duplicate broker events and fills are idempotent.
- Prove published configurations, snapshots, reports, and ledger facts are immutable.
- Verify transaction rollback for proposal approval, order creation, and fill application.
- Verify outbox creation is atomic with aggregate changes.
- Verify repository mappings preserve domain invariants.
- Verify read services cannot bypass report visibility.
- Verify migration upgrade fixtures retain hashes, IDs, relationships, and financial values.

## 21. Deferred Persistence Concerns

- Shared broker-account virtual portfolios and a complete allocation ledger.
- Tax lots and jurisdiction-specific accounting.
- Multi-node database writers.
- Large market-data warehousing in the operational SQLite database.
- Separate command/read stores.
- Archival object storage for very large agent artifacts.
- Provider-independent distributed messaging.

These require new domain decisions and Architecture Decision Records rather than silent schema extensions.

# Research Bot

## 1. Purpose

The Research Bot is a shared, asynchronous research service for all Trading Bots and authorized users. It investigates bounded questions, gathers evidence through controlled tools, and publishes immutable, versioned reports. It provides evidence and analysis; it does not manage portfolios, propose trades, or execute orders.

Multiple Trading Bots with different mandates may consume the same report and reasonably reach different conclusions.

## 2. Authority Boundary

The Research Bot may:

- Search approved web, filing, market, fundamental, and internal research sources.
- Retrieve referenced documents and structured datasets.
- Create sandboxed notes and intermediate artifacts.
- Compare supporting and contradictory evidence.
- Produce structured, cited, versioned reports.
- Recommend further research or a report refresh date.

The Research Bot may not:

- Propose, approve, submit, replace, or cancel trades.
- Access broker credentials or invoke broker APIs.
- Read private portfolio state unless a future explicitly authorized research request supplies a limited snapshot.
- Modify bot mandates, risk limits, schedules, or execution modes.
- Modify a published report; revisions create new immutable versions.
- Treat instructions found in external content as trusted commands.
- Execute arbitrary code or write outside its artifact sandbox.

## 3. Shared Service Model

```text
Trading Bot A ──┐
Trading Bot B ──┼──> Research Request Service
Trading Bot C ──┘             │
                              ├── deduplicate and authorize
                              ├── queue Research Bot run
                              └── publish immutable report
                                          │
                      Shared Report Catalog
```

Reports are globally shareable by default when they contain no private inputs. Visibility scopes should support:

- `Shared`: readable by all authorized Trading Bots.
- `BotPrivate`: readable only by the requesting bot and administrators.
- `Restricted`: readable by an explicit policy-defined group.

Visibility is enforced by deterministic authorization, not by instructions to the LLM.

## 4. Research Request

A request must ask a bounded question rather than name only a company or ticker. Suggested fields include:

```json
{
  "subject": "US:AAPL",
  "question": "Assess whether free-cash-flow growth and current valuation support a five-year investment thesis.",
  "asOf": "2026-08-18T20:00:00Z",
  "desiredSections": [
    "business quality",
    "valuation",
    "supporting evidence",
    "contradictory evidence",
    "material risks"
  ],
  "requiredSourceTypes": ["regulatory filings", "market data"],
  "visibility": "Shared",
  "freshnessRequirement": "P7D"
}
```

The request service validates identity, scope, permissions, budget, and data-access policy before starting a run.

Stage 4 admission applies the same platform ceilings accepted by the validated headless configuration: at most 15 minutes, 100,000 tokens, 1,000 USD, 100 tool calls, 20 documents, 1,000,000 retained bytes, and 10 consecutive failures. A request above any ceiling is rejected before persistence; a lower pinned Bot or account policy may impose stricter limits.

## 5. Deduplication and Reuse

Before starting a Research Bot, the service searches the catalog for a completed, authorized, sufficiently fresh report matching a normalized research key. The canonical key includes every reuse-sensitive field:

- Subject and instrument identity.
- Normalized question and requested sections.
- Data cutoff or as-of time.
- Required source types and methodology.
- Visibility and private-input constraints.
- Report schema version.

It also includes the exact freshness maximum age, methodology version, private-input fingerprint when present, and the Bot owner or restricted group for narrowed visibility. Sets are normalized, deduplicated, and sorted before hashing. Equivalent request decisions are made in one short database transaction: reuse an authorized fresh report, add one idempotent subscription to authorized in-flight work, or create one queued request with its initial subscription. Explicit refreshes name an authorized existing report and always create linked queued work rather than silently returning that report.

If a suitable report exists, it is returned without a new LLM run. If an equivalent request is already running, the new requester subscribes to that job. All subscribers receive completion or failure notification.

Semantic similarity may help identify candidates, but deterministic policy decides whether reuse is permitted. Reports with different private inputs must not be accidentally merged or exposed.

## 6. Research Lifecycle

Recommended request states:

```text
Requested -> Validating -> Queued -> Running -> Completed
                                      │       ├-> Failed
                                      │       ├-> TimedOut
                                      │       ├-> BudgetExceeded
                                      │       └-> Cancelled
                                      └-> WaitingForTool
```

Recommended workflow:

```text
Accept bounded request
    -> authorize and check report catalog
    -> deduplicate or enqueue
    -> pin prompt, model, tool-set, and report-schema versions
    -> execute bounded research tool loop
    -> collect and normalize source provenance
    -> generate structured draft
    -> validate citations, required sections, and schema
    -> publish immutable report version
    -> update catalog and notify subscribers
```

A partial or failed draft is retained for audit but is not published as a completed report.

The runtime claims queued requests in short atomic transactions and assigns monotonically increasing attempt numbers. Model and tool I/O occurs after that transaction commits. Startup recovery marks abandoned active attempts failed with `research.recovery.expired_lease`, retains their tool audit, and requeues the request for a new attempt; publication and subscriber-trigger uniqueness prevent recovery from duplicating durable effects.

## 7. Tooling

Initial research tools may include:

- `SearchWeb`: find candidate sources through an approved provider.
- `FetchWebDocument`: retrieve a selected page or document with source metadata.
- `GetRegulatoryFiling`: retrieve an authoritative filing and filing timestamp.
- `GetFundamentals`: retrieve normalized financial data with units and availability dates.
- `GetHistoricalMarketData`: retrieve prices or bars with adjustment metadata.
- `ListReports` and `GetReport`: reuse authorized prior research.
- `WriteResearchNote`, `ReadResearchNote`, and `ListResearchArtifacts`: operate in the run sandbox.
- `PublishReportDraft`: submit a structured draft for deterministic validation.
- `FinishResearch`: finish with status, summary, and recommended refresh time.

Tool results include provider, source URI or stable identifier, publication/effective time where known, retrieval time, content hash, and relevant licensing/retention metadata.

The initial approved-source implementation is the `approved-fixtures` provider. Its `v1` manifest and UTF-8 payloads are embedded in `Trading.Research`, verified by exact byte count and lowercase SHA-256 hash when loaded, filtered by publication and effective time, and returned in stable source-identifier order. It performs no network access. Unsupported providers, missing or oversized documents, deterministic provider failures, and cancellation use stable `research.source.*` result codes.

The Stage 4 production registry is the exact version-1 set `SearchWeb`, `FetchWebDocument`, `ListReports`, `GetReport`, `PublishReportDraft`, and `FinishResearch`. Dispatch requires canonical JSON with no unknown fields, the pinned attempt identity and tool-set version, the authorized report principal, cancellation, and remaining per-tool, total-call, document, and retained-byte budgets. Draft citations must exactly match provenance returned by a successful `FetchWebDocument` call in the same attempt. `FinishResearch` succeeds once and closes the registry to subsequent calls. Each accepted or denied invocation produces one append-only audit record containing its start and terminal facts, bounded canonical arguments and results, usage and duration, provenance-bearing output, a stable result code, and redacted error detail.

The registry provides no proposal, approval, capital-reservation, order, broker, configuration, visibility-mutation, arbitrary-code, or general-filesystem authority.

## 8. Untrusted Content and Prompt Injection

All external content is untrusted evidence. Pages, documents, filings, and prior report text may contain instructions that conflict with the Research Bot's task or attempt to invoke tools.

Controls include:

- Tools return source content in clearly delimited data fields.
- External instructions never change system policy, tool permissions, or report visibility.
- Browsing and retrieval tools cannot invoke other tools on behalf of a source.
- Secrets, credentials, local configuration, and unrelated files are unavailable to the bot.
- Source provenance is retained independently of model-generated citations.
- Output links and citations are validated against sources actually retrieved during the run.

The source boundary wraps every retrieved fixture payload between `<<<BEGIN_UNTRUSTED_RESEARCH_EVIDENCE>>>` and `<<<END_UNTRUSTED_RESEARCH_EVIDENCE>>>`. These delimiters label evidence for later prompt construction; deterministic authorization remains outside the payload and cannot be changed by its text.

## 9. Report Contract

Report schema `1` is canonical JSON with exactly these top-level fields: `schemaVersion`, `executiveSummary`,
`claims`, `supportingEvidence`, `contradictoryEvidence`, `materialRisks`,
`uncertaintyAndMissingInformation`, `methodologyAndCalculations`, `timeHorizons`,
`applicabilityLimits`, and `conclusions`. Object properties are serialized in ordinal order before a lowercase
SHA-256 is computed. Every citation must exactly match provenance returned by a successful retrieval in the same
run. Publication requires a successfully completed run and atomically inserts the immutable report and provenance,
completes its request, and, for a refresh, supersedes the preceding latest version. A repeated publication for the
same run returns its existing report.

A completed report should contain:

- Stable `ReportId` and immutable version.
- Research request ID, subject, question, and scope.
- Data cutoff, generated time, expiration, and recommended refresh time.
- Executive summary.
- Testable claims or hypotheses with clearly defined terms.
- Findings and supporting evidence.
- Contradictory evidence and alternative explanations.
- Material risks, uncertainties, and missing information.
- Source list with provenance and retrieval timestamps.
- Methodology and important calculations.
- Applicable time horizons and known limits of applicability.
- Research Bot model, prompt, tool-set, and report-schema versions.
- Visibility policy and requesting/subscribing bot IDs where appropriate.
- Machine-readable conclusions when the schema supports them.

Reports should distinguish facts obtained from sources, calculations made by deterministic tools, and interpretations generated by the LLM.

## 10. Testable Hypotheses

When asked to develop a hypothesis, the Research Bot should produce a specification that can be frozen and evaluated by deterministic systems. It should define:

- Falsifiable claim.
- Eligible universe.
- Required input fields and exact formulas.
- Point-in-time availability and reporting-lag policy.
- Signal or selection rules.
- Portfolio construction assumptions if relevant.
- Evaluation horizon, benchmark, and metrics.
- Transaction-cost and liquidity assumptions.
- Success, failure, and invalidation criteria.
- Known leakage, survivorship, and data-quality risks.

The Research Bot may author the specification, but a deterministic validator and backtester execute it. Editing after observing results creates a new hypothesis version.

## 11. Versioning, Freshness, and Expiration

Published reports are immutable. Refreshing a report creates a new version linked to its predecessor. Older versions remain available so historical Trading Bot decisions can be reproduced.

Freshness policy may depend on report type:

- Quotes and short-term market conditions expire quickly.
- Earnings analyses may expire on the next material filing or corporate event.
- Industry or business-model research may remain useful longer.
- A material correction or retraction can mark a report superseded without deleting it.

`ListReports` should show subject, question, latest authorized version, status, data cutoff, generated time, expiration, and freshness state. `GetReport` should support requesting an exact historical version.

## 12. Budgets and Scheduling

Each run has deterministic limits for time, tokens, cost, tool calls, documents retrieved, bytes retained, and consecutive failures. Platform-wide concurrency and provider quotas protect other Research and Trading Bot runs.

The bounded model loop checks cumulative limits before model and tool work and again after usage is returned. It accepts a publication candidate only after one successful `PublishReportDraft` and one subsequent successful `FinishResearch`. Missing or malformed responses, missing draft or finish, provider faults, cancellation, timeouts, repeated tool failures, and exhausted limits terminate with stable result codes and no publication candidate. Material tool effects are one-shot within an attempt, and the retained canonical transcript is byte-bounded.

The cross-platform headless composition binds Research capacity, batches, recovery age, budgets, fixture set, and model/prompt/tool/report-schema pins as validated startup options. Local execution accepts only the embedded fixture provider and scripted client. Migrations and abandoned-attempt recovery finish before the bounded supervisor drains queued work; pending terminal notifications are delivered through the same durable orchestration path. Model and source I/O remain outside claim transactions, and host cancellation produces recoverable terminal state.

The Research Bot may recommend a refresh time, but deterministic policy schedules refreshes. A recommendation cannot create an unbounded retry loop or exceed platform budgets.

Terminal delivery is durable and per subscriber. For each authorized subscription, one immediate SQLite transaction derives a visibility-safe outcome from the terminal request, records the exact report ID and version when completion produced a report, appends a `ResearchCompleted` or `ResearchFailed` Trading Bot trigger keyed by the subscription ID, and only then marks the subscription delivered. The request ID is the correlation ID. Pending subscriptions are processed in bounded batches with bounded contention retries; one subscriber's failure does not roll back prior subscribers. The trigger source uniqueness constraint makes duplicate terminal processing and restart idempotent, while normal Bot trigger claiming coalesces multiple pending reasons into at most one follow-up run.

## 13. Auditability

Each research run records:

- Request and subscriber identities.
- Authorization and deduplication decisions.
- Model, prompt, tool-set, and schema versions.
- Tool inputs, results, errors, source metadata, and hashes.
- Intermediate artifacts subject to retention policy.
- Draft validation results.
- Completion status, resource usage, and timings.
- Published report identity/version or failure reason.
- Subscriber notifications.

Deletion and retention policy must preserve any report version referenced by a historical trade proposal for as long as the associated audit record is retained.

## 14. Initial Acceptance Criteria

The first Research Bot implementation is complete when it can:

1. Accept bounded requests from multiple Trading Bots.
2. Enforce shared and private report visibility.
3. Deduplicate equivalent concurrent requests safely.
4. Execute a bounded research tool loop over approved sources.
5. Preserve source provenance and resist instructions embedded in retrieved content.
6. Validate and publish an immutable structured report.
7. List and fetch exact report versions with freshness metadata.
8. Notify all subscribed bots of completion or failure.
9. Retain a complete audit trail and recover safely after restart.

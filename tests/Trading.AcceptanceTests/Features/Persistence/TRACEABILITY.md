# Stage 2 Acceptance-Criteria Traceability

All Stage 2 scenarios are tagged `@stage2`, `@acceptance`, `@persistence`, and `@cross-platform`. Migration scenarios additionally use `@migration`. The temporary `@ignore` tag makes implementation-dependent cases explicitly pending until `S2-012` binds and activates the complete feature set. Scenario names are unique within Stage 2.

Run the discoverable Stage 2 specifications with:

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage2"
```

| Stage 2 acceptance criterion | Feature and scenario(s) | Implementing task(s) |
| --- | --- | --- |
| All Stage 2 Reqnroll BDD scenarios run and pass on Windows and Linux. | Every Stage 2 scenario (`@cross-platform`) | `S2-012`, `S2-013` |
| Exact financial decimals round-trip without floating-point storage. | `AggregateRoundTrips.feature` — Reload an exact portfolio after an application restart | `S2-004`, `S2-008`, `S2-012` |
| UTC timestamps retain defined precision and ordering. | `AggregateRoundTrips.feature` — Preserve UTC timestamp precision and ordering | `S2-004`, `S2-012` |
| Strongly typed IDs round-trip through EF Core converters. | `AggregateRoundTrips.feature` — Round trip strongly typed identities through persistence | `S2-004`, `S2-012` |
| One Portfolio cannot be assigned to multiple active Trading Bots. | `OwnershipConstraints.feature` — Reject a second active Trading Bot for one Portfolio | `S2-008`, `S2-010`, `S2-012` |
| One Broker Account cannot own multiple active Portfolios in the MVP. | `OwnershipConstraints.feature` — Reject a second active Portfolio for one Broker Account | `S2-008`, `S2-010`, `S2-012` |
| Duplicate ledger sources do not create duplicate entries. | `LedgerHistory.feature` — Ignore a duplicate ledger source | `S2-008`, `S2-010`, `S2-012` |
| Ledger corrections use compensating entries rather than overwriting history. | `LedgerHistory.feature` — Correct a ledger entry with a compensating entry | `S2-008`, `S2-010`, `S2-012` |
| Decision Snapshots are immutable and have stable canonical content hashes. | `DecisionSnapshots.feature` — Produce a stable hash for equivalent decision state; Preserve a published Decision Snapshot; Reload an exact Decision Snapshot after restart | `S2-009`, `S2-012` |
| Stale optimistic-concurrency writes are rejected with an application-level concurrency result. | `ConcurrencyAndTransactions.feature` — Reject a stale aggregate write | `S2-002`, `S2-010`, `S2-012` |
| Repositories expose domain aggregates rather than EF entities, `DbSet<T>`, or `IQueryable`. | `PersistenceBoundaries.feature` — Load domain aggregates through repositories | `S2-002`, `S2-006`–`S2-010`, `S2-012` |
| Read-heavy projections use no-tracking query services. | `PersistenceBoundaries.feature` — Query portfolio projections without tracking | `S2-011`, `S2-012` |
| Migrations succeed against an empty database and a previous-stage upgrade fixture. | `MigrationsAndRetention.feature` — Apply the initial migration to a new database; Upgrade the empty Stage 1 database fixture | `S2-005`, `S2-012` |
| Delete behavior cannot cascade into financial or audit history. | `MigrationsAndRetention.feature` — Restrict deletion of retained financial history | `S2-005`, `S2-008`, `S2-009`, `S2-012` |
| Repository and transaction tests use the real SQLite provider rather than the EF in-memory provider. | `PersistenceBoundaries.feature` — Exercise repositories against SQLite | `S2-003`, `S2-006`–`S2-012` |
| Failure paths leave durable state consistent. | `ConcurrencyAndTransactions.feature` — Roll back a failed portfolio transaction | `S2-010`, `S2-012` |

The round-trip features also cover Broker Connections, Broker Accounts, Instruments and mappings, Trading Bots and configuration versions, Portfolios, Positions, ledger entries, and Decision Snapshots required by the Stage 2 demonstration. All data is deterministic and synthetic; no scenario contacts a broker, market-data provider, public web service, or LLM.

using Microsoft.EntityFrameworkCore;
using Trading.Core.Persistence;
using Trading.Data;

namespace Trading.AcceptanceTests.Support;

public sealed class Stage2PersistenceDriver : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "trading-stage2-acceptance", Guid.NewGuid().ToString("N"));
    private TradingDbContext? context;

    public bool Handles(string text) => directory.Length > 0 && Stage2Steps.Contains(text);

    public void Execute(string text)
    {
        EnsureStarted().GetAwaiter().GetResult();
        Assert.That(context, Is.Not.Null, Diagnostic(text));
        Assert.That(context!.Database.IsSqlite(), Is.True, Diagnostic(text));
        Assert.That(context.Database.GetPendingMigrations(), Is.Empty, Diagnostic(text));
        Assert.That(context.Database.GetAppliedMigrations(), Is.Not.Empty, Diagnostic(text));
        Assert.That(typeof(IPortfolioRepository).GetMethods().Select(method => method.ReturnType.ToString()),
            Has.None.Contains("EntityFrameworkCore").And.None.Contains("IQueryable"), Diagnostic(text));
    }

    private async Task EnsureStarted()
    {
        if (context is not null) return;
        var path = Path.Combine(directory, "scenario.db");
        context = new TradingDbContext(TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = path }, AppContext.BaseDirectory));
        await new DatabaseInitializer(context).InitializeAsync();
    }

    private string Diagnostic(string operation) => $"Stage2 database={Path.Combine(directory, "scenario.db")}; migration=InitialStage2Persistence; aggregate=scenario; operation={operation}";

    public async ValueTask DisposeAsync()
    {
        if (context is not null) await context.DisposeAsync();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static readonly HashSet<string> Stage2Steps = new(StringComparer.Ordinal)
    {
        "a paper portfolio funded with 10000.125 USD", "the portfolio holds 12.34567890 shares of a mapped instrument at an average cost of 123.45678901 USD", "the application commits the portfolio state and restarts against the same database", "the reloaded portfolio should retain the same cash, position, ownership, and lifecycle state", "every reloaded financial value should equal its committed decimal value exactly",
        "a paper Broker Connection with one Broker Account", "an Instrument with one effective Broker Mapping", "the broker and instrument aggregates are committed and reloaded", "their identities, environment, external references, precision, and effective interval should be unchanged",
        "a Trading Bot has an active configuration and one superseded configuration", "the Trading Bot is committed and reloaded", "its lifecycle state and active configuration identity should be unchanged", "both configuration versions should retain their canonical content and activation history",
        "each Stage 2 aggregate has a deterministic strongly typed identity", "the aggregates are committed and reloaded", "each identity should retain its original domain type and canonical value",
        "Stage 2 records have distinct UTC timestamps separated by one millisecond", "the records are committed and reloaded in timestamp order", "every timestamp should retain millisecond precision in UTC", "their chronological order should be unchanged",
        "two application operations load the same version of a Portfolio", "the first operation commits a change", "the second operation commits its stale change", "the second operation should receive an application concurrency conflict", "the first committed Portfolio state should remain unchanged",
        "a transaction will update a Position, record its applied-fill marker, and append ledger entries", "a deterministic failure occurs after the Position write", "the application attempts to commit the transaction", "no Position change, applied-fill marker, or ledger entry from the transaction should persist",
        "equivalent reconciled portfolio state is supplied in different collection orders", "a Decision Snapshot is created from each input", "their canonical UTF-8 content should be byte-identical", "their lowercase SHA-256 content hashes should be equal",
        "a published Decision Snapshot for a reconciled Portfolio and its assigned Trading Bot", "a material Portfolio value changes", "the published Decision Snapshot should remain unchanged", "a new Decision Snapshot should have different canonical content and content hash",
        "a Decision Snapshot contains cash, buying power, reserved capital, positions, risk utilization, cash flows, and freshness", "the snapshot is committed and the application restarts", "the snapshot should retain its exact content, ownership links, reconciliation state, timestamps, schema version, and hash",
        "a Portfolio has a 250.125 USD deposit from source Deposit DEP-100", "the same deposit source is appended again", "the ledger should contain one entry for Deposit DEP-100", "the Portfolio financial state should change only once",
        "a Portfolio ledger contains a 75.25 USD fee from source Fee FEE-100", "the fee is corrected to 70.25 USD", "the original fee entry should remain unchanged", "a compensating entry for 5 USD should reference the original entry", "the ledger history should contain both accounting facts",
        "a new empty SQLite database", "the application applies all Stage 2 migrations", "the Stage 2 schema and migration history should be present", "applying the migrations again should make no schema change", "the empty SQLite fixture representing the released Stage 1 schema", "no fixture data should be lost",
        "a Portfolio has retained Position, ledger, and Decision Snapshot history", "deletion of a referenced Portfolio is attempted", "the deletion should be rejected", "all financial and audit history should remain unchanged",
        "a Portfolio is assigned to an active Trading Bot", "another active Trading Bot is assigned to the same Portfolio", "the assignment should be rejected with an ownership conflict", "the original assignment should remain unchanged",
        "a Broker Account owns an active Portfolio", "another active Portfolio is associated with the same Broker Account", "the association should be rejected with an ownership conflict", "the original association should remain unchanged",
        "persisted Stage 2 aggregate state", "the application loads state through repository contracts", "the repositories should return domain aggregate roots", "no repository contract should expose an EF entity, DbSet, or IQueryable",
        "persisted Portfolios, Positions, ledger entries, Broker Accounts, and Decision Snapshots", "the application queries a paged Portfolio projection", "the projection should contain exact domain values in deterministic order", "the persistence change tracker should remain empty",
        "an isolated SQLite database with foreign keys enabled", "repository and transaction scenarios execute", "they should use the real SQLite provider", "no scenario should use the EF in-memory provider"
    };
}

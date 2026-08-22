using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Trading.Data.Tests.Migrations;

[TestFixture, Category("Stage5Migrations"), Category("ProposalPersistence")]
internal sealed class Stage5MigrationTests
{
    private const string Bot = "01EEEEEEEEEEEEEEEEEEEEEEEE";
    private const string Request = "01RQEEEEEEEEEEEEEEEEEEEEEE";
    private const string ResearchRun = "01RNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Report = "01RPEEEEEEEEEEEEEEEEEEEEEE";
    private const string Proposal = "01PPEEEEEEEEEEEEEEEEEEEEEE";
    private static string Hash(char c) => new(c, 64);
    private static readonly string[] Tables = ["hypotheses", "hypothesis_versions", "hypothesis_evidence_reports", "hypothesis_test_results", "trade_proposals", "trade_proposal_evidence_reports", "guardrail_evaluations", "proposal_approvals", "capital_reservations"];

    [Test]
    public async Task FreshAndCompletedStageFourUpgradeHaveEquivalentSchemaAndRetainHistory()
    {
        await using var fresh = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(fresh.Context).InitializeAsync(); var schema = await SchemaAsync(fresh.Context);
        await using var upgraded = await TemporarySqliteDatabase.CreateAsync(); await upgraded.Context.Database.MigrateAsync("20260820164929_AddStage4ResearchPersistence"); await SeedStageFourAsync(upgraded.Context); var before = await ScalarAsync<string>(upgraded.Context, "SELECT id||'|'||content_hash FROM research_reports"); await new DatabaseInitializer(upgraded.Context).InitializeAsync();
        Assert.Multiple(async () => { Assert.That(await SchemaAsync(upgraded.Context), Is.EqualTo(schema)); Assert.That(await ScalarAsync<string>(upgraded.Context, "SELECT id||'|'||content_hash FROM research_reports"), Is.EqualTo(before)); Assert.That(await ScalarAsync<long>(upgraded.Context, "SELECT COUNT(*) FROM research_report_sources"), Is.EqualTo(1)); Assert.That(await ScalarAsync<long>(upgraded.Context, "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(11)); Assert.That(await ScalarAsync<string>(upgraded.Context, "SELECT value FROM schema_metadata WHERE key='application_data_format_version'"), Is.EqualTo("6")); });
    }

    [Test]
    public async Task StageFiveSchemaHasRestrictionsIndexesAndNoModelDrift()
    {
        await using var db = await SeedStageFiveAsync(); var objects = await ObjectsAsync(db.Context);
        Assert.Multiple(() => { foreach (var table in Tables) Assert.That(objects, Does.Contain("table|" + table)); Assert.That(db.Context.Database.HasPendingModelChanges(), Is.False); Assert.That(objects, Does.Contain("index|IX_trade_proposals_idempotency_key")); Assert.That(objects, Does.Contain("index|IX_guardrail_evaluations_trade_proposal_id_evaluation_sequence")); Assert.That(objects, Does.Contain("index|IX_capital_reservations_trade_proposal_id")); Assert.That(objects, Does.Contain("trigger|trade_proposals_content_immutable")); Assert.That(objects, Does.Contain("trigger|proposal_approvals_immutable")); Assert.That(objects, Does.Contain("trigger|trg_guardrail_evaluations_immutable_update")); Assert.That(objects, Does.Contain("trigger|trg_guardrail_evaluations_immutable_delete")); });
        foreach (var table in Tables) Assert.That(await StringsAsync(db.Context, $"SELECT on_delete FROM pragma_foreign_key_list('{table}')"), Is.All.EqualTo("RESTRICT"));
    }

    [Test]
    public async Task SqliteEnforcesUniqueCanonicalAndValidGovernanceFacts()
    {
        await using var db = await SeedStageFiveAsync();
        await ExecuteAsync(db.Context, $"INSERT INTO capital_reservations VALUES ('active1','portfolio','{Proposal}',NULL,'1.00','USD','Active',1,3,NULL,NULL,1)");
        Assert.Multiple(() =>
        {
            Assert.That(async () => await ExecuteAsync(db.Context, ProposalSql("proposal2", "idem")), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO guardrail_evaluations (id,trade_proposal_id,evaluation_sequence,evaluation_stage,policy_version,outcome,state_snapshot_id,rule_results_json,evaluated_at,content_hash) VALUES ('eval2','{Proposal}',1,'Initial','p','Passed','snapshot','{{}}',2,'{Hash('z')}')"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO capital_reservations VALUES ('active2','portfolio','{Proposal}',NULL,'1.00','USD','Active',1,3,NULL,NULL,1)"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, "INSERT INTO hypothesis_versions VALUES ('hv2','hypothesis',2,1,'{}','ABC',1,NULL)"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO proposal_approvals VALUES ('approval2','{Proposal}','Maybe','User','u',NULL,2,1,'snapshot')"), Throws.TypeOf<SqliteException>());
        });
    }

    [Test]
    public async Task FrozenAndAuditFactsAreImmutableAndDeletesAreRestricted()
    {
        await using var db = await SeedStageFiveAsync();
        Assert.Multiple(() => { Assert.That(async () => await ExecuteAsync(db.Context, "UPDATE hypothesis_versions SET specification_json='changed' WHERE id='hv'"), Throws.TypeOf<SqliteException>()); Assert.That(async () => await ExecuteAsync(db.Context, $"UPDATE trade_proposals SET rationale='changed' WHERE id='{Proposal}'"), Throws.TypeOf<SqliteException>()); Assert.That(async () => await ExecuteAsync(db.Context, "UPDATE guardrail_evaluations SET outcome='Failed' WHERE id='eval'"), Throws.TypeOf<SqliteException>()); Assert.That(async () => await ExecuteAsync(db.Context, "DELETE FROM proposal_approvals WHERE id='approval'"), Throws.TypeOf<SqliteException>()); Assert.That(async () => await ExecuteAsync(db.Context, "UPDATE capital_reservations SET amount='2.00' WHERE id='reserve'"), Throws.TypeOf<SqliteException>()); Assert.That(async () => await ExecuteAsync(db.Context, "DELETE FROM portfolios WHERE id='portfolio'"), Throws.TypeOf<SqliteException>()); });
    }

    [Test]
    public async Task ExactValuesCanonicalPayloadsUtcAndConcurrencyRoundTrip()
    {
        await using var db = await SeedStageFiveAsync(); var reservation = await db.Context.CapitalReservations.SingleAsync(); Assert.Multiple(() => { Assert.That(reservation.Amount, Is.EqualTo("1234567890123456.12345678")); Assert.That(reservation.ExpiresAt, Is.EqualTo(1735689600123)); });
        var proposal = await db.Context.TradeProposals.SingleAsync(); await using var other = new TradingDbContext(new DbContextOptionsBuilder<TradingDbContext>().UseSqlite((SqliteConnection)db.Context.Database.GetDbConnection()).Options); var stale = await other.TradeProposals.SingleAsync(); proposal.Status = "AwaitingHumanApproval"; proposal.Version = 2; await db.Context.SaveChangesAsync(); stale.Status = "Rejected"; stale.Version = 2; Assert.That(async () => await other.SaveChangesAsync(), Throws.TypeOf<DbUpdateConcurrencyException>());
    }

    private static async Task<TemporarySqliteDatabase> SeedStageFiveAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync(); await SeedStageFourAsync(db.Context); await ExecuteAsync(db.Context, $"INSERT INTO hypotheses VALUES ('hypothesis','Quality','Validated',NULL,1,1,1)"); await ExecuteAsync(db.Context, $"INSERT INTO hypothesis_versions VALUES ('hv','hypothesis',1,1,'{{\"schemaVersion\":1}}','{Hash('c')}',1,1)"); await ExecuteAsync(db.Context, "UPDATE hypotheses SET current_version_id='hv' WHERE id='hypothesis'"); await ExecuteAsync(db.Context, $"INSERT INTO hypothesis_evidence_reports VALUES ('hv','{Report}','Supporting')"); await ExecuteAsync(db.Context, $"INSERT INTO hypothesis_test_results VALUES ('test','hv','dataset-v1','code-v1','{Hash('d')}','Completed',1,2,'{{\"schemaVersion\":1}}','{{\"schemaVersion\":1}}','{Hash('e')}')"); await ExecuteAsync(db.Context, ProposalSql(Proposal, "idem")); await ExecuteAsync(db.Context, $"INSERT INTO trade_proposal_evidence_reports VALUES ('{Proposal}','{Report}')"); await ExecuteAsync(db.Context, $"INSERT INTO guardrail_evaluations (id,trade_proposal_id,evaluation_sequence,evaluation_stage,policy_version,outcome,state_snapshot_id,rule_results_json,evaluated_at,content_hash) VALUES ('eval','{Proposal}',1,'Initial','policy-v1','Passed','snapshot','{{\"schemaVersion\":1}}',2,'{Hash('g')}')"); await ExecuteAsync(db.Context, $"INSERT INTO proposal_approvals VALUES ('approval','{Proposal}','Approved','User','user-1','reviewed',2,1,'snapshot')"); await ExecuteAsync(db.Context, $"INSERT INTO capital_reservations VALUES ('reserve','portfolio','{Proposal}',NULL,'1234567890123456.12345678','USD','Released',1,1735689600123,NULL,2,1)"); db.Context.ChangeTracker.Clear(); return db; }
    private static string ProposalSql(string id, string key) => $"INSERT INTO trade_proposals VALUES ('{id}','{Bot}','botrun','portfolio','snapshot','cfg','instrument','DirectTrade','{{\"schemaVersion\":1}}','rationale','hv','Recorded',1,1735689600123,'{key}',1)";
    private static async Task SeedStageFourAsync(TradingDbContext c) { await ExecuteAsync(c, $"INSERT INTO trading_bots VALUES ('{Bot}','Bot','Enabled',NULL,NULL,NULL,NULL,1,1,1)"); await ExecuteAsync(c, $"INSERT INTO trading_bot_configuration_versions VALUES ('cfg','{Bot}',1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','p','{Hash('a')}',1,1,NULL)"); await ExecuteAsync(c, $"UPDATE trading_bots SET active_configuration_version_id='cfg' WHERE id='{Bot}'"); await ExecuteAsync(c, "INSERT INTO instruments VALUES ('instrument','Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',1,1,1)"); await ExecuteAsync(c, $"INSERT INTO portfolios VALUES ('portfolio','P','USD',NULL,'{Bot}','Active','1000.00','{{}}',1,1,1)"); await ExecuteAsync(c, $"INSERT INTO portfolio_decision_snapshots VALUES ('snapshot','portfolio','{Bot}','cfg',1,'Reconciled','{{}}',1,'{{}}','{Hash('b')}',1)"); await ExecuteAsync(c, $"INSERT INTO bot_runs VALUES ('botrun','{Bot}','cfg','snapshot','Completed',NULL,NULL,1,2,'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,'{Hash('f')}')"); await ExecuteAsync(c, $"UPDATE trading_bots SET last_completed_run_id='botrun' WHERE id='{Bot}'"); await ExecuteAsync(c, $"INSERT INTO research_requests VALUES ('{Request}','Instrument','US:AAPL','q','key',1,'Completed','Shared','{Bot}','{{}}','{{}}',1,2,NULL,1,1)"); await ExecuteAsync(c, $"INSERT INTO research_subscriptions VALUES ('sub','{Request}','{Bot}',1,'Delivered',2)"); await ExecuteAsync(c, $"INSERT INTO research_runs VALUES ('{ResearchRun}','{Request}',1,'Completed','{{}}','p','t','r',1,2,NULL,'{{}}',1)"); await ExecuteAsync(c, $"INSERT INTO research_tool_invocations VALUES ('tool','{ResearchRun}',1,'FinishResearch',1,'{{}}','Succeeded',1,2,'{{}}',NULL,NULL,NULL,'{{}}')"); await ExecuteAsync(c, $"INSERT INTO research_reports VALUES ('{Report}','series',1,'{Request}','{ResearchRun}','Instrument','US:AAPL','q','Shared',1,2,NULL,'Published',NULL,'v1','{{}}',NULL,'{Hash('a')}','{{}}')"); await ExecuteAsync(c, $"UPDATE research_requests SET result_report_id='{Report}' WHERE id='{Request}'"); await ExecuteAsync(c, $"INSERT INTO research_report_sources VALUES ('source','{Report}',1,'Filing',NULL,'10-k','Annual',NULL,NULL,1,'{Hash('b')}','{{}}')"); }
    private static async Task<string> SchemaAsync(TradingDbContext c) => string.Join('\n', await StringsAsync(c, "SELECT type||'|'||name||'|'||coalesce(sql,'') FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' AND name <> '__ef_migrations_history' ORDER BY type,name"));
    private static async Task<string[]> ObjectsAsync(TradingDbContext c) => await StringsAsync(c, "SELECT type||'|'||name FROM sqlite_schema WHERE type IN ('table','index','trigger')");
    private static async Task ExecuteAsync(TradingDbContext c, string sql)
    {
        sql = sql.Replace("INSERT INTO trading_bots VALUES", "INSERT INTO trading_bots (id,name,status,active_configuration_version_id,requested_next_run_at,accepted_next_run_at,last_completed_run_id,created_at,updated_at,version) VALUES", StringComparison.Ordinal);
        sql = sql.Replace("INSERT INTO capital_reservations VALUES", "INSERT INTO capital_reservations (id,portfolio_id,trade_proposal_id,order_id,amount,currency,status,created_at,expires_at,consumed_at,released_at,version) VALUES", StringComparison.Ordinal);
        await using var command = c.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
    private static async Task<T> ScalarAsync<T>(TradingDbContext c, string sql) { await using var command = c.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; return (T)(await command.ExecuteScalarAsync())!; }
    private static async Task<string[]> StringsAsync(TradingDbContext c, string sql) { await using var command = c.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; await using var reader = await command.ExecuteReaderAsync(); var values = new List<string>(); while (await reader.ReadAsync()) values.Add(reader.GetString(0)); return [.. values]; }
}

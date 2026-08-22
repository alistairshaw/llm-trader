using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Trading.Data.Tests;

[TestFixture, Category("DurableBrokerWork"), Category("Stage6Migrations")]
internal sealed class DurableBrokerWorkPersistenceTests
{
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Test]
    public async Task ExactInboxAndOutboxEnvelopeFactsRoundTripWithoutOverloading()
    {
        await using var db = await CreateAsync();
        await ExecuteAsync(db.Context, "PRAGMA foreign_keys=OFF");
        await ExecuteAsync(db.Context, $"INSERT INTO outbox_messages (id,order_id,work_kind,idempotency_key,payload_json,payload_hash,correlation_id,status,available_at,created_at,attempt_count,lease_owner,lease_expires_at,last_error,completed_at,version) VALUES ('work-1','order-1','Reconcile','work-key','{{\"schemaVersion\":1}}','{Hash}','correlation-1','Pending',2000,1000,3,NULL,NULL,'bounded error',NULL,4)");
        await ExecuteAsync(db.Context, $"INSERT INTO inbox_messages (id,idempotency_key,correlation_id,received_at,available_at,status,payload_json,payload_hash,attempt_count,lease_owner,lease_expires_at,last_error,completed_at,version) VALUES ('message-1','source-key','correlation-2',3000,4000,'Claimed','{{\"schemaVersion\":1}}','{Hash}',2,'worker-a',5000,NULL,NULL,3)");

        Assert.Multiple(async () =>
        {
            Assert.That(await RowAsync(db.Context, "SELECT id||'|'||order_id||'|'||work_kind||'|'||idempotency_key||'|'||correlation_id||'|'||attempt_count||'|'||available_at||'|'||created_at||'|'||status||'|'||last_error FROM outbox_messages"), Is.EqualTo("work-1|order-1|Reconcile|work-key|correlation-1|3|2000|1000|Pending|bounded error"));
            Assert.That(await RowAsync(db.Context, "SELECT id||'|'||idempotency_key||'|'||correlation_id||'|'||attempt_count||'|'||received_at||'|'||available_at||'|'||status||'|'||lease_owner||'|'||lease_expires_at FROM inbox_messages"), Is.EqualTo("message-1|source-key|correlation-2|2|3000|4000|Claimed|worker-a|5000"));
        });
    }

    [Test]
    public async Task AtomicClaimsRespectAvailabilityAndActiveLeasesAndReclaimExpiry()
    {
        await using var db = await CreateAsync();
        await InsertInboxAsync(db.Context, "eligible", "eligible-key", 1000);
        await InsertInboxAsync(db.Context, "future", "future-key", 9000);

        var first = await ClaimAsync(db.DatabasePath, "worker-a", 2000, 5000);
        var competitor = await ClaimAsync(db.DatabasePath, "worker-b", 2000, 5000);
        var beforeExpiry = await ReclaimAsync(db.Context, "worker-b", 4999, 8000);
        var afterExpiry = await ReclaimAsync(db.Context, "worker-b", 5000, 8000);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("eligible"));
            Assert.That(competitor, Is.Null);
            Assert.That(beforeExpiry, Is.Zero);
            Assert.That(afterExpiry, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RetryAndTerminalFactsRemainBoundedByExplicitLifecycleConstraints()
    {
        await using var db = await CreateAsync();
        await InsertInboxAsync(db.Context, "retry", "retry-key", 1000);
        await ExecuteAsync(db.Context, "UPDATE inbox_messages SET attempt_count=attempt_count+1, available_at=6000, last_error='broker.timeout', version=version+1 WHERE id='retry'");
        Assert.That(await ClaimAsync(db.DatabasePath, "early", 5999, 7000), Is.Null);
        Assert.That(await ClaimAsync(db.DatabasePath, "retry-worker", 6000, 7000), Is.EqualTo("retry"));
        await ExecuteAsync(db.Context, "UPDATE inbox_messages SET status='Failed', lease_owner=NULL, lease_expires_at=NULL, completed_at=7000, last_error='broker.exhausted', version=version+1 WHERE id='retry'");
        Assert.That(await RowAsync(db.Context, "SELECT attempt_count||'|'||status||'|'||completed_at||'|'||last_error FROM inbox_messages WHERE id='retry'"), Is.EqualTo("2|Failed|7000|broker.exhausted"));
    }

    [Test]
    public async Task StableIdentitiesAndCanonicalConstraintsRejectDuplicatesAndInvalidRows()
    {
        await using var db = await CreateAsync();
        await InsertInboxAsync(db.Context, "one", "same-key", 1000);
        Assert.Multiple(() =>
        {
            Assert.That(async () => await InsertInboxAsync(db.Context, "two", "same-key", 1000), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO inbox_messages (id,idempotency_key,correlation_id,received_at,available_at,status,payload_json,payload_hash,attempt_count,version) VALUES ('bad','bad','corr',1000,999,'Pending','{{}}','{Hash}',0,1)"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, "UPDATE inbox_messages SET payload_json='changed' WHERE id='one'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, "UPDATE inbox_messages SET status='Claimed' WHERE id='one'"), Throws.TypeOf<SqliteException>());
        });
    }

    private static async Task<TemporarySqliteDatabase> CreateAsync()
    {
        var db = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(db.Context).InitializeAsync();
        return db;
    }

    private static Task<int> InsertInboxAsync(TradingDbContext context, string id, string key, long availableAt) =>
        ExecuteAsync(context, $"INSERT INTO inbox_messages (id,idempotency_key,correlation_id,received_at,available_at,status,payload_json,payload_hash,attempt_count,version) VALUES ('{id}','{key}','correlation',1000,{availableAt},'Pending','{{\"schemaVersion\":1}}','{Hash}',0,1)");

    private static async Task<string?> ClaimAsync(string databasePath, string owner, long now, long expiresAt)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE inbox_messages SET status='Claimed',lease_owner=$owner,lease_expires_at=$expires,attempt_count=attempt_count+1,version=version+1 WHERE id=(SELECT id FROM inbox_messages WHERE status='Pending' AND available_at<=$now ORDER BY available_at,received_at,id LIMIT 1) AND status='Pending' RETURNING id";
        command.Parameters.AddWithValue("$owner", owner); command.Parameters.AddWithValue("$expires", expiresAt); command.Parameters.AddWithValue("$now", now);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static Task<int> ReclaimAsync(TradingDbContext context, string owner, long now, long expiresAt) =>
        ExecuteAsync(context, $"UPDATE inbox_messages SET lease_owner='{owner}',lease_expires_at={expiresAt},attempt_count=attempt_count+1,version=version+1 WHERE status='Claimed' AND lease_expires_at<={now}");

    private static async Task<int> ExecuteAsync(TradingDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> RowAsync(TradingDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return (string?)await command.ExecuteScalarAsync();
    }
}

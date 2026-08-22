using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Trading.Core.Operations;

namespace Trading.Data.Tests;

[TestFixture, Category("KillSwitch"), Category("Stage7Migrations")]
internal sealed class KillSwitchPersistenceTests
{
    private static readonly DateTimeOffset ChangedAt = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ChangeIsDurableAuditedIdempotentAndOptimisticallyConcurrent()
    {
        await using var database = await CreateAsync();
        var store = new KillSwitchStore(database.Context);
        var scope = new KillSwitchScope(KillSwitchScopeKind.Portfolio, "portfolio-1");
        var command = new KillSwitchChange("switch-1", scope, KillSwitchState.Active, 0, "reconciliation stale",
            "operator-7", "CONFIRM", ChangedAt);

        var applied = await store.ChangeAsync(command, CancellationToken.None);
        database.Context.ChangeTracker.Clear();
        var retry = await store.ChangeAsync(command, CancellationToken.None);
        var conflict = await store.ChangeAsync(command with { IdempotencyKey = "switch-2", State = KillSwitchState.Clear }, CancellationToken.None);
        var history = await store.GetHistoryAsync(scope, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(applied.Status, Is.EqualTo(KillSwitchChangeStatus.Applied));
            Assert.That(retry.Status, Is.EqualTo(KillSwitchChangeStatus.Idempotent));
            Assert.That(conflict.Status, Is.EqualTo(KillSwitchChangeStatus.Conflict));
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].ActorId, Is.EqualTo("operator-7"));
            Assert.That(history[0].PriorState, Is.EqualTo(KillSwitchState.Clear));
            Assert.That(history[0].ResultingState, Is.EqualTo(KillSwitchState.Active));
        });
    }

    [Test]
    public async Task EffectiveStateUsesRestrictivePlatformToBotHierarchyAndSurvivesRestart()
    {
        await using var database = await CreateAsync();
        var store = new KillSwitchStore(database.Context);
        await store.ChangeAsync(new("platform-active", KillSwitchScope.Platform, KillSwitchState.Active, 0,
            "emergency", "operator-1", "CONFIRM", ChangedAt), CancellationToken.None);
        await database.Context.Database.CloseConnectionAsync();

        var options = new DatabaseOptions { DatabasePath = database.DatabasePath };
        await using var restarted = new TradingDbContext(TradingDbContextFactory.CreateOptions(options, TestContext.CurrentContext.TestDirectory));
        await restarted.Database.OpenConnectionAsync();
        var effective = await new KillSwitchStore(restarted).GetEffectiveAsync(
            new("account-1", "portfolio-1", "bot-1"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(effective.IsBlocked, Is.True);
            Assert.That(effective.ReasonCode, Is.EqualTo(KillSwitchReasonCodes.Blocked));
            Assert.That(effective.Source!.Scope, Is.EqualTo(KillSwitchScope.Platform));
        });
    }

    private static async Task<TemporarySqliteDatabase> CreateAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();
        return database;
    }
}

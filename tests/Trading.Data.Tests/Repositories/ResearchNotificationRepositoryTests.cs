using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Research;

namespace Trading.Data.Tests.Repositories;

[Category("ResearchNotifications")]
public sealed class ResearchNotificationRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 22, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task FailureDeliveryAtomicallyPersistsVisibilitySafeOutcomeAndTriggerAndRetryIsIdempotent()
    {
        await using var db = await CreateAsync(); var seeded = await SeedFailedAsync(db.Context, "Failed");
        var repository = new ResearchNotificationRepository(db.Context); var trigger = BotRunTriggerId.New();
        var first = (ResearchNotificationDeliveryResult.Delivered)await repository.DeliverAsync(seeded.subscription, trigger, Now, default);
        db.Context.ChangeTracker.Clear();
        var retry = (ResearchNotificationDeliveryResult.AlreadyDelivered)await repository.DeliverAsync(seeded.subscription, BotRunTriggerId.New(), Now.AddSeconds(1), default);
        var stored = await db.Context.BotRunTriggers.SingleAsync(); var subscription = await db.Context.ResearchSubscriptions.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(first.Notification.Outcome, Is.EqualTo(ResearchTerminalOutcome.Failed));
            Assert.That(first.Notification.ReportId, Is.Null); Assert.That(first.Notification.CorrelationId, Is.EqualTo(seeded.request.ToString()));
            Assert.That(retry.TriggerId, Is.EqualTo(trigger)); Assert.That(retry.Notification.DeliveredAt, Is.EqualTo(Now)); Assert.That(stored.TriggerType, Is.EqualTo("ResearchFailed"));
            Assert.That(stored.SourceId, Is.EqualTo(seeded.subscription.ToString())); Assert.That(stored.Reason, Does.Not.Contain(seeded.otherBot.ToString()));
            Assert.That(subscription.NotificationStatus, Is.EqualTo("Delivered")); Assert.That(subscription.NotifiedAt, Is.EqualTo(Now.ToUnixTimeMilliseconds()));
        });
    }

    [TestCase("TimedOut")]
    [TestCase("BudgetExceeded")]
    [TestCase("Cancelled")]
    public async Task EveryFailureTerminalStateCreatesTheStableFailureTrigger(string status)
    {
        await using var db = await CreateAsync(); var seeded = await SeedFailedAsync(db.Context, status);
        var result = (ResearchNotificationDeliveryResult.Delivered)await new ResearchNotificationRepository(db.Context).DeliverAsync(seeded.subscription, BotRunTriggerId.New(), Now, default);
        Assert.That(result.Notification.Outcome.ToString(), Is.EqualTo(status));
        Assert.That((await db.Context.BotRunTriggers.SingleAsync()).TriggerType, Is.EqualTo("ResearchFailed"));
    }

    [Test]
    public async Task NonTerminalRequestIsDeferredWithoutPartialWrites()
    {
        await using var db = await CreateAsync(); var seeded = await SeedFailedAsync(db.Context, "Running");
        Assert.That(await new ResearchNotificationRepository(db.Context).DeliverAsync(seeded.subscription, BotRunTriggerId.New(), Now, default), Is.TypeOf<ResearchNotificationDeliveryResult.NotTerminal>());
        db.Context.ChangeTracker.Clear(); Assert.That(await db.Context.BotRunTriggers.CountAsync(), Is.Zero);
        Assert.That((await db.Context.ResearchSubscriptions.SingleAsync()).NotificationStatus, Is.EqualTo("Pending"));
    }

    [Test]
    public async Task PendingEnumerationIsBoundedOrderedAndSeparatesSubscribers()
    {
        await using var db = await CreateAsync(); var a = await SeedFailedAsync(db.Context, "Failed");
        var second = ResearchSubscriptionId.New(); db.Context.ResearchSubscriptions.Add(new ResearchSubscriptionEntity { Id = second.ToString(), ResearchRequestId = a.request.ToString(), TradingBotId = a.otherBot.ToString(), SubscribedAt = Now.AddSeconds(1).ToUnixTimeMilliseconds(), NotificationStatus = "Pending" }); await db.Context.SaveChangesAsync();
        var pending = await new ResearchNotificationRepository(db.Context).GetPendingAsync(a.request, 1, default);
        Assert.That(pending, Is.EqualTo(new[] { a.subscription }));
    }

    private static async Task<(ResearchRequestId request, ResearchSubscriptionId subscription, TradingBotId otherBot)> SeedFailedAsync(TradingDbContext context, string status)
    {
        var bot = TradingBotId.New(); var other = TradingBotId.New(); var request = ResearchRequestId.New(); var subscription = ResearchSubscriptionId.New();
        foreach (var id in new[] { bot, other }) context.TradingBots.Add(new TradingBotEntity { Id = id.ToString(), Name = "notification-" + id, Status = "Enabled", CreatedAt = Now.ToUnixTimeMilliseconds(), UpdatedAt = Now.ToUnixTimeMilliseconds(), Version = 1 });
        context.ResearchRequests.Add(new ResearchRequestEntity { Id = request.ToString(), SubjectType = "Instrument", SubjectId = "US:AAPL", Question = "outlook", NormalizedResearchKey = "key-" + request, AsOf = Now.AddDays(-1).ToUnixTimeMilliseconds(), Status = status, Visibility = "Shared", RequestingBotId = bot.ToString(), FreshnessRequirementJson = "{\"maximumAgeTicks\":864000000000,\"retrievedAt\":\"2026-08-20T22:00:00+00:00\",\"sourceAsOf\":\"2026-08-19T22:00:00+00:00\"}", RequestJson = "{\"authorizedSubscribers\":[],\"hasPrivateInputs\":false,\"restrictedGroup\":null}", StartedAt = Now.AddMinutes(-2).ToUnixTimeMilliseconds(), CompletedAt = status == "Running" ? null : Now.AddMinutes(-1).ToUnixTimeMilliseconds(), CreatedAt = Now.AddMinutes(-3).ToUnixTimeMilliseconds(), Version = 1 });
        context.ResearchSubscriptions.Add(new ResearchSubscriptionEntity { Id = subscription.ToString(), ResearchRequestId = request.ToString(), TradingBotId = bot.ToString(), SubscribedAt = Now.AddMinutes(-3).ToUnixTimeMilliseconds(), NotificationStatus = "Pending" }); await context.SaveChangesAsync(); return (request, subscription, other);
    }
    private static async Task<TemporarySqliteDatabase> CreateAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await db.Context.Database.MigrateAsync(); return db; }
}

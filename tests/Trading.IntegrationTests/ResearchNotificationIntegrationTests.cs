using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Data;

namespace Trading.IntegrationTests;

[Category("ResearchNotifications")]
[Category("TriggerCoalescing")]
public sealed class ResearchNotificationIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 22, 30, 0, TimeSpan.Zero);

    [Test]
    public async Task RestartedMultiSubscriberDeliveryCreatesOneIndependentTriggerPerBot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "research-notifications", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "notifications.db"); var options = TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = path }, AppContext.BaseDirectory);
            ResearchRequestId requestId; ResearchSubscriptionId firstSubscription; ResearchSubscriptionId secondSubscription; TradingBotId first; TradingBotId second;
            await using (var seed = new TradingDbContext(options))
            {
                await new DatabaseInitializer(seed).InitializeAsync(); first = await AddBot(seed, "alpha"); second = await AddBot(seed, "beta");
                var request = new ResearchRequest(ResearchRequestId.New(), first, "US:AAPL", "outlook", Now.AddDays(-1), ResearchVisibility.Shared, new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(1)), "shared-outlook", Now.AddMinutes(-5), [second]);
                request.BeginValidation(); request.Queue(); firstSubscription = ResearchSubscriptionId.New(); secondSubscription = ResearchSubscriptionId.New(); request.Subscribe(firstSubscription, first, Now.AddMinutes(-5)); request.Subscribe(secondSubscription, second, Now.AddMinutes(-4));
                var requests = new ResearchRequestRepository(seed); await requests.AddAsync(request, default); var attempt = new ResearchRunAttempt(ResearchRunAttemptId.New(), request.Id, new ResearchVersionPins("scripted", "research", "1", "p", "t", "1"), new ResearchBudget(TimeSpan.FromMinutes(1), 100, new Money(1, Currency.USD), 2, 2, 1000, 1), Now.AddMinutes(-3));
                _ = await requests.TryClaimQueuedAsync(request.Id, new ResearchAttemptClaim(attempt, 1), default); seed.ChangeTracker.Clear(); var running = (await requests.GetAsync(request.Id, default))!; running.Terminate(ResearchTerminalOutcome.Failed, Now.AddMinutes(-1)); Assert.That(await requests.SaveAsync(running, 2, default), Is.TypeOf<PersistenceWriteResult.Succeeded>()); requestId = request.Id;
            }
            await using (var firstHost = new TradingDbContext(options)) _ = await new ResearchNotificationRepository(firstHost).DeliverAsync(firstSubscription, BotRunTriggerId.New(), Now, default);
            await using (var restarted = new TradingDbContext(options))
            {
                var repository = new ResearchNotificationRepository(restarted); _ = await repository.DeliverAsync(firstSubscription, BotRunTriggerId.New(), Now, default); _ = await repository.DeliverAsync(secondSubscription, BotRunTriggerId.New(), Now, default);
                Assert.Multiple(() => { Assert.That(restarted.Set<BotRunTriggerEntity>().Count(), Is.EqualTo(2)); Assert.That(restarted.Set<ResearchSubscriptionEntity>().Count(x => x.NotificationStatus == "Delivered"), Is.EqualTo(2)); });
                Assert.That(await repository.GetPendingAsync(requestId, 10, default), Is.Empty);
                Assert.That(await new BotRunTriggerRepository(restarted).GetPendingAsync(first, default), Has.Count.EqualTo(1)); Assert.That(await new BotRunTriggerRepository(restarted).GetPendingAsync(second, default), Has.Count.EqualTo(1));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<TradingBotId> AddBot(TradingDbContext context, string name)
    {
        var bot = new TradingBot(TradingBotId.New(), name, Now); Assert.That(await new TradingBotRepository(context).AddAsync(bot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>()); return bot.Id;
    }
}

using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Research.Contracts;

namespace Trading.Research.Tests;

[Category("ResearchNotifications")]
public sealed class ResearchNotificationDeliveryServiceTests
{
    [Test]
    public async Task BatchRetriesConflictsBoundedlyAndContainsSubscriberFailures()
    {
        var request = ResearchRequestId.New(); var first = ResearchSubscriptionId.New(); var second = ResearchSubscriptionId.New();
        var repository = new ScriptedRepository(first, second); var service = new ResearchNotificationDeliveryService(repository, new Ids(), new Clock());
        var result = await service.DeliverPendingAsync(request, 10, 2, default);
        Assert.Multiple(() => { Assert.That(result.Delivered, Is.EqualTo(1)); Assert.That(result.Deferred, Is.EqualTo(1)); Assert.That(repository.Calls[first], Is.EqualTo(2)); Assert.That(repository.Calls[second], Is.EqualTo(2)); });
    }

    private sealed class ScriptedRepository(params ResearchSubscriptionId[] subscriptions) : IResearchNotificationRepository
    {
        public Dictionary<ResearchSubscriptionId, int> Calls { get; } = [];
        public Task<IReadOnlyList<ResearchSubscriptionId>> GetPendingAsync(ResearchRequestId requestId, int limit, CancellationToken token) => Task.FromResult<IReadOnlyList<ResearchSubscriptionId>>(subscriptions);
        public Task<ResearchNotificationDeliveryResult> DeliverAsync(ResearchSubscriptionId id, BotRunTriggerId trigger, DateTimeOffset at, CancellationToken token)
        {
            Calls[id] = Calls.GetValueOrDefault(id) + 1;
            if (id == subscriptions[0] && Calls[id] == 2) return Task.FromResult<ResearchNotificationDeliveryResult>(new ResearchNotificationDeliveryResult.Delivered(null!, trigger));
            return Task.FromResult<ResearchNotificationDeliveryResult>(new ResearchNotificationDeliveryResult.ConcurrencyConflict());
        }
    }
    private sealed class Ids : IResearchNotificationIdentifierSource { public BotRunTriggerId NewTriggerId() => BotRunTriggerId.New(); }
    private sealed class Clock : IResearchClock { public DateTimeOffset UtcNow => new(2026, 8, 20, 22, 0, 0, TimeSpan.Zero); }
}

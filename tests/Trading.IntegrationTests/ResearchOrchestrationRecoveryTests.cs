using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Data;

namespace Trading.IntegrationTests;

[Category("ResearchOrchestration")]
[Category("ResearchRecovery")]
public sealed class ResearchOrchestrationRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 23, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task RestartRetainsAbandonedAuditAndClaimsExactlyOneFreshAttempt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "research-recovery", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var options = TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = Path.Combine(directory, "recovery.db") }, AppContext.BaseDirectory);
            ResearchRequestId requestId; ResearchRunAttemptId abandoned;
            await using (var firstHost = new TradingDbContext(options))
            {
                await new DatabaseInitializer(firstHost).InitializeAsync(); var bot = new TradingBot(TradingBotId.New(), "research-owner", Now);
                await new TradingBotRepository(firstHost).AddAsync(bot, default);
                var request = new ResearchRequest(ResearchRequestId.New(), bot.Id, "US:AAPL", "Five-year outlook?", Now.AddDays(-1), ResearchVisibility.Shared,
                    new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(1)), "key", Now); request.BeginValidation(); request.Queue();
                await new ResearchRequestRepository(firstHost).AddAsync(request, default); firstHost.ChangeTracker.Clear();
                var work = await new ResearchOrchestrationRepository(firstHost).TryClaimAsync(request.Id, Attempt(request.Id, Now), default);
                requestId = request.Id; abandoned = work!.Attempt.Id;
                await new ResearchRunAttemptRepository(firstHost).AppendToolAuditAsync(new("partial", abandoned, 1, "SearchWeb", 1, "{}", "Succeeded", Now, Now, "{}", null, null, "{}"), default);
            }
            await using (var restarted = new TradingDbContext(options))
            {
                var repository = new ResearchOrchestrationRepository(restarted);
                Assert.That(await repository.GetOrphanedAsync(Now.AddMinutes(11), 10, default), Is.EqualTo(new[] { abandoned }));
                Assert.That(await repository.RecoverAndRequeueAsync(abandoned, Now.AddMinutes(11), "research.recovery.expired_lease", default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                restarted.ChangeTracker.Clear(); var next = await repository.TryClaimAsync(requestId, Attempt(requestId, Now.AddMinutes(12)), default);
                Assert.Multiple(() => { Assert.That(next!.AttemptNumber, Is.EqualTo(2)); Assert.That(next.Attempt.Id, Is.Not.EqualTo(abandoned)); Assert.That(restarted.Set<ResearchToolInvocationEntity>().Count(), Is.EqualTo(1)); Assert.That(restarted.Set<ResearchRunEntity>().Count(), Is.EqualTo(2)); });
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static ResearchRunAttempt Attempt(ResearchRequestId requestId, DateTimeOffset at) => new(ResearchRunAttemptId.New(), requestId,
        new("scripted", "research", "1", "prompt-v1", "tools-v1", "1"), new(TimeSpan.FromMinutes(5), 1000, new Money(1, Currency.USD), 10, 5, 10000, 2), at);
}

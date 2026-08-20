using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;

namespace Trading.Data.Tests.Repositories;

[Category("ResearchOrchestration")]
[Category("ResearchRecovery")]
public sealed class ResearchOrchestrationRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 22, 0, 0, TimeSpan.Zero);
    private static readonly string[] RecoveredThenRunning = ["Failed", "Running"];

    [Test]
    public async Task ClaimIsAtomicAttemptNumbersIncreaseAndRecoveryRetainsPriorAttempt()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync(); await database.Context.Database.MigrateAsync();
        var bot = TradingBotId.New(); database.Context.TradingBots.Add(new TradingBotEntity { Id = bot.ToString(), Name = "research-owner", Status = "Enabled", CreatedAt = Now.ToUnixTimeMilliseconds(), UpdatedAt = Now.ToUnixTimeMilliseconds(), Version = 1 }); await database.Context.SaveChangesAsync();
        var request = new ResearchRequest(ResearchRequestId.New(), bot, "US:AAPL", "Five-year outlook?", Now.AddDays(-1), ResearchVisibility.Shared,
            new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7)), "key", Now); request.BeginValidation(); request.Queue();
        await new ResearchRequestRepository(database.Context).AddAsync(request, default); database.Context.ChangeTracker.Clear();
        var repository = new ResearchOrchestrationRepository(database.Context); var first = Attempt(request.Id, Now);
        var claim = await repository.TryClaimAsync(request.Id, first, default);
        Assert.Multiple(() => { Assert.That(claim, Is.Not.Null); Assert.That(claim!.AttemptNumber, Is.EqualTo(1)); Assert.That(claim.Attempt.Status, Is.EqualTo(ResearchRunAttemptStatus.Running)); });
        Assert.That(await repository.TryClaimAsync(request.Id, Attempt(request.Id, Now), default), Is.Null);
        Assert.That(await repository.RecoverAndRequeueAsync(first.Id, Now.AddMinutes(11), "research.recovery.expired_lease", default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear(); var second = await repository.TryClaimAsync(request.Id, Attempt(request.Id, Now.AddMinutes(12)), default);
        Assert.That(second!.AttemptNumber, Is.EqualTo(2));
        Assert.That((await database.Context.ResearchRuns.AsNoTracking().OrderBy(x => x.AttemptNumber).Select(x => x.Status).ToListAsync()), Is.EqualTo(RecoveredThenRunning));
    }

    private static ResearchRunAttempt Attempt(ResearchRequestId id, DateTimeOffset at) => new(ResearchRunAttemptId.New(), id,
        new("scripted", "research", "1", "prompt-v1", "tools-v1", "1"), new(TimeSpan.FromMinutes(5), 1000, new Money(1, Currency.USD), 10, 5, 10000, 2), at);
}

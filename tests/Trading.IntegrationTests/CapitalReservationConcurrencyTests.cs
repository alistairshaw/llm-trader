using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Proposals;
using Trading.Data;

namespace Trading.IntegrationTests;

[TestFixture, Category("CapitalConcurrency"), Category("ProposalGovernance")]
public sealed class CapitalReservationConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 21, 0, 0, TimeSpan.Zero);
    private const string Bot = "01EEEEEEEEEEEEEEEEEEEEEEEE";
    private const string Run = "01BREEEEEEEEEEEEEEEEEEEEEE";
    private const string Portfolio = "01PFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Snapshot = "01PSEEEEEEEEEEEEEEEEEEEEEE";
    private const string Configuration = "01CFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Instrument = "01MNEEEEEEEEEEEEEEEEEEEEEE";

    [Test]
    public async Task ConcurrentWritersCannotOverReserveSamePortfolioAndRestartSeesWinner()
    {
        var directory = Path.Combine(Path.GetTempPath(), "capital-concurrency", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "capital.db");
        try
        {
            TradeProposal first; TradeProposal second;
            await using (var setup = Open(path))
            {
                await new DatabaseInitializer(setup).InitializeAsync(); await SeedParents(setup);
                first = Proposal(); second = Proposal(); var repository = new TradeProposalRepository(setup);
                await repository.RecordAsync(first, "capital-concurrency-1", default);
                await repository.RecordAsync(second, "capital-concurrency-2", default);
                Approve(first); await repository.SaveAsync(first, 1, default); setup.ChangeTracker.Clear();
                Approve(second); await repository.SaveAsync(second, 1, default); setup.ChangeTracker.Clear();
            }

            await using var firstContext = Open(path); await using var secondContext = Open(path);
            var firstTask = new AtomicCapitalReservationRepository(firstContext).TryReserveAsync(Request(first), default);
            var secondTask = new AtomicCapitalReservationRepository(secondContext).TryReserveAsync(Request(second), default);
            var outcomes = await Task.WhenAll(firstTask, secondTask);

            Assert.That(outcomes.Count(x => x is AtomicCapitalReservationWriteResult.Reserved), Is.EqualTo(1));
            Assert.That(outcomes.Count(x => x is AtomicCapitalReservationWriteResult.Rejected
            { Code: ProposalGovernanceCodes.InsufficientCapital } or AtomicCapitalReservationWriteResult.Contention), Is.EqualTo(1));
            await using var restarted = Open(path);
            var active = await new CapitalReservationRepository(restarted)
                .GetActiveForPortfolioAsync(PortfolioId.Parse(Portfolio), Now.AddMinutes(4), default);
            Assert.Multiple(() =>
            {
                Assert.That(active, Has.Count.EqualTo(1));
                Assert.That(active.Single().Amount, Is.EqualTo(new Money(700, Currency.USD)));
                Assert.That(active.Single().PortfolioId, Is.EqualTo(PortfolioId.Parse(Portfolio)));
            });
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static AtomicCapitalReservationRequest Request(TradeProposal proposal) => new(
        new CapitalReservation(CapitalReservationId.New(), proposal, new Money(700, Currency.USD),
            Now.AddMinutes(3), Now.AddMinutes(20)), proposal.TradingBotId, proposal.ContentVersion,
        new FreshStateReference(proposal.PortfolioSnapshotId, Now, Hash('s')),
        new Money(1000, Currency.USD), Now.AddMinutes(3));

    private static void Approve(TradeProposal proposal)
    {
        proposal.StartValidation(Now.AddMinutes(1)); proposal.RequireHumanApproval(Now.AddMinutes(1));
        proposal.Approve(ProposalApprovalId.New(), new DecisionActor(ApprovalActorType.User, "operator"), null,
            Now.AddMinutes(2), proposal.ContentVersion,
            new FreshStateReference(proposal.PortfolioSnapshotId, Now, Hash('s')));
    }

    private static TradeProposal Proposal() => new(TradeProposalId.New(), TradingBotId.Parse(Bot), BotRunId.Parse(Run),
        PortfolioId.Parse(Portfolio), TradingBotConfigurationVersionId.Parse(Configuration),
        PortfolioDecisionSnapshotId.Parse(Snapshot), InstrumentId.Parse(Instrument),
        new DirectTradeAction(TradeSide.Buy, new Quantity(7, "shares"), ProposedOrderType.Limit,
            new Price(100, Currency.USD), ProposedTimeInForce.Day), "concurrent proposal",
        new ProposalContentVersion(1, Hash('p')), null, [], Now, Now.AddHours(1));

    private static async Task SeedParents(TradingDbContext context)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bots (id,name,status,created_at,updated_at,version) VALUES ({Bot},'Bot','Enabled',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bot_configuration_versions VALUES ({Configuration},{Bot},1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','p',{Hash('c')},{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},NULL)");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE trading_bots SET active_configuration_version_id={Configuration} WHERE id={Bot}");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO instruments VALUES ({Instrument},'Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolios VALUES ({Portfolio},'P','USD',NULL,{Bot},'Active','1000','{{}}',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolio_decision_snapshots VALUES ({Snapshot},{Portfolio},{Bot},{Configuration},{Now.ToUnixTimeMilliseconds()},'Reconciled','{{}}',1,'{{}}',{Hash('s')},{Now.ToUnixTimeMilliseconds()})");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO bot_runs VALUES ({Run},{Bot},{Configuration},{Snapshot},'Completed',NULL,NULL,{Now.ToUnixTimeMilliseconds()},{Now.AddMinutes(1).ToUnixTimeMilliseconds()},'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,{Hash('r')})");
    }

    private static TradingDbContext Open(string path) => new(TradingDbContextFactory.CreateOptions(
        new DatabaseOptions { DatabasePath = path }, AppContext.BaseDirectory));
    private static string Hash(char value) => new(value, 64);
}

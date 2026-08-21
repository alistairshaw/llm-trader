using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Data;
using Trading.TestInfrastructure;

namespace Trading.IntegrationTests;

[Category("Stage2")]
public sealed class RestartSafePortfolioWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 16, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task CompletePortfolioStateSurvivesAServiceProviderRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "trading-integration-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "workflow.db");
        try
        {
            var ids = new WorkflowIds();
            string? publishedSnapshotHash = null;
            await using (var first = CreateContext(path))
            {
                await new DatabaseInitializer(first).InitializeAsync();
                var connection = new BrokerConnection(ids.Connection, "simulated", "Paper", BrokerEnvironment.Paper, "secret:paper", ["orders"], Now);
                connection.Enable();
                var account = new BrokerAccount(ids.Account, ids.Connection, "PAPER-100", "Paper Account", "Cash", Currency.USD, ["orders"], Now.AddMilliseconds(1));
                account.Reconcile(Now.AddMilliseconds(2));
                var instrument = new Instrument(ids.Instrument, InstrumentType.Equity, "ACME", "Acme", Currency.USD, "NYSE", 8, 8, Now.AddMilliseconds(3));
                instrument.AddBrokerMapping(ids.Mapping, ids.Connection, "ACME.N", "ACME", "NYSE", Now.AddMilliseconds(4));
                var bot = new TradingBot(ids.Bot, "Paper Bot", Now.AddMilliseconds(5));
                var configuration = bot.AddConfiguration(ids.Configuration,
                    new InvestmentMandate("growth", TimeSpan.FromDays(365), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])),
                    new RiskPolicy([new RiskLimit("position", 10, "percent")]), new ToolPolicy([new ToolAllowance("quotes", 2)]),
                    new RunBudget(TimeSpan.FromMinutes(1), 100, new Money(1, Currency.USD), 2, 1, 1),
                    new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(1)), ExecutionMode.PaperTrading,
                    new ModelConfiguration("scripted", "stage2", 0, 100), "stage2-v1", Now.AddMilliseconds(5));
                bot.ActivateConfiguration(configuration.Id, Now.AddMilliseconds(6));
                var portfolio = new Portfolio(ids.Portfolio, "Paper Portfolio", Currency.USD, new Money(10000.125m, Currency.USD), 5m, Now.AddMilliseconds(5));
                portfolio.AssociateBrokerAccount(ids.Account);
                portfolio.AssignTradingBot(ids.Bot);

                Assert.That(await new BrokerConnectionRepository(first).AddAsync(connection, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new BrokerAccountRepository(first).AddAsync(account, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new InstrumentRepository(first).AddAsync(instrument, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new TradingBotRepository(first).AddAsync(bot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new PortfolioRepository(first).AddAsync(portfolio, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());

                var position = new Position(ids.Position, ids.Portfolio, ids.Instrument, "shares", Currency.USD, Now.AddMilliseconds(6));
                position.ApplyChange(12.34567890m, new Money(123.45678901m, Currency.USD), Money.Zero(Currency.USD), PositionChangeSource.Execution, "FILL-100", Now.AddMilliseconds(7));
                Assert.That(await new PositionRepository(first).AddAsync(position, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new PortfolioLedgerRepository(first).AppendAsync(new PortfolioLedgerEntry(ids.Ledger, ids.Portfolio, PortfolioLedgerEntryType.Deposit, new Money(10000.125m, Currency.USD), null, null, Now.AddMilliseconds(8), LedgerSourceType.BrokerEvent, "DEP-100"), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                var snapshot = new PortfolioDecisionSnapshot(ids.Snapshot, ids.Portfolio, ids.Bot, ids.Configuration, Now.AddMilliseconds(9),
                    ReconciliationStatus.Reconciled, new Money(10000.125m, Currency.USD), new Money(8475.96712499m, Currency.USD),
                    Money.Zero(Currency.USD), [new PositionSnapshot(ids.Instrument, 12.34567890m, new Money(1524.15787501m, Currency.USD))], [],
                    15.24157875m, [new CashFlowSnapshot(new Money(10000.125m, Currency.USD), Now.AddMilliseconds(8), "DEP-100")],
                    new DataFreshness(Now, Now.AddMilliseconds(9), TimeSpan.FromMinutes(5)), Now.AddMilliseconds(9));
                Assert.That(await new PortfolioDecisionSnapshotRepository(first).PublishAsync(snapshot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                publishedSnapshotHash = snapshot.ContentHash;
            }

            await using (var second = CreateContext(path))
            {
                var portfolio = await new PortfolioRepository(second).GetAsync(ids.Portfolio, default);
                var position = await new PositionRepository(second).GetAsync(ids.Position, default);
                var instrument = await new InstrumentRepository(second).GetAsync(ids.Instrument, default);
                var bot = await new TradingBotRepository(second).GetAsync(ids.Bot, default);
                var snapshot = await new PortfolioDecisionSnapshotRepository(second).GetAsync(ids.Snapshot, default);
                var ledger = await new PortfolioQueries(second).GetLedgerAsync(new(ids.Portfolio), new(0, 10), default);
                var projection = await new PortfolioQueries(second).GetPortfoliosAsync(new(null, null), new(0, 10), default);
                Assert.Multiple(() =>
                {
                    Assert.That(portfolio!.CapitalAllocation, Is.EqualTo(new Money(10000.125m, Currency.USD)));
                    Assert.That(portfolio.BrokerAccountId, Is.EqualTo(ids.Account));
                    Assert.That(position!.Quantity, Is.EqualTo(12.34567890m));
                    Assert.That(position.AverageCost, Is.EqualTo(new Money(123.45678901m, Currency.USD)));
                    Assert.That(instrument!.BrokerMappings.Single().Id, Is.EqualTo(ids.Mapping));
                    Assert.That(bot!.ActiveConfigurationVersionId, Is.EqualTo(ids.Configuration));
                    Assert.That(ledger.Single().SourceId, Is.EqualTo("DEP-100"));
                    Assert.That(snapshot!.ContentHash, Is.EqualTo(publishedSnapshotHash));
                    Assert.That(snapshot.CanonicalContent, Does.Contain("12.3456789"));
                    Assert.That(projection.Single().Id, Is.EqualTo(ids.Portfolio));
                    Assert.That(second.ChangeTracker.Entries(), Is.Empty);
                });
                TestContext.Progress.WriteLine($"Stage2 demonstration database={path}; migration=20260819154728_InitialStage2Persistence; portfolio={ids.Portfolio}; snapshot={ids.Snapshot}; hash={snapshot!.ContentHash}");
            }
        }
        catch (Exception exception)
        {
            Assert.Fail($"Stage2 database={path}; migration=InitialStage2Persistence; aggregate=Portfolio; operation=restart-reload: {exception.Message}");
        }
        finally
        {
            SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory, SqliteTestDatabaseCleanup.ConnectionString(path));
        }
    }

    private static TradingDbContext CreateContext(string path) => new TradingDbContext(TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = path }, AppContext.BaseDirectory));

    private sealed class WorkflowIds
    {
        public BrokerConnectionId Connection { get; } = BrokerConnectionId.Parse("01J5QH8M000000000000000001");
        public BrokerAccountId Account { get; } = BrokerAccountId.Parse("01J5QH8M000000000000000002");
        public InstrumentId Instrument { get; } = InstrumentId.Parse("01J5QH8M000000000000000003");
        public InstrumentBrokerMappingId Mapping { get; } = InstrumentBrokerMappingId.Parse("01J5QH8M000000000000000004");
        public PortfolioId Portfolio { get; } = PortfolioId.Parse("01J5QH8M000000000000000005");
        public PositionId Position { get; } = PositionId.Parse("01J5QH8M000000000000000006");
        public PortfolioLedgerEntryId Ledger { get; } = PortfolioLedgerEntryId.Parse("01J5QH8M000000000000000007");
        public TradingBotId Bot { get; } = TradingBotId.Parse("01J5QH8M000000000000000008");
        public TradingBotConfigurationVersionId Configuration { get; } = TradingBotConfigurationVersionId.Parse("01J5QH8M000000000000000009");
        public PortfolioDecisionSnapshotId Snapshot { get; } = PortfolioDecisionSnapshotId.Parse("01J5QH8M00000000000000000A");
    }
}

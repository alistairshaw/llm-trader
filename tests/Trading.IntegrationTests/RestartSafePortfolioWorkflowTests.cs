using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Portfolios;
using Trading.Data;

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
            await using (var first = CreateContext(path))
            {
                await new DatabaseInitializer(first).InitializeAsync();
                var connection = new BrokerConnection(ids.Connection, "simulated", "Paper", BrokerEnvironment.Paper, "secret:paper", ["orders"], Now);
                connection.Enable();
                var account = new BrokerAccount(ids.Account, ids.Connection, "PAPER-100", "Paper Account", "Cash", Currency.USD, ["orders"], Now.AddMilliseconds(1));
                account.Reconcile(Now.AddMilliseconds(2));
                var instrument = new Instrument(ids.Instrument, InstrumentType.Equity, "ACME", "Acme", Currency.USD, "NYSE", 8, 8, Now.AddMilliseconds(3));
                instrument.AddBrokerMapping(ids.Mapping, ids.Connection, "ACME.N", "ACME", "NYSE", Now.AddMilliseconds(4));
                var portfolio = new Portfolio(ids.Portfolio, "Paper Portfolio", Currency.USD, new Money(10000.125m, Currency.USD), 5m, Now.AddMilliseconds(5));
                portfolio.AssociateBrokerAccount(ids.Account);

                Assert.That(await new BrokerConnectionRepository(first).AddAsync(connection, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new BrokerAccountRepository(first).AddAsync(account, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new InstrumentRepository(first).AddAsync(instrument, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new PortfolioRepository(first).AddAsync(portfolio, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());

                var position = new Position(ids.Position, ids.Portfolio, ids.Instrument, "shares", Currency.USD, Now.AddMilliseconds(6));
                position.ApplyChange(12.34567890m, new Money(123.45678901m, Currency.USD), Money.Zero(Currency.USD), PositionChangeSource.Execution, "FILL-100", Now.AddMilliseconds(7));
                Assert.That(await new PositionRepository(first).AddAsync(position, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
                Assert.That(await new PortfolioLedgerRepository(first).AppendAsync(new PortfolioLedgerEntry(ids.Ledger, ids.Portfolio, PortfolioLedgerEntryType.Deposit, new Money(10000.125m, Currency.USD), null, null, Now.AddMilliseconds(8), LedgerSourceType.BrokerEvent, "DEP-100"), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
            }

            await using (var second = CreateContext(path))
            {
                var portfolio = await new PortfolioRepository(second).GetAsync(ids.Portfolio, default);
                var position = await new PositionRepository(second).GetAsync(ids.Position, default);
                var instrument = await new InstrumentRepository(second).GetAsync(ids.Instrument, default);
                var ledger = await new PortfolioQueries(second).GetLedgerAsync(new(ids.Portfolio), new(0, 10), default);
                Assert.Multiple(() =>
                {
                    Assert.That(portfolio!.CapitalAllocation, Is.EqualTo(new Money(10000.125m, Currency.USD)));
                    Assert.That(portfolio.BrokerAccountId, Is.EqualTo(ids.Account));
                    Assert.That(position!.Quantity, Is.EqualTo(12.34567890m));
                    Assert.That(position.AverageCost, Is.EqualTo(new Money(123.45678901m, Currency.USD)));
                    Assert.That(instrument!.BrokerMappings.Single().Id, Is.EqualTo(ids.Mapping));
                    Assert.That(ledger.Single().SourceId, Is.EqualTo("DEP-100"));
                    Assert.That(second.ChangeTracker.Entries(), Is.Empty);
                });
            }
        }
        catch (Exception exception)
        {
            Assert.Fail($"Stage2 database={path}; migration=InitialStage2Persistence; aggregate=Portfolio; operation=restart-reload: {exception.Message}");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
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
    }
}

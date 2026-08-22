using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Data;
using Trading.TestInfrastructure;

namespace Trading.Data.Tests.Repositories;

[TestFixture, Category("OrderConversionTransaction")]
public sealed class AtomicOrderConversionRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private const string Bot = "01EEEEEEEEEEEEEEEEEEEEEEEE";
    private const string Run = "01BREEEEEEEEEEEEEEEEEEEEEE";
    private const string Portfolio = "01PFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Snapshot = "01PSEEEEEEEEEEEEEEEEEEEEEE";
    private const string Configuration = "01CFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Instrument = "01MNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Connection = "01BCEEEEEEEEEEEEEEEEEEEEEE";
    private const string Account = "01BAEEEEEEEEEEEEEEEEEEEEEE";
    private const string Mapping = "01MMEEEEEEEEEEEEEEEEEEEEEE";

    [Test]
    public async Task CreatesIntentReservationBindingAndSubmitWorkAtomicallyAndRetryReusesOrder()
    {
        await using var fixture = await CreateAsync();
        var request = Request(fixture.Proposal, fixture.Reservation.Id);
        var repository = new AtomicOrderConversionRepository(fixture.Database.Context);

        var first = await repository.TryConvertAsync(request, default);
        fixture.Database.Context.ChangeTracker.Clear();
        var retry = await repository.TryConvertAsync(request with
        {
            OrderId = OrderId.New(),
            WorkItemId = OrderWorkItemId.New(),
            CorrelationId = new("retry-correlation")
        }, default);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.TypeOf<AtomicOrderConversionWriteResult.Created>());
            Assert.That(retry, Is.TypeOf<AtomicOrderConversionWriteResult.AlreadyCreated>());
            Assert.That(fixture.Database.Context.Orders.Count(), Is.EqualTo(1));
            Assert.That(fixture.Database.Context.OutboxMessages.Count(), Is.EqualTo(1));
            Assert.That(fixture.Database.Context.TradeProposals.Single().Status, Is.EqualTo("ConvertedToOrder"));
            Assert.That(fixture.Database.Context.CapitalReservations.Single().OrderId,
                Is.EqualTo(request.OrderId.ToString()));
        });
        var order = fixture.Database.Context.Orders.Single();
        var work = fixture.Database.Context.OutboxMessages.Single();
        Assert.Multiple(() =>
        {
            Assert.That(order.ClientOrderId, Is.EqualTo(request.ClientOrderId.Value));
            Assert.That(order.CapitalReservationId, Is.EqualTo(fixture.Reservation.Id.ToString()));
            Assert.That(order.Quantity, Is.EqualTo("2"));
            Assert.That(order.QuantityUnit, Is.EqualTo("shares"));
            Assert.That(order.Currency, Is.EqualTo("USD"));
            Assert.That(work.OrderId, Is.EqualTo(order.Id));
            Assert.That(work.IdempotencyKey, Is.EqualTo($"submit:{request.ClientOrderId.Value}"));
            Assert.That(work.PayloadJson, Does.Contain(fixture.Proposal.ContentVersion.ContentHash));
            Assert.That(work.PayloadJson, Does.Contain(fixture.Evaluation.Id.ToString()));
            Assert.That(work.PayloadJson, Does.Contain(fixture.Approval.Id.ToString()));
            Assert.That(work.PayloadJson, Does.Contain(Mapping));
        });
    }

    [Test]
    public async Task FailedFreshAuthorizationWritesNothing()
    {
        await using var fixture = await CreateAsync();
        fixture.Database.Context.BrokerAccounts.Single().Status = "Restricted";
        await fixture.Database.Context.SaveChangesAsync();
        fixture.Database.Context.ChangeTracker.Clear();

        var result = await new AtomicOrderConversionRepository(fixture.Database.Context)
            .TryConvertAsync(Request(fixture.Proposal, fixture.Reservation.Id), default);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new AtomicOrderConversionWriteResult.Rejected(
                AtomicOrderConversionCodes.AccountRestricted)));
            Assert.That(fixture.Database.Context.Orders.Count(), Is.Zero);
            Assert.That(fixture.Database.Context.OutboxMessages.Count(), Is.Zero);
            Assert.That(fixture.Database.Context.TradeProposals.Single().Status, Is.EqualTo("Approved"));
            Assert.That(fixture.Database.Context.CapitalReservations.Single().OrderId, Is.Null);
        });
    }

    private static AtomicOrderConversionRequest Request(TradeProposal proposal, CapitalReservationId reservationId) =>
        new(proposal.Id, reservationId, OrderId.New(), OrderWorkItemId.New(), new("conversion-correlation"),
            new ClientOrderIdentity("paper-conversion-stable"), Now.AddMinutes(6));

    private static async Task<Fixture> CreateAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();
        await SeedParentsAsync(database.Context);
        var proposal = new TradeProposal(TradeProposalId.New(), TradingBotId.Parse(Bot), BotRunId.Parse(Run),
            PortfolioId.Parse(Portfolio), TradingBotConfigurationVersionId.Parse(Configuration),
            PortfolioDecisionSnapshotId.Parse(Snapshot), InstrumentId.Parse(Instrument),
            new DirectTradeAction(TradeSide.Buy, new Quantity(2, "shares"), ProposedOrderType.Limit,
                new Price(125, Currency.USD), ProposedTimeInForce.Day), "approved paper order",
            new ProposalContentVersion(1, Hash('p')), null, [], Now, Now.AddHours(2), ExecutionMode.PaperTrading);
        var proposals = new TradeProposalRepository(database.Context);
        await proposals.RecordAsync(proposal, "conversion-proposal", default);
        var policies = new[]
        {
            new GuardrailPolicyReference(GuardrailPolicyLevel.Platform, "platform", "v1"),
            new GuardrailPolicyReference(GuardrailPolicyLevel.Account, Account, "v1"),
            new GuardrailPolicyReference(GuardrailPolicyLevel.Portfolio, Portfolio, "v1"),
            new GuardrailPolicyReference(GuardrailPolicyLevel.TradingBot, Bot, "v1"),
        };
        var fresh = new FreshStateReference(PortfolioDecisionSnapshotId.Parse(Snapshot), Now, Hash('s'));
        proposal.StartValidation(Now.AddMinutes(1));
        proposal.RecordEvaluation(GuardrailEvaluationId.New(), policies, GuardrailOutcome.Passed,
            [new("notional", GuardrailOutcome.Passed, "within policy")], Now.AddMinutes(1), fresh,
            Hash('e'), "guardrail.passed");
        proposal.CompleteValidation(GuardrailOutcome.Passed, Now.AddMinutes(1));
        var approval = proposal.Approve(ProposalApprovalId.New(),
            new DecisionActor(ApprovalActorType.User, "operator"), "approved", Now.AddMinutes(2),
            proposal.ContentVersion, fresh);
        proposal.StartRevalidation(Now.AddMinutes(3));
        var evaluation = proposal.RecordEvaluation(GuardrailEvaluationId.New(), policies,
            GuardrailOutcome.Passed, [new("fresh-state", GuardrailOutcome.Passed, "fresh")],
            Now.AddMinutes(3), fresh, Hash('f'), "guardrail.passed");
        proposal.CompleteValidation(GuardrailOutcome.Passed, Now.AddMinutes(3));
        await proposals.SaveAsync(proposal, 1, default);
        database.Context.ChangeTracker.Clear();
        var reservation = new CapitalReservation(CapitalReservationId.New(), proposal,
            new Money(250, Currency.USD), Now.AddMinutes(4), Now.AddMinutes(30));
        await new CapitalReservationRepository(database.Context).AddAsync(reservation, default);
        database.Context.ChangeTracker.Clear();
        return new Fixture(database, proposal, evaluation, approval, reservation);
    }

    private static async Task SeedParentsAsync(TradingDbContext context)
    {
        var now = Now.ToUnixTimeMilliseconds();
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO broker_connections VALUES ({Connection},'Simulated','Paper','Paper','secret-ref','Enabled','{{}}',{now},{now},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO broker_accounts VALUES ({Account},{Connection},'paper','Paper','Cash','USD','Active',{now},'{{}}',{now},{now},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bots (id,name,status,created_at,updated_at,version) VALUES ({Bot},'Bot','Enabled',{now},{now},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bot_configuration_versions VALUES ({Configuration},{Bot},1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','p',{Hash('c')},{now},{now},NULL)");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE trading_bots SET active_configuration_version_id={Configuration} WHERE id={Bot}");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO instruments VALUES ({Instrument},'Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',{now},{now},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO instrument_broker_mappings VALUES ({Mapping},{Instrument},{Connection},'AAPL','AAPL','NASDAQ',{Now.AddHours(-1).ToUnixTimeMilliseconds()},NULL,'{{}}')");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolios VALUES ({Portfolio},'P','USD',{Account},{Bot},'Active','1000','{{}}',{now},{now},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolio_decision_snapshots VALUES ({Snapshot},{Portfolio},{Bot},{Configuration},{now},'Reconciled','{{}}',1,'{{}}',{Hash('s')},{now})");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO bot_runs VALUES ({Run},{Bot},{Configuration},{Snapshot},'Completed',NULL,NULL,{now},{Now.AddMinutes(1).ToUnixTimeMilliseconds()},'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,{Hash('r')})");
    }

    private static string Hash(char value) => new(value, 64);
    private sealed record Fixture(TemporarySqliteDatabase Database, TradeProposal Proposal,
        GuardrailEvaluation Evaluation, ProposalApproval Approval, CapitalReservation Reservation) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}

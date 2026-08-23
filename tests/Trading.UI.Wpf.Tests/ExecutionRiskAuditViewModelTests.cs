using System.Xml.Linq;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Tests;

[TestFixture, Category("ExecutionRiskAudit")]
public sealed class ExecutionRiskAuditViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 21, 0, 0, TimeSpan.Zero);
    private static readonly int[] ExpectedOffsets = [25, 0];
    private static readonly ExecutionQueryPrincipal Principal = new("operator", false,
        [TradingBotId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA01")], [PortfolioId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA02")],
        [BrokerAccountId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA03")]);

    [Test]
    public async Task ExactFinancialFactsChronologyAndRiskAreDisplayedWithoutDuplicates()
    {
        var order = Order(OrderStatus.Unknown);
        var fill = new FillProjection(FillId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA08"), "exec-1", 2.12500000m,
            12.34567891m, "USD", .12345678m, Now.AddMinutes(1), Now.AddMinutes(2));
        var position = new PositionEffectProjection(PositionId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA09"), 2.125m,
            "shares", 12.34567891m, -.00000001m, "USD", Now.AddMinutes(3));
        var ledger = new LedgerEffectProjection(PortfolioLedgerEntryId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA10"),
            "BrokerFee", -.12345678m, "USD", null, "Fill", fill.Id.ToString(), Now.AddMinutes(3));
        var audit = new ExecutionAuditEvent("broker.reconciliation", "recon-1", Now.AddMinutes(2), "RecoveryRequired",
            order.CorrelationId, "broker.unknown", "Recovered by client identity");
        var source = new Source([order, order], new(order, "paper-order", fill.Quantity, fill.Quantity * fill.Price,
            fill.Fee, "Consumed", 3.5m, [fill, fill], [position, position], [ledger, ledger], [audit, audit]));
        await using var model = new ExecutionRiskAuditViewModel(source, Principal);

        await model.RefreshAsync();
        model.SelectedOrder = model.Orders.Single();
        await model.LoadDetailAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Orders, Has.Count.EqualTo(1));
            Assert.That(model.Fills, Has.Count.EqualTo(1));
            Assert.That(model.Effects, Has.Count.EqualTo(2));
            Assert.That(model.Audit, Has.Count.EqualTo(1));
            Assert.That(model.OrderFinancials, Is.EqualTo("Filled 2.125 shares; gross 26.23456768375 USD; fees 0.12345678 USD"));
            Assert.That(model.Fills.Single().Price, Is.EqualTo("12.34567891 USD"));
            Assert.That(model.Fills.Single().Gross, Is.EqualTo("26.23456768375 USD"));
            Assert.That(model.Effects.First().Effect, Does.Contain("-0.00000001 USD"));
            Assert.That(model.Reservation, Is.EqualTo("Reservation Consumed; remaining 3.5 USD"));
            Assert.That(model.RiskAnnouncement, Does.StartWith("Warning:").And.Contain("unknown").IgnoreCase);
        }
    }

    [Test]
    public async Task ScopePrincipalRiskFilterAndPagingReachTheAuthorizedQuery()
    {
        var source = new Source([], null);
        await using var model = new ExecutionRiskAuditViewModel(source, Principal) { RiskFilter = " Rejected " };
        await model.NextPageAsync();
        await model.PreviousPageAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.Requests.Select(x => x.Principal), Is.All.EqualTo(Principal));
            Assert.That(source.Requests.Select(x => x.Filter.Status), Is.All.EqualTo(OrderStatus.Rejected));
            Assert.That(source.Requests.Select(x => x.Page.Offset), Is.EqualTo(ExpectedOffsets));
        }
    }

    [Test]
    public async Task MissingDetailDoesNotDisclosePriorFinancialFacts()
    {
        var order = Order(OrderStatus.Filled);
        var source = new Source([order], null);
        await using var model = new ExecutionRiskAuditViewModel(source, Principal);
        await model.RefreshAsync(); model.SelectedOrder = order; await model.LoadDetailAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Fills, Is.Empty); Assert.That(model.Effects, Is.Empty); Assert.That(model.Audit, Is.Empty);
            Assert.That(model.OrderFinancials, Is.EqualTo("No Order selected."));
            Assert.That(model.StateText, Does.Contain("unavailable"));
        }
    }

    [Test]
    public void ViewHasStableAccessibleRiskAndFinancialMetadata()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "ExecutionRiskAuditView.xaml"));
        var ids = document.Descendants().SelectMany(x => x.Attributes())
            .Where(x => x.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal)).Select(x => x.Value).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain("ExecutionRisk.Workspace").And.Contain("ExecutionRisk.Fills")
                .And.Contain("ExecutionRisk.Effects").And.Contain("ExecutionRisk.Audit").And.Contain("ExecutionRisk.Reservation")
                .And.Contain("ExecutionRisk.Tab.Orders").And.Contain("ExecutionRisk.Tab.Fills"));
            Assert.That(document.Descendants().Count(x => x.Name.LocalName == "Label" && x.Attribute("Target") is not null), Is.EqualTo(2));
            Assert.That(document.Descendants().Any(x => x.Attributes().Any(a => a.Name.LocalName.EndsWith(".LiveSetting", StringComparison.Ordinal) && a.Value == "Assertive")), Is.True);
            Assert.That(document.Descendants().Any(x => x.Attributes().Any(a => a.Name.LocalName.EndsWith(".HeadingLevel", StringComparison.Ordinal))), Is.True);
            Assert.That(document.Descendants().Where(x => x.Name.LocalName == "DataGrid").All(x => x.Attribute("IsReadOnly")?.Value == "True"), Is.True);
        }
    }

    private static OrderListItem Order(OrderStatus status) => new(OrderId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA04"),
        "paper-client", Principal.TradingBotIds.Single(), Principal.PortfolioIds.Single(), Principal.BrokerAccountIds.Single(),
        TradeProposalId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA05"), InstrumentId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA06"),
        OrderSide.Buy, 10.00000000m, "shares", "USD", status, "correlation-1", Now, null);

    private sealed class Source(IReadOnlyList<OrderListItem> orders, OrderExecutionDetail? detail) : IOrderExecutionQueries
    {
        public List<(ExecutionQueryPrincipal Principal, OrderQueryFilter Filter, ExecutionPageRequest Page)> Requests { get; } = [];
        public Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(ExecutionQueryPrincipal principal, OrderQueryFilter filter,
            ExecutionPageRequest page, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Requests.Add((principal, filter, page)); return Task.FromResult(orders); }
        public Task<OrderExecutionDetail?> GetOrderAsync(ExecutionQueryPrincipal principal, OrderId id, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(detail); }
    }
}

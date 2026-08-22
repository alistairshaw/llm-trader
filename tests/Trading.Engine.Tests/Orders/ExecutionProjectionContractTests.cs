using Trading.Core.Identifiers;
using Trading.Core.Orders;

namespace Trading.Engine.Tests.Orders;

[TestFixture, Category("ExecutionAudit")]
public sealed class ExecutionProjectionContractTests
{
    [Test]
    public void PagesAreBoundedAndFiltersCarryExactExecutionScopes()
    {
        var filter = new OrderQueryFilter(TradingBotId.New(), PortfolioId.New(), BrokerAccountId.New(),
            TradeProposalId.New(), OrderStatus.PartiallyFilled, "Paper", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1));
        Assert.Multiple(() =>
        {
            Assert.That(filter.Environment, Is.EqualTo("Paper"));
            Assert.That(filter.Status, Is.EqualTo(OrderStatus.PartiallyFilled));
            Assert.That(new ExecutionPageRequest(0, ExecutionPageRequest.MaximumSize).Size, Is.EqualTo(100));
            Assert.That(() => new ExecutionPageRequest(-1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ExecutionPageRequest(0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }
}

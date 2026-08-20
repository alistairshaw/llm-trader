using Trading.Core.Identifiers;
using Trading.Core.Proposals;

namespace Trading.Engine.Tests;

[TestFixture, Category("ProposalQueries")]
public sealed class ProposalQueryContractTests
{
    [Test]
    public void PrincipalFreezesAndDeterministicallyOrdersAuthorizationScopes()
    {
        var first = TradingBotId.Parse("01EEEEEEEEEEEEEEEEEEEEEEE1");
        var second = TradingBotId.Parse("01EEEEEEEEEEEEEEEEEEEEEEE2");
        var source = new List<TradingBotId> { second, first, second };
        var principal = new ProposalQueryPrincipal("reviewer", false, source);
        source.Clear();

        Assert.That(principal.TradingBotIds, Is.EqualTo(new[] { first, second }));
        Assert.That(principal.TradingBotIds, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<TradingBotId>>());
    }

    [Test]
    public void QueryBoundaryIsEfFreeAndPaginationIsBounded()
    {
        var exposed = typeof(IProposalQueries).Assembly.GetTypes()
            .Where(x => x.Namespace == typeof(IProposalQueries).Namespace && x.Name.Contains("Proposal", StringComparison.Ordinal))
            .SelectMany(x => x.GetProperties().Select(p => p.PropertyType).Concat(x.GetMethods().Select(m => m.ReturnType)))
            .Select(x => x.FullName ?? string.Empty).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(exposed, Has.None.StartsWith("Microsoft.EntityFrameworkCore"));
            Assert.That(() => new ProposalPageRequest(-1, 10), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ProposalPageRequest(0, ProposalPageRequest.MaximumSize + 1), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }
}

using System.Xml.Linq;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Tests;

[Category("PortfolioBroker")]
public sealed class PortfolioBrokerViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ExactValuesPaperEnvironmentAndUnsafeStatesAreExplicit()
    {
        var source = new Source(new(PortfolioBrokerLoadStatus.Succeeded, [View("Disabled", "Pending", Now.AddHours(-1))]));
        await using var model = new PortfolioBrokerViewModel(source, new FixedTimeProvider(Now));

        await model.RefreshAsync();

        var row = model.Items.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Capital, Is.EqualTo("1234.5678 USD"));
            Assert.That(row.Ledger, Is.EqualTo("20.125 USD"));
            Assert.That(row.Environment, Is.EqualTo("PAPER — simulated broker environment"));
            Assert.That(row.IsDisconnected, Is.True);
            Assert.That(row.IsUncertain, Is.True);
            Assert.That(row.IsStale, Is.True);
            Assert.That(row.AutomationStatus, Does.Contain("DISCONNECTED").And.Contain("RECONCILIATION UNCERTAIN").And.Contain("STALE DATA"));
            Assert.That(model.SafetyAnnouncement, Does.StartWith("Warning:"));
        }
    }

    [Test]
    public async Task DeniedAndEmptyResultsDoNotDiscloseRows()
    {
        await using var denied = new PortfolioBrokerViewModel(new Source(new(PortfolioBrokerLoadStatus.Denied, [View("Enabled", "Reconciled", Now)])));
        await denied.RefreshAsync();
        await using var empty = new PortfolioBrokerViewModel(new Source(new(PortfolioBrokerLoadStatus.Succeeded, [])));
        await empty.RefreshAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(denied.Items, Is.Empty);
            Assert.That(denied.IsDenied, Is.True);
            Assert.That(denied.StateText, Does.Contain("unavailable"));
            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.StateText, Does.Contain("No authorized portfolios"));
        }
    }

    [Test]
    public async Task FiltersAndPagingArePassedDeterministically()
    {
        var source = new Source(new(PortfolioBrokerLoadStatus.Succeeded, []));
        await using var model = new PortfolioBrokerViewModel(source) { Search = " alpha ", StatusFilter = " Active " };
        await model.NextPageAsync();
        await model.PreviousPageAsync();
        Assert.That(source.Requests, Is.EqualTo(new[]
        {
            ("alpha", "Active", 25, 25),
            ("alpha", "Active", 0, 25),
        }));
    }

    [Test]
    public void ViewExposesStableIdsNamesHeadingLabelsAndAssertiveSafetyState()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "PortfolioBrokerView.xaml"));
        var attributes = document.Descendants().SelectMany(x => x.Attributes()).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(attributes.Count(x => x.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(7));
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".HeadingLevel", StringComparison.Ordinal)), Is.True);
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".LiveSetting", StringComparison.Ordinal) && x.Value == "Assertive"), Is.True);
            Assert.That(document.Descendants().Count(x => x.Name.LocalName == "Label" && x.Attribute("Target") is not null), Is.EqualTo(2));
        }
    }

    private static OperatorPortfolioBrokerView View(string connection, string reconciliation, DateTimeOffset updated) =>
        new(PortfolioId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA01"), "Alpha", TradingBotId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA02"),
            BrokerAccountId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA03"), BrokerConnectionId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA04"),
            "USD", 1234.56780000m, 2, 3.75000000m, 20.12500000m, "Active", "Paper A", "Enabled",
            "Fixture", connection, "Paper", ["Cancel", "Submit"], 4, reconciliation, Now.AddMinutes(-5), updated);

    private sealed class Source(PortfolioBrokerLoadResult result) : IPortfolioBrokerViewSource
    {
        public List<(string?, string?, int, int)> Requests { get; } = [];
        public Task<PortfolioBrokerLoadResult> LoadAsync(string? search, string? status, int offset, int size, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Requests.Add((search, status, offset, size)); return Task.FromResult(result); }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
}

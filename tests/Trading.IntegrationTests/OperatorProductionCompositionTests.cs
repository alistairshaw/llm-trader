using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trading.Core.Identifiers;
using Trading.Engine.Operators;
using Trading.Host;

namespace Trading.IntegrationTests;

[TestFixture]
public sealed class OperatorProductionCompositionTests
{
    private const string ResearchReportId = "01J5QH8M000000000000000701";
    private const string ResearchSeriesId = "fixture-series";

    [Test]
    public async Task HostResolvesOneAuthorizedBoundaryForEveryOperatorContract()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"operator-composition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(
                Configuration(directory)));
            var services = host.Services;
            var principal = services.GetRequiredService<OperatorPrincipal>();
            var implementation = services.GetRequiredService<AuthorizedOperatorService>();

            Assert.Multiple(() =>
            {
                Assert.That(services.GetRequiredService<IOperatorAuthorization>(), Is.TypeOf<ProductionOperatorAuthorization>());
                Assert.That(services.GetRequiredService<IOperatorWorkflowPort>(), Is.TypeOf<ProductionOperatorWorkflowPort>());
                Assert.That(services.GetRequiredService<IOperatorQueries>(), Is.SameAs(implementation));
                Assert.That(services.GetRequiredService<IBotOperatorService>(), Is.SameAs(implementation));
                Assert.That(services.GetRequiredService<IRunOperatorService>(), Is.SameAs(implementation));
                Assert.That(services.GetRequiredService<IResearchOperatorService>(), Is.SameAs(implementation));
                Assert.That(services.GetRequiredService<IProposalOperatorService>(), Is.SameAs(implementation));
                Assert.That(services.GetRequiredService<IKillSwitchOperatorService>(), Is.SameAs(implementation));
            });

            var overview = await implementation.GetOverviewAsync(principal, CancellationToken.None);
            var create = await implementation.CreateAsync(principal, "fixture bot", CancellationToken.None);
            var trigger = await implementation.TriggerAsync(principal,
                TradingBotId.Parse("01J5QH8M000000000000000101"), "manual fixture", CancellationToken.None);
            var request = await implementation.RequestAsync(principal,
                TradingBotId.Parse("01J5QH8M000000000000000101"), "fixture research", CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(overview.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
                Assert.That(create.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
                Assert.That(trigger.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
                Assert.That(request.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            });
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task MissingPermissionAndOutOfScopeResourceHaveSameUnavailableResult()
    {
        var options = new TradingHostOptions
        {
            OperatorMode = true,
            WpfTestProfile = true,
            DataDirectory = Path.GetTempPath(),
        };
        var service = new AuthorizedOperatorService(new ProductionOperatorAuthorization(options),
            new ProductionOperatorWorkflowPort(options));
        var reader = new OperatorPrincipal("reader", [OperatorAuthority.ReadOperations]);
        var operatorPrincipal = ProductionOperatorAuthorization.CreatePrincipal(options);
        var missingPermission = await service.TriggerAsync(reader,
            TradingBotId.Parse("01J5QH8M000000000000000101"), "denied", CancellationToken.None);
        var outside = await service.GetPageAsync<BotSummary>(operatorPrincipal, OperatorPageKind.Bots,
            new(OperatorResourceKind.TradingBot, "01J5QH8M000000000000009999"), new(), new(0, 1),
            CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(missingPermission.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(missingPermission.Code, Is.EqualTo("operator.unavailable"));
            Assert.That(outside.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(outside.Value, Is.Null);
        });
    }

    [Test]
    public async Task WpfTestProfileAuthorizesCatalogExactReportAndSeriesHistory()
    {
        var options = new TradingHostOptions
        {
            OperatorMode = true,
            WpfTestProfile = true,
            DataDirectory = Path.GetTempPath(),
        };
        var service = new AuthorizedOperatorService(new ProductionOperatorAuthorization(options),
            new ProductionOperatorWorkflowPort(options));
        var principal = ProductionOperatorAuthorization.CreatePrincipal(options);

        var catalog = await service.GetPageAsync<ResearchSummary>(principal, OperatorPageKind.Research,
            OperatorResource.Platform, new(), new(0, 20), CancellationToken.None);
        var exact = await service.GetPageAsync<ResearchDetail>(principal, OperatorPageKind.Research,
            new(OperatorResourceKind.ResearchReport, ResearchReportId),
            new(Status: $"exact:{ResearchSeriesId}:1"), new(0, 1), CancellationToken.None);
        var versions = await service.GetPageAsync<ResearchSummary>(principal, OperatorPageKind.Research,
            new(OperatorResourceKind.ResearchReport, ResearchSeriesId), new(Status: "versions"), new(0, 20),
            CancellationToken.None);

        var summary = exact.Value?.Items.Single().Summary;
        Assert.Multiple(() =>
        {
            Assert.That(catalog.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            Assert.That(catalog.Value?.Items.Single().Id.ToString(), Is.EqualTo(ResearchReportId));
            Assert.That(exact.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            Assert.That(summary?.Id.ToString(), Is.EqualTo(ResearchReportId));
            Assert.That(summary?.SeriesId, Is.EqualTo(ResearchSeriesId));
            Assert.That(summary?.Version, Is.EqualTo(1));
            Assert.That(versions.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            Assert.That(versions.Value?.Items.Single().SeriesId, Is.EqualTo(ResearchSeriesId));
            Assert.That(versions.Value?.Items.Single().Version, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResearchFixtureAuthorityRemainsProfileBoundedAndNonDisclosing()
    {
        var testOptions = new TradingHostOptions { OperatorMode = true, WpfTestProfile = true };
        var testService = new AuthorizedOperatorService(new ProductionOperatorAuthorization(testOptions),
            new ProductionOperatorWorkflowPort(testOptions));
        var operatorPrincipal = ProductionOperatorAuthorization.CreatePrincipal(testOptions);
        var readerWithoutPermission = new OperatorPrincipal("reader", []);
        var unknownIdentity = new OperatorResource(OperatorResourceKind.ResearchReport,
            "01J5QH8M000000000000009999");
        var exactResource = new OperatorResource(OperatorResourceKind.ResearchReport, ResearchReportId);

        var missingPermission = await testService.GetPageAsync<ResearchDetail>(readerWithoutPermission,
            OperatorPageKind.Research, exactResource, new(Status: $"exact:{ResearchSeriesId}:1"), new(0, 1),
            CancellationToken.None);
        var outsideScope = await testService.GetPageAsync<ResearchDetail>(operatorPrincipal,
            OperatorPageKind.Research, unknownIdentity, new(Status: "exact:unknown:1"), new(0, 1),
            CancellationToken.None);
        var wrongResourceKind = await testService.GetPageAsync<ResearchDetail>(operatorPrincipal,
            OperatorPageKind.Research, new(OperatorResourceKind.Order, ResearchReportId),
            new(Status: $"exact:{ResearchSeriesId}:1"), new(0, 1), CancellationToken.None);

        var defaultOptions = new TradingHostOptions { OperatorMode = true, WpfTestProfile = false };
        var defaultService = new AuthorizedOperatorService(new ProductionOperatorAuthorization(defaultOptions),
            new ProductionOperatorWorkflowPort(defaultOptions));
        var defaultResult = await defaultService.GetPageAsync<ResearchDetail>(
            ProductionOperatorAuthorization.CreatePrincipal(defaultOptions), OperatorPageKind.Research,
            exactResource, new(Status: $"exact:{ResearchSeriesId}:1"), new(0, 1), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(missingPermission.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(missingPermission.Value, Is.Null);
            Assert.That(outsideScope.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(outsideScope.Value, Is.Null);
            Assert.That(wrongResourceKind.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(wrongResourceKind.Value, Is.Null);
            Assert.That(defaultResult.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(defaultResult.Value, Is.Null);
        });
    }

    private static Dictionary<string, string?> Configuration(string directory) => new()
    {
        ["Trading:Mode"] = "Simulated",
        ["Trading:DataDirectory"] = directory,
        ["Trading:OperatorMode"] = "true",
        ["Trading:WpfTestProfile"] = "true",
        ["Research:Mode"] = "Fixture",
        ["Research:FixtureVersion"] = "v1",
        ["Research:ModelProvider"] = "scripted",
        ["Research:ModelId"] = "research",
    };
}

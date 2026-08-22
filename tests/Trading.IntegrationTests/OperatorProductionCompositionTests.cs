using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trading.Core.Identifiers;
using Trading.Engine.Operators;
using Trading.Host;

namespace Trading.IntegrationTests;

[TestFixture]
public sealed class OperatorProductionCompositionTests
{
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

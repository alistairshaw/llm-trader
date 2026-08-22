using Trading.Core.Brokers;
using Trading.Core.Orders;
using Trading.Engine.Execution;
using Trading.Engine.Runtime;

namespace Trading.Architecture.Tests;

public sealed class OrderExecutionAuthorityTests
{
    [Test]
    public void CoreOrderAndBrokerContractsRemainInfrastructureNeutral()
    {
        var references = typeof(Order).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);
        Assert.That(references, Has.None.Matches<string>(name => name is not null &&
            (name.Contains("EntityFrameworkCore", StringComparison.Ordinal) ||
             name.Contains("Sqlite", StringComparison.Ordinal) ||
             name.Contains("WindowsDesktop", StringComparison.Ordinal) ||
             name.Contains("BrokerSdk", StringComparison.Ordinal))));
    }

    [Test]
    public void LlmToolDispatchersExposeNoExecutionOperation()
    {
        Type[] dispatchers = [typeof(StageThreeToolDispatcher), typeof(TradingBotResearchToolDispatcher), typeof(ProposalToolDispatcher)];
        var toolNames = dispatchers.SelectMany(type =>
            type.GetProperty("Definitions")?.GetValue(null) is IEnumerable<ToolDefinition> definitions
                ? definitions.Select(definition => definition.Name)
                : []);
        Assert.That(toolNames, Has.None.Matches<string>(name =>
            name.Contains("Order", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Broker", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Submit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Cancel", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void PaperGatewayCannotAcceptLiveEnvironmentIdentity()
    {
        var operationContexts = typeof(IPaperBrokerGateway).GetMethods().Where(method => !method.IsSpecialName)
            .Select(method => method.GetParameters()[0].ParameterType);
        Assert.That(operationContexts, Has.All.EqualTo(typeof(PaperBrokerOperationContext)));
    }

    [Test]
    public void ProposalConversionHasOnlyDeterministicPersistenceAndIdentifierAuthority()
    {
        var dependencies = typeof(ProposalOrderConversionService).GetConstructors().Single()
            .GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        Assert.That(dependencies, Is.EquivalentTo(new[]
        {
            typeof(Trading.Core.Persistence.IAtomicOrderConversionRepository),
            typeof(IOrderExecutionIdentifierSource),
        }));
        Assert.That(dependencies, Has.None.Matches<Type>(type =>
            type.Name.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
            type.Name.Contains("Llm", StringComparison.OrdinalIgnoreCase) ||
            type == typeof(IPaperBrokerGateway)));
    }
}

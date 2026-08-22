using Trading.Core.Brokers;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[Category("BrokerContracts")]
public sealed class OrderExecutionContractTests
{
    private static readonly string[] GatewayOperations =
        ["CancelAsync", "FindByClientOrderIdAsync", "get_Capabilities", "ReconcileAsync", "SubmitAsync"];
    private static readonly string[] WorkStoreOperations = ["ClaimAsync", "CompleteAsync", "RetryAsync"];
    private static readonly string[] InboxOperations = ["ReceiveAsync", "ClaimAsync", "CompleteAsync"];

    [Test]
    public void PaperGatewayExposesEveryNormalizedBrokerOperation()
    {
        var operations = typeof(IPaperBrokerGateway).GetMethods().Select(method => method.Name).Order().ToArray();
        Assert.That(operations, Is.EqualTo(GatewayOperations));
    }

    [Test]
    public void EveryPaperGatewayOperationRequiresCancellationAndPaperContext()
    {
        var operations = typeof(IPaperBrokerGateway).GetMethods().Where(method => !method.IsSpecialName);
        Assert.That(operations, Has.All.Matches<System.Reflection.MethodInfo>(method =>
            method.GetParameters()[0].ParameterType == typeof(PaperBrokerOperationContext) &&
            method.GetParameters()[^1].ParameterType == typeof(CancellationToken)));
    }

    [Test]
    public void ExecutionPortsExposeInjectedTransactionClockAndIdentitySeams()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(IOrderExecutionClock).GetProperty(nameof(IOrderExecutionClock.UtcNow)), Is.Not.Null);
            Assert.That(typeof(IOrderExecutionIdentifierSource).GetMethods(), Has.Length.EqualTo(6));
            Assert.That(typeof(IOrderExecutionTransaction).GetMethods().Single().IsGenericMethod, Is.True);
            Assert.That(typeof(IOrderWorkStore).GetMethods().Select(x => x.Name), Is.EquivalentTo(WorkStoreOperations));
            Assert.That(typeof(IBrokerInbox).GetMethods().Select(x => x.Name), Is.EquivalentTo(InboxOperations));
        });
    }
}

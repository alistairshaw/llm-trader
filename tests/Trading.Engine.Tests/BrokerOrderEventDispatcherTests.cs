using System.Text.Json;
using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[TestFixture, Category("BrokerOrderEvents")]
public sealed class BrokerOrderEventDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    [TestCase(BrokerOrderEventWriteDisposition.Applied, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerOrderEventWriteDisposition.Duplicate, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerOrderEventWriteDisposition.Reconcile, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerOrderEventWriteDisposition.Deferred, DurableBrokerDispatchDisposition.Retryable)]
    [TestCase(BrokerOrderEventWriteDisposition.Contention, DurableBrokerDispatchDisposition.Retryable)]
    public async Task MapsDurableWriteDisposition(BrokerOrderEventWriteDisposition write,
        DurableBrokerDispatchDisposition expected)
    {
        var repository = new Repository(new(write, "broker_event.result"));
        var result = await new BrokerOrderEventDispatcher(repository, new Clock(), "worker-a")
            .DispatchAsync(Message(), default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Disposition, Is.EqualTo(expected));
            Assert.That(repository.Command!.LeaseOwner, Is.EqualTo("worker-a"));
            Assert.That(repository.Command.Environment, Is.EqualTo("Paper"));
            Assert.That(repository.Command.Kind, Is.EqualTo(BrokerOrderEventKind.Acknowledged));
        });
    }

    [TestCase("{\"schemaVersion\":2}")]
    [TestCase("{\"schemaVersion\":1,\"unexpected\":true}")]
    [TestCase("{\"schemaVersion\":1,\"environment\":\"Live\",\"brokerAccountId\":\"bad\",\"clientOrderId\":\"x\",\"brokerOrderId\":null,\"kind\":\"Acknowledged\",\"code\":\"broker.acknowledged\",\"occurredAt\":\"2026-08-22T18:00:00.0000000+00:00\"}")]
    public async Task RejectsUnknownVersionFieldsAndNonPaperEnvironment(string payload)
    {
        var repository = new Repository(new(BrokerOrderEventWriteDisposition.Applied, "unused"));
        var result = await new BrokerOrderEventDispatcher(repository, new Clock(), "worker")
            .DispatchAsync(Message(payload), default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Disposition, Is.EqualTo(DurableBrokerDispatchDisposition.Terminal));
            Assert.That(result.Code, Is.EqualTo(BrokerOrderEventCodes.InvalidSchema));
            Assert.That(repository.Command, Is.Null);
        });
    }

    [Test]
    public async Task ExecutionIsHandedOffToFillAccounting()
    {
        var payload = Payload(BrokerOrderEventKind.Execution);
        var result = await new BrokerOrderEventDispatcher(
                new Repository(new(BrokerOrderEventWriteDisposition.Applied, "unused")), new Clock(), "worker")
            .DispatchAsync(Message(payload), default);
        Assert.That(result.Code, Is.EqualTo(BrokerOrderEventCodes.InvalidSchema));
    }

    private static BrokerInboxEnvelope Message(string? payload = null) => new(BrokerMessageId.New(), "event-1",
        payload ?? Payload(BrokerOrderEventKind.Acknowledged), new("paper:event-1"), Now, 1);

    private static string Payload(BrokerOrderEventKind kind)
    {
        var account = BrokerAccountId.New();
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            environment = "Paper",
            brokerAccountId = account.ToString(),
            clientOrderId = "paper-client-1",
            brokerOrderId = "paper-order-1",
            kind = kind.ToString(),
            code = "broker.acknowledged",
            occurredAt = Now.ToString("O")
        });
    }

    private sealed class Clock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Repository(BrokerOrderEventWriteResult result) : IBrokerOrderEventRepository
    {
        public ApplyBrokerOrderEventCommand? Command { get; private set; }
        public Task<BrokerOrderEventWriteResult> ApplyAsync(ApplyBrokerOrderEventCommand command,
            CancellationToken token)
        {
            Command = command;
            return Task.FromResult(result);
        }
    }
}

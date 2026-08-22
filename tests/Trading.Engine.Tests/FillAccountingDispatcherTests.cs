using System.Text.Json;
using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[TestFixture, Category("FillAccounting")]
public sealed class FillAccountingDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task StrictExecutionPayloadIsHandedToAtomicRepository()
    {
        var repository = new Repository(new(FillAccountingWriteDisposition.Applied, "order_execution.fill_applied"));
        var result = await new FillAccountingDispatcher(repository, new Clock(), "worker").DispatchAsync(Message(), default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Disposition, Is.EqualTo(DurableBrokerDispatchDisposition.Finalized));
            Assert.That(repository.Command!.Execution.ExecutionId, Is.EqualTo("execution-1"));
            Assert.That(repository.Command.Execution.Quantity.Amount, Is.EqualTo(4m));
            Assert.That(repository.Command.Execution.Price.Amount, Is.EqualTo(69.5m));
            Assert.That(repository.Command.Execution.Fee.Amount, Is.EqualTo(1.25m));
        });
    }

    [TestCase("Live", DurableBrokerDispatchDisposition.Terminal)]
    [TestCase("Paper", DurableBrokerDispatchDisposition.Finalized)]
    public async Task OnlyExactPaperSchemaIsAccepted(string environment, DurableBrokerDispatchDisposition expected)
    {
        var repository = new Repository(new(FillAccountingWriteDisposition.Applied, "order_execution.fill_applied"));
        var result = await new FillAccountingDispatcher(repository, new Clock(), "worker")
            .DispatchAsync(Message(environment), default);
        Assert.That(result.Disposition, Is.EqualTo(expected));
    }

    private static BrokerInboxEnvelope Message(string environment = "Paper")
    {
        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            environment,
            brokerAccountId = BrokerAccountId.New().ToString(),
            clientOrderId = "paper-client-1",
            brokerOrderId = "paper-order-1",
            kind = "Execution",
            code = "broker.execution",
            occurredAt = Now.ToString("O"),
            execution = new
            {
                executionId = "execution-1",
                quantity = 4m,
                quantityUnit = "shares",
                price = 69.5m,
                currency = "USD",
                fee = 1.25m,
                feeCurrency = "USD",
                executedAt = Now.ToString("O")
            }
        });
        return new(BrokerMessageId.New(), Guid.NewGuid().ToString("N"), payload, new("fill-correlation"), Now, 1);
    }

    private sealed class Clock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Repository(FillAccountingWriteResult result) : IFillAccountingRepository
    {
        public ApplyFillAccountingCommand? Command { get; private set; }
        public Task<FillAccountingWriteResult> ApplyAsync(ApplyFillAccountingCommand command, CancellationToken token)
        { Command = command; return Task.FromResult(result); }
    }
}

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Brokers.Simulation;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Core.Proposals;
using Trading.Data;
using Trading.Engine.Execution;

namespace Trading.Host;

internal static partial class PaperSmoke
{
    public static async Task RunAsync(IServiceProvider services, CapitalReservation reservation,
        ILogger logger, CancellationToken token)
    {
        var state = services.GetRequiredService<PaperSmokeState>();
        var conversion = await services.GetRequiredService<IOrderConversionService>().ConvertAsync(
            new(ProposalSmoke.ValidId, reservation.Id, state.UtcNow), token);
        var order = conversion.Order ?? throw new InvalidOperationException($"Paper conversion failed: {conversion.Code}");
        services.GetRequiredService<TradingDbContext>().ChangeTracker.Clear();
        var client = new ClientOrderIdentity(order.ClientOrderId);
        var broker = services.GetRequiredService<SimulatedPaperBroker>();
        broker.Configure(client, new(SimulatedSubmissionBehavior.TimeoutAfterAcceptance,
        [
            new(BrokerOrderEventKind.Acknowledged, "broker.acknowledged"),
            new(BrokerOrderEventKind.Execution, "broker.execution.partial",
                new(new Quantity(30, "shares"), new Price(10, Currency.USD), new Money(1, Currency.USD))),
            new(BrokerOrderEventKind.Execution, "broker.execution.final",
                new(new Quantity(40, "shares"), new Price(10, Currency.USD), new Money(1, Currency.USD))),
            new(BrokerOrderEventKind.Execution, "broker.execution.duplicate", DuplicateOf: 2),
        ]));

        var outbox = services.GetRequiredService<OrderOutboxProcessor>();
        var firstDrain = await outbox.DrainOnceAsync(token);
        services.GetRequiredService<TradingDbContext>().ChangeTracker.Clear();
        var secondDrain = await outbox.DrainOnceAsync(token);

        var context = new PaperBrokerOperationContext(SmokeFixture.AccountTwoId,
            BrokerConnectionId.Parse("01J5QH8M000000000000000304"),
            new BrokerOperationEnvironment.Paper("Deterministic paper fixture"), new("paper-smoke-events"), state.UtcNow);
        var events = await broker.ReadEventsAsync(context, token);
        var inboxRepository = services.GetRequiredService<IBrokerInboxRepository>();
        foreach (var brokerEvent in events.DistinctBy(x => x.MessageId))
        {
            var payload = EventPayload(brokerEvent);
            _ = await inboxRepository.ReceiveAsync(new(brokerEvent.MessageId,
                $"paper:{brokerEvent.MessageId}", payload, new("paper-smoke-events"), state.UtcNow), token);
        }
        services.GetRequiredService<TradingDbContext>().ChangeTracker.Clear();

        var inbox = services.GetRequiredService<BrokerInboxProcessor>();
        var inboxDrain = await inbox.DrainOnceAsync(token);

        var duplicateDrain = await inbox.DrainOnceAsync(token);
        var detail = await services.GetRequiredService<IOrderExecutionQueries>().GetOrderAsync(
            new("smoke-operator", true, [], [], []), order.Id, token)
            ?? throw new InvalidOperationException("Paper Order projection was not available.");
        if (detail.Order.Status != OrderStatus.Filled || detail.FilledQuantity != 70 || detail.GrossAmount != 700 ||
            detail.Fees != 2 || detail.ReservationStatus != "Consumed" || detail.RemainingReservation != 0 ||
            detail.Fills.Count != 2 || detail.Audit.Count == 0 || broker.Snapshot(context).Count != 1)
            throw new InvalidOperationException($"The Stage 6 paper smoke produced an unexpected durable outcome: status={detail.Order.Status};quantity={detail.FilledQuantity};gross={detail.GrossAmount};fees={detail.Fees};reservation={detail.ReservationStatus};remaining={detail.RemainingReservation};fills={detail.Fills.Count};audit={detail.Audit.Count};broker={broker.Snapshot(context).Count};outbox={firstDrain}/{secondDrain};inbox={inboxDrain}.");

        Result(logger, order.Id.ToString(), order.ClientOrderId, conversion.Code, firstDrain.Processed,
            secondDrain.Processed, inboxDrain.Processed, duplicateDrain.Processed, detail.BrokerOrderId!,
            detail.FilledQuantity, detail.GrossAmount, detail.Fees, detail.ReservationStatus!,
            detail.Fills.Count, detail.Audit.Count, liveSubmissions: 0, reconciledUnknown: true, recoverable: true);
    }

    private static string EventPayload(BrokerOrderEvent value) => value.Execution is null
        ? JsonSerializer.Serialize(new { brokerAccountId = SmokeFixture.AccountTwoId.ToString(), brokerOrderId = value.BrokerOrderId, clientOrderId = value.ClientOrderId.Value, code = value.Code, environment = "Paper", kind = value.Kind.ToString(), occurredAt = value.OccurredAt.ToString("O"), schemaVersion = 1 })
        : JsonSerializer.Serialize(new { brokerAccountId = SmokeFixture.AccountTwoId.ToString(), brokerOrderId = value.BrokerOrderId, clientOrderId = value.ClientOrderId.Value, code = value.Code, environment = "Paper", execution = new { currency = value.Execution.Price.Currency.Code, executedAt = value.Execution.ExecutedAt.ToString("O"), executionId = value.Execution.ExecutionId, fee = value.Execution.Fee.Amount, feeCurrency = value.Execution.Fee.Currency.Code, price = value.Execution.Price.Amount, quantity = value.Execution.Quantity.Amount, quantityUnit = value.Execution.Quantity.Unit }, kind = value.Kind.ToString(), occurredAt = value.OccurredAt.ToString("O"), schemaVersion = 1 });

    [LoggerMessage(30, LogLevel.Information,
        "Stage6 Order={Order} ClientOrder={ClientOrder} Conversion={Conversion} SubmitDrain={SubmitDrain} ReconcileDrain={ReconcileDrain} InboxDrain={InboxDrain} DuplicateDrain={DuplicateDrain} BrokerOrder={BrokerOrder} PositionQuantity={PositionQuantity} Gross={Gross} Fees={Fees} Reservation={Reservation} FillCount={FillCount} AuditCount={AuditCount} LiveSubmissions={LiveSubmissions} ReconciledUnknown={ReconciledUnknown} Recoverable={Recoverable}")]
    private static partial void Result(ILogger logger, string order, string clientOrder, string conversion,
        int submitDrain, int reconcileDrain, int inboxDrain, int duplicateDrain, string brokerOrder,
        decimal positionQuantity, decimal gross, decimal fees, string reservation, int fillCount,
        int auditCount, int liveSubmissions, bool reconciledUnknown, bool recoverable);
}

internal sealed class PaperWorkDispatcher(IOrderWorkDispatcher submit, IOrderWorkDispatcher reconcile) : IOrderWorkDispatcher
{
    public Task<DurableBrokerDispatchResult> DispatchAsync(OrderWorkEnvelope work, CancellationToken token) =>
        work.Kind == OrderWorkKind.Submit ? submit.DispatchAsync(work, token) : reconcile.DispatchAsync(work, token);
}

internal sealed class PaperInboxDispatcher(IBrokerInboxDispatcher status, IBrokerInboxDispatcher fills) : IBrokerInboxDispatcher
{
    public Task<DurableBrokerDispatchResult> DispatchAsync(BrokerInboxEnvelope message, CancellationToken token) =>
        message.CanonicalPayload.Contains("\"kind\":\"Execution\"", StringComparison.Ordinal)
            ? fills.DispatchAsync(message, token) : status.DispatchAsync(message, token);
}

internal sealed class SmokePaperAccountReconciler : IPaperBrokerAccountReconciler
{
    public Task<bool> ReconcileAsync(BrokerAccountId accountId, CancellationToken token)
    { token.ThrowIfCancellationRequested(); return Task.FromResult(accountId == SmokeFixture.AccountTwoId); }
}

internal sealed class ImmediatePaperLatency : ISimulatedBrokerLatency
{
    public Task WaitAsync(string operation, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
}

internal sealed class PaperSmokeState : IOrderExecutionClock, IOrderExecutionIdentifierSource,
    ISimulatedBrokerClock, ISimulatedBrokerIdentifierSource
{
    private int sequence;
    public DateTimeOffset UtcNow => new(2026, 8, 20, 23, 5, 0, TimeSpan.Zero);
    private string Next() => $"01J5QH8M00000000000000{++sequence:0000}";
    public OrderId NewOrderId() => OrderId.Parse(Next());
    public OrderTransitionId NewTransitionId() => OrderTransitionId.Parse(Next());
    public FillId NewFillId() => FillId.Parse(Next());
    public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.Parse(Next());
    public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.Parse(Next());
    public BrokerMessageId NewMessageId() => BrokerMessageId.Parse(Next());
    public CorrelationIdentity NewCorrelationId() => new($"paper-smoke-{++sequence:0000}");
    public string NewBrokerOrderId() => $"paper-broker-{++sequence:0000}";
    public string NewExecutionId() => $"paper-execution-{++sequence:0000}";
}

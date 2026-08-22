using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public sealed record PaperOrderReconciliationOptions(TimeSpan BrokerTimeout, TimeSpan AbsenceGracePeriod,
    int RequiredAbsenceConfirmations, int MaximumAttempts)
{
    public static PaperOrderReconciliationOptions Default { get; } = new(TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(5), 2, 5);
    public PaperOrderReconciliationOptions Validate()
    {
        if (BrokerTimeout <= TimeSpan.Zero || BrokerTimeout > TimeSpan.FromMinutes(5) || AbsenceGracePeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(BrokerTimeout));
        ArgumentOutOfRangeException.ThrowIfLessThan(RequiredAbsenceConfirmations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumAttempts, RequiredAbsenceConfirmations);
        return this;
    }
}

public sealed class PaperOrderReconciliationDispatcher(IOrderReconciliationRepository repository,
    IPaperBrokerGateway gateway, IOrderExecutionClock clock, IOrderExecutionIdentifierSource identifiers,
    PaperOrderReconciliationOptions options) : IOrderWorkDispatcher, IOrderReconciliationService
{
    private readonly PaperOrderReconciliationOptions options = options.Validate();

    public async Task<DurableBrokerDispatchResult> DispatchAsync(OrderWorkEnvelope work, CancellationToken cancellationToken)
    {
        var prepared = await repository.PrepareAsync(work, gateway.Capabilities, cancellationToken).ConfigureAwait(false);
        if (prepared is PrepareOrderReconciliationResult.AlreadyCompleted completed)
            return new(DurableBrokerDispatchDisposition.Completed, completed.Code);
        if (prepared is PrepareOrderReconciliationResult.Rejected rejected)
            return new(DurableBrokerDispatchDisposition.Terminal, rejected.Code);
        if (prepared is PrepareOrderReconciliationResult.Contention)
            return new(DurableBrokerDispatchDisposition.Retryable, OrderReconciliationCodes.Contention);
        var value = ((PrepareOrderReconciliationResult.Ready)prepared).Value;
        var started = clock.UtcNow;
        BrokerReconciliationResult result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.BrokerTimeout);
        try
        {
            var context = new PaperBrokerOperationContext(value.BrokerAccountId, value.BrokerConnectionId,
                new BrokerOperationEnvironment.Paper(value.EnvironmentName), value.CorrelationId, started);
            result = await gateway.FindByClientOrderIdAsync(context, new(value.ClientOrderId), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { result = new(BrokerReconciliationOutcome.RetryableFailure, BrokerExecutionCodes.ReconciliationUncertain, null, null, clock.UtcNow); }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        { result = new(BrokerReconciliationOutcome.RetryableFailure, BrokerExecutionCodes.ReconciliationUncertain, null, null, clock.UtcNow); }

        var resolution = Resolve(value, result, clock.UtcNow);
        if (resolution is OrderReconciliationCodes.AbsentPending or OrderReconciliationCodes.Unavailable or OrderReconciliationCodes.Uncertain)
            return value.Attempt >= options.MaximumAttempts
                ? await PersistAsync(value, result, OrderReconciliationCodes.AttemptsExhausted, started, cancellationToken).ConfigureAwait(false)
                : new(DurableBrokerDispatchDisposition.Retryable, resolution);
        return await PersistAsync(value, result, resolution, started, cancellationToken).ConfigureAwait(false);
    }

    public Task<BrokerReconciliationResult> ReconcileAsync(OrderId orderId, CancellationToken cancellationToken) =>
        Task.FromException<BrokerReconciliationResult>(new NotSupportedException(
            "Durable reconciliation requires a claimed work envelope."));

    private string Resolve(PreparedOrderReconciliation value, BrokerReconciliationResult result, DateTimeOffset now)
    {
        if (result.Outcome == BrokerReconciliationOutcome.Found)
            return result.BrokerOrderId is null || result.Status is null ? OrderReconciliationCodes.IdentityMismatch : OrderReconciliationCodes.Found;
        if (result.Outcome == BrokerReconciliationOutcome.Absent)
            return now - value.UnknownSince >= options.AbsenceGracePeriod && value.Attempt >= options.RequiredAbsenceConfirmations
                ? OrderReconciliationCodes.AbsenceConfirmed : OrderReconciliationCodes.AbsentPending;
        return result.Outcome == BrokerReconciliationOutcome.RetryableFailure
            ? OrderReconciliationCodes.Unavailable : OrderReconciliationCodes.Uncertain;
    }

    private async Task<DurableBrokerDispatchResult> PersistAsync(PreparedOrderReconciliation value,
        BrokerReconciliationResult result, string resolution, DateTimeOffset started, CancellationToken token)
    {
        var saved = await repository.CompleteAsync(new(value, result, resolution, started, clock.UtcNow,
            identifiers.NewTransitionId()), token).ConfigureAwait(false);
        return saved is PersistenceWriteResult.Succeeded
            ? new(DurableBrokerDispatchDisposition.Finalized, resolution)
            : new(DurableBrokerDispatchDisposition.Retryable, OrderReconciliationCodes.Contention);
    }
}

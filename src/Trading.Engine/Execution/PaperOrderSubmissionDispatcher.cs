using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public sealed record PaperOrderSubmissionOptions(TimeSpan BrokerTimeout)
{
    public static PaperOrderSubmissionOptions Default { get; } = new(TimeSpan.FromSeconds(30));
    public PaperOrderSubmissionOptions Validate()
    {
        if (BrokerTimeout <= TimeSpan.Zero || BrokerTimeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(BrokerTimeout));
        return this;
    }
}

public sealed class PaperOrderSubmissionDispatcher(
    IOrderSubmissionRepository repository,
    IPaperBrokerGateway gateway,
    IOrderExecutionClock clock,
    IOrderExecutionIdentifierSource identifiers,
    PaperOrderSubmissionOptions options) : IOrderWorkDispatcher
{
    private readonly PaperOrderSubmissionOptions options = options.Validate();

    public async Task<DurableBrokerDispatchResult> DispatchAsync(OrderWorkEnvelope work, CancellationToken cancellationToken)
    {
        var prepared = await repository.PrepareAsync(work, clock.UtcNow, gateway.Capabilities, cancellationToken).ConfigureAwait(false);
        if (prepared is PrepareOrderSubmissionResult.AlreadyCompleted already)
            return new(DurableBrokerDispatchDisposition.Completed, already.Code);
        if (prepared is PrepareOrderSubmissionResult.Rejected rejected)
            return new(DurableBrokerDispatchDisposition.Terminal, rejected.Code);
        if (prepared is PrepareOrderSubmissionResult.Contention)
            return new(DurableBrokerDispatchDisposition.Retryable, OrderSubmissionCodes.Contention);
        var submission = ((PrepareOrderSubmissionResult.Ready)prepared).Value;
        var startedAt = clock.UtcNow;
        BrokerSubmissionResult result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.BrokerTimeout);
        try
        {
            var context = new PaperBrokerOperationContext(submission.BrokerAccountId, submission.BrokerConnectionId,
                new BrokerOperationEnvironment.Paper(submission.EnvironmentName), submission.CorrelationId, startedAt);
            result = await gateway.SubmitAsync(context, submission.Request, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new(BrokerSubmissionOutcome.Unknown, BrokerExecutionCodes.Unknown, null, clock.UtcNow);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            result = new(BrokerSubmissionOutcome.Unknown, BrokerExecutionCodes.Unknown, null, clock.UtcNow);
        }

        if (result.Outcome == BrokerSubmissionOutcome.RetryableFailure)
            return new(DurableBrokerDispatchDisposition.Retryable, result.Code);
        if (result.Outcome == BrokerSubmissionOutcome.Duplicate && result.BrokerOrderId is null)
            result = new(BrokerSubmissionOutcome.Unknown, BrokerExecutionCodes.Unknown, null, result.CompletedAt);
        var transitions = new[] { identifiers.NewTransitionId(), identifiers.NewTransitionId(), identifiers.NewTransitionId() };
        var saved = await repository.CompleteAsync(new(submission, result, startedAt, result.CompletedAt,
            result.Code, transitions), cancellationToken).ConfigureAwait(false);
        return saved is PersistenceWriteResult.Succeeded
            ? new(DurableBrokerDispatchDisposition.Finalized, result.Code)
            : new(DurableBrokerDispatchDisposition.Retryable, OrderSubmissionCodes.Contention);
    }
}

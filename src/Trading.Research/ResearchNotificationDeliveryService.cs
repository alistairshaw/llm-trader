using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Research.Contracts;

namespace Trading.Research;

public interface IResearchNotificationIdentifierSource
{
    BotRunTriggerId NewTriggerId();
}

public sealed record ResearchNotificationBatchResult(int Delivered, int AlreadyDelivered, int Deferred);

public sealed class ResearchNotificationDeliveryService(
    IResearchNotificationRepository notifications,
    IResearchNotificationIdentifierSource identifiers,
    IResearchClock clock)
{
    public async Task<ResearchNotificationBatchResult> DeliverPendingAsync(ResearchRequestId requestId,
        int batchSize, int maximumAttempts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (maximumAttempts is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        var pending = await notifications.GetPendingAsync(requestId, batchSize, cancellationToken).ConfigureAwait(false);
        var delivered = 0; var duplicate = 0; var deferred = 0;
        foreach (var subscriptionId in pending)
        {
            var completed = false;
            for (var attempt = 0; attempt < maximumAttempts && !completed; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ResearchNotificationDeliveryResult result;
                try
                {
                    result = await notifications.DeliverAsync(subscriptionId, identifiers.NewTriggerId(),
                        clock.UtcNow, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { continue; }
                switch (result)
                {
                    case ResearchNotificationDeliveryResult.Delivered: delivered++; completed = true; break;
                    case ResearchNotificationDeliveryResult.AlreadyDelivered: duplicate++; completed = true; break;
                    case ResearchNotificationDeliveryResult.NotTerminal: deferred++; completed = true; break;
                    case ResearchNotificationDeliveryResult.ConcurrencyConflict: break;
                }
            }
            if (!completed) deferred++;
        }
        return new(delivered, duplicate, deferred);
    }
}

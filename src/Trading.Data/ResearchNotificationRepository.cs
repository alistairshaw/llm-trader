using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Research;

namespace Trading.Data;

public sealed class ResearchNotificationRepository(TradingDbContext db) : IResearchNotificationRepository
{
    public async Task<IReadOnlyList<ResearchSubscriptionId>> GetPendingAsync(ResearchRequestId requestId, int limit, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        return await db.ResearchSubscriptions.AsNoTracking()
            .Where(x => x.ResearchRequestId == requestId.ToString() && x.NotificationStatus == "Pending")
            .OrderBy(x => x.SubscribedAt).ThenBy(x => x.Id).Take(limit)
            .Select(x => ResearchSubscriptionId.Parse(x.Id)).ToArrayAsync(token).ConfigureAwait(false);
    }

    public async Task<ResearchNotificationDeliveryResult> DeliverAsync(ResearchSubscriptionId subscriptionId,
        BotRunTriggerId triggerId, DateTimeOffset deliveredAt, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(subscriptionId); ArgumentNullException.ThrowIfNull(triggerId);
        if (deliveredAt.Offset != TimeSpan.Zero) throw new ArgumentException("Delivery time must be UTC.", nameof(deliveredAt));
        await db.Database.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(IsolationLevel.Serializable, deferred: false);
        await db.Database.UseTransactionAsync(transaction, token).ConfigureAwait(false);
        try
        {
            var subscription = await db.ResearchSubscriptions.SingleOrDefaultAsync(x => x.Id == subscriptionId.ToString(), token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Research subscription was not found.");
            var request = await db.ResearchRequests.AsNoTracking().SingleAsync(x => x.Id == subscription.ResearchRequestId, token).ConfigureAwait(false);
            var effectiveDeliveryAt = subscription.NotificationStatus == "Delivered" && subscription.NotifiedAt is not null
                ? UtcUnixMilliseconds.FromProvider(subscription.NotifiedAt.Value) : deliveredAt;
            var notification = await BuildAsync(subscription, request, effectiveDeliveryAt, token).ConfigureAwait(false);
            if (notification is null) return await RollbackAsync(new ResearchNotificationDeliveryResult.NotTerminal(), transaction, token).ConfigureAwait(false);
            var existing = await db.BotRunTriggers.AsNoTracking().SingleOrDefaultAsync(x => x.TradingBotId == subscription.TradingBotId && x.SourceType == "ResearchSubscription" && x.SourceId == subscription.Id, token).ConfigureAwait(false);
            if (subscription.NotificationStatus == "Delivered")
            {
                if (existing is null) throw new InvalidOperationException("Delivered notification is missing its durable trigger.");
                await transaction.CommitAsync(token).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
                return new ResearchNotificationDeliveryResult.AlreadyDelivered(notification, BotRunTriggerId.Parse(existing.Id));
            }
            if (subscription.NotificationStatus != "Pending") throw new InvalidOperationException("Research notification has an unsupported state.");

            var type = notification.Outcome == ResearchTerminalOutcome.Completed ? BotRunTriggerType.ResearchCompleted : BotRunTriggerType.ResearchFailed;
            var facts = CanonicalJsonSerializer.Serialize(1, new { correlationId = notification.CorrelationId, outcome = notification.Outcome.ToString(), reportId = notification.ReportId?.ToString(), reportVersion = notification.ReportVersion, requestId = notification.RequestId.ToString() });
            if (existing is null) db.BotRunTriggers.Add(new BotRunTriggerEntity { Id = triggerId.ToString(), TradingBotId = subscription.TradingBotId, TriggerType = CanonicalEnumeration.Format(type), Reason = facts, SourceType = "ResearchSubscription", SourceId = subscription.Id, OccurredAt = UtcUnixMilliseconds.ToProvider(deliveredAt), CreatedAt = UtcUnixMilliseconds.ToProvider(deliveredAt) });
            subscription.NotificationStatus = "Delivered"; subscription.NotifiedAt = UtcUnixMilliseconds.ToProvider(deliveredAt);
            await db.SaveChangesAsync(token).ConfigureAwait(false); await transaction.CommitAsync(token).ConfigureAwait(false);
            await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
            return new ResearchNotificationDeliveryResult.Delivered(notification, existing is null ? triggerId : BotRunTriggerId.Parse(existing.Id));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 5 or 6 or 1555 or 2067 })
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, CancellationToken.None).ConfigureAwait(false); db.ChangeTracker.Clear();
            return new ResearchNotificationDeliveryResult.ConcurrencyConflict();
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); } catch (InvalidOperationException) { }
            await db.Database.UseTransactionAsync(null, CancellationToken.None).ConfigureAwait(false); db.ChangeTracker.Clear(); throw;
        }
    }

    private async Task<ResearchSubscriptionNotification?> BuildAsync(ResearchSubscriptionEntity subscription, ResearchRequestEntity request, DateTimeOffset at, CancellationToken token)
    {
        if (request.Status is not ("Completed" or "Failed" or "TimedOut" or "BudgetExceeded" or "Cancelled")) return null;
        var outcome = CanonicalEnumeration.Parse<ResearchTerminalOutcome>(request.Status);
        ResearchReportId? reportId = request.ResultReportId is null ? null : ResearchReportId.Parse(request.ResultReportId); int? version = null;
        if (reportId is not null)
        {
            var report = await db.ResearchReports.AsNoTracking().SingleAsync(x => x.Id == request.ResultReportId, token).ConfigureAwait(false);
            if (!Authorized(subscription.TradingBotId, request, report.Visibility)) throw new UnauthorizedAccessException("Subscriber cannot access the terminal report.");
            version = report.VersionNumber;
        }
        return new(subscription.Id is null ? throw new InvalidOperationException() : ResearchSubscriptionId.Parse(subscription.Id), ResearchRequestId.Parse(request.Id), TradingBotId.Parse(subscription.TradingBotId), outcome, reportId, version, request.Id, at);
    }

    private static bool Authorized(string botId, ResearchRequestEntity request, string visibility) => visibility == "Shared" || visibility == "BotPrivate" && botId == request.RequestingBotId || visibility == "Restricted" && ResearchPersistenceMapper.ToDomain(request, []).AuthorizedSubscriberIds.Any(x => x.ToString() == botId);
    private async Task<T> RollbackAsync<T>(T value, SqliteTransaction transaction, CancellationToken token) { await transaction.RollbackAsync(token).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false); db.ChangeTracker.Clear(); return value; }
}

using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Research;

namespace Trading.Data;

public sealed class ResearchOrchestrationRepository(TradingDbContext db) : IResearchOrchestrationRepository
{
    public async Task<IReadOnlyList<ResearchRequestId>> GetQueuedAsync(int limit, CancellationToken token)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        return await db.ResearchRequests.AsNoTracking().Where(x => x.Status == "Queued")
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(limit)
            .Select(x => ResearchRequestId.Parse(x.Id)).ToListAsync(token).ConfigureAwait(false);
    }

    public async Task<ResearchOrchestrationWork?> TryClaimAsync(ResearchRequestId requestId,
        ResearchRunAttempt attempt, CancellationToken token)
    {
        await db.Database.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection())
            .BeginTransaction(IsolationLevel.Serializable, deferred: false);
        await db.Database.UseTransactionAsync(transaction, token).ConfigureAwait(false);
        try
        {
            var entity = await db.ResearchRequests.SingleOrDefaultAsync(x => x.Id == requestId.ToString(), token).ConfigureAwait(false);
            if (entity is null || entity.Status != "Queued") { await transaction.RollbackAsync(token).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false); db.ChangeTracker.Clear(); return null; }
            if (await db.ResearchRuns.AnyAsync(x => x.ResearchRequestId == entity.Id &&
                (x.Status == "Pending" || x.Status == "Running" || x.Status == "WaitingForTool"), token).ConfigureAwait(false))
            { await transaction.RollbackAsync(token).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false); db.ChangeTracker.Clear(); return null; }
            var number = (await db.ResearchRuns.Where(x => x.ResearchRequestId == entity.Id)
                .MaxAsync(x => (int?)x.AttemptNumber, token).ConfigureAwait(false) ?? 0) + 1;
            var started = ResearchRunAttempt.Rehydrate(new(attempt.Id, requestId, attempt.Versions, attempt.Budget,
                ResearchRunAttemptStatus.Running, attempt.CreatedAt, attempt.CreatedAt, null, null, null));
            entity.Status = "Running"; entity.StartedAt = UtcUnixMilliseconds.ToProvider(attempt.CreatedAt); entity.Version++;
            db.ResearchRuns.Add(ResearchPersistenceMapper.ToEntity(started, number, 1));
            await db.SaveChangesAsync(token).ConfigureAwait(false);
            var subscriptions = await db.ResearchSubscriptions.AsNoTracking().Where(x => x.ResearchRequestId == entity.Id)
                .OrderBy(x => x.SubscribedAt).ThenBy(x => x.Id).ToListAsync(token).ConfigureAwait(false);
            var request = ResearchPersistenceMapper.ToDomain(entity, subscriptions);
            var refresh = ResearchPersistenceMapper.RefreshReportId(entity.RequestJson);
            await transaction.CommitAsync(token).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
            return new(request, started, entity.Version, 1, number, refresh);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 5 or 6 or 1555 or 2067 })
        { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, CancellationToken.None).ConfigureAwait(false); db.ChangeTracker.Clear(); return null; }
        catch
        { try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); } catch (InvalidOperationException) { } await db.Database.UseTransactionAsync(null, CancellationToken.None).ConfigureAwait(false); db.ChangeTracker.Clear(); throw; }
    }

    public async Task<PersistenceWriteResult> TerminalizeAsync(ResearchRequestId requestId, ResearchRunAttempt attempt,
        ResearchRequestStatus requestStatus, long expectedAttemptVersion, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        try
        {
            var request = await db.ResearchRequests.SingleAsync(x => x.Id == requestId.ToString(), token).ConfigureAwait(false);
            var run = await db.ResearchRuns.SingleAsync(x => x.Id == attempt.Id.ToString(), token).ConfigureAwait(false);
            if (run.Version != expectedAttemptVersion || request.Status is not ("Running" or "WaitingForTool"))
                return await RollbackAsync<PersistenceWriteResult>(new PersistenceWriteResult.ConcurrencyConflict(expectedAttemptVersion, run.Version), transaction).ConfigureAwait(false);
            if (run.Status != attempt.Status.ToString())
                return await RollbackAsync<PersistenceWriteResult>(new PersistenceWriteResult.ConcurrencyConflict(expectedAttemptVersion, run.Version), transaction).ConfigureAwait(false);
            if (requestStatus is ResearchRequestStatus.Completed or < ResearchRequestStatus.Failed)
                throw new ArgumentException("A non-completed terminal request status is required.", nameof(requestStatus));
            request.Status = requestStatus.ToString(); request.CompletedAt = UtcUnixMilliseconds.ToProvider(attempt.CompletedAt!.Value); request.Version++;
            await db.SaveChangesAsync(token).ConfigureAwait(false); await transaction.CommitAsync(token).ConfigureAwait(false);
            return new PersistenceWriteResult.Succeeded();
        }
        catch (DbUpdateConcurrencyException)
        { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); db.ChangeTracker.Clear(); return new PersistenceWriteResult.ConcurrencyConflict(expectedAttemptVersion, null); }
    }

    public async Task<IReadOnlyList<ResearchRunAttemptId>> GetOrphanedAsync(DateTimeOffset recoveryBefore, int limit, CancellationToken token) =>
        await db.ResearchRuns.AsNoTracking().Where(x => (x.Status == "Running" || x.Status == "WaitingForTool") && x.StartedAt <= UtcUnixMilliseconds.ToProvider(recoveryBefore))
            .OrderBy(x => x.StartedAt).ThenBy(x => x.Id).Take(limit).Select(x => ResearchRunAttemptId.Parse(x.Id)).ToListAsync(token).ConfigureAwait(false);

    public async Task<PersistenceWriteResult> RecoverAndRequeueAsync(ResearchRunAttemptId attemptId, DateTimeOffset recoveredAt,
        string resultCode, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var run = await db.ResearchRuns.SingleOrDefaultAsync(x => x.Id == attemptId.ToString(), token).ConfigureAwait(false);
        if (run is null || run.Status is not ("Running" or "WaitingForTool"))
            return await RollbackAsync<PersistenceWriteResult>(new PersistenceWriteResult.ConcurrencyConflict(0, run?.Version), transaction).ConfigureAwait(false);
        var request = await db.ResearchRequests.SingleAsync(x => x.Id == run.ResearchRequestId, token).ConfigureAwait(false);
        run.Status = "Failed"; run.CompletedAt = UtcUnixMilliseconds.ToProvider(recoveredAt); run.TerminalReason = resultCode; run.Version++;
        request.Status = "Queued"; request.StartedAt = null; request.CompletedAt = null; request.Version++;
        await db.SaveChangesAsync(token).ConfigureAwait(false); await transaction.CommitAsync(token).ConfigureAwait(false);
        return new PersistenceWriteResult.Succeeded();
    }

    private async Task<T> RollbackAsync<T>(T value, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); await db.Database.UseTransactionAsync(null, CancellationToken.None).ConfigureAwait(false); db.ChangeTracker.Clear(); return value; }
}

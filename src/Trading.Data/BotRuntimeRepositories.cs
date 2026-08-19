using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;

namespace Trading.Data;

public sealed class BotRunTriggerRepository(TradingDbContext dbContext) : IBotRunTriggerRepository
{
    public async Task<PersistenceWriteResult> AppendAsync(PendingBotRunTrigger trigger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        dbContext.BotRunTriggers.Add(new BotRunTriggerEntity
        {
            Id = trigger.Id.ToString(),
            TradingBotId = trigger.TradingBotId.ToString(),
            TriggerType = CanonicalEnumeration.Format(trigger.Type),
            Reason = Require(trigger.Reason),
            SourceType = Normalize(trigger.SourceType),
            SourceId = Normalize(trigger.SourceId),
            OccurredAt = UtcUnixMilliseconds.ToProvider(trigger.OccurredAt),
            CreatedAt = UtcUnixMilliseconds.ToProvider(trigger.CreatedAt),
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new PersistenceWriteResult.Succeeded();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            dbContext.ChangeTracker.Clear();
            return new PersistenceWriteResult.UniquenessConflict(trigger.SourceId is null
                ? "bot_run_trigger_id" : "bot_run_trigger_source");
        }
    }

    public async Task<IReadOnlyList<PendingBotRunTrigger>> GetPendingAsync(TradingBotId botId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(botId);
        var entities = await dbContext.BotRunTriggers.AsNoTracking()
            .Where(x => x.TradingBotId == botId.ToString() && x.ConsumedByRunId == null)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(ToPending).ToArray();
    }

    internal static PendingBotRunTrigger ToPending(BotRunTriggerEntity x) => new(
        BotRunTriggerId.Parse(x.Id), TradingBotId.Parse(x.TradingBotId),
        CanonicalEnumeration.Parse<BotRunTriggerType>(x.TriggerType), x.Reason,
        UtcUnixMilliseconds.FromProvider(x.OccurredAt), UtcUnixMilliseconds.FromProvider(x.CreatedAt),
        x.SourceType, x.SourceId);

    private static string Require(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
    private static string? Normalize(string? value) => value is null ? null : Require(value);
}

public sealed class BotRunRepository(TradingDbContext dbContext) : IBotRunRepository, IBotRunInputAuditWriter
{
    private const int UsageSchemaVersion = 1;

    public async Task<BotRun?> GetAsync(BotRunId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await dbContext.BotRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        var triggers = await dbContext.BotRunTriggers.AsNoTracking().Where(x => x.ConsumedByRunId == entity.Id)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var tools = await dbContext.BotToolInvocations.AsNoTracking().Where(x => x.BotRunId == entity.Id)
            .OrderBy(x => x.SequenceNumber).ToListAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(entity, triggers, tools);
    }

    public async Task<BotRunLeaseResult> TryClaimAsync(BotRunClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (claim.LeaseExpiresAt <= claim.StartedAt) throw new ArgumentException("Lease expiry must follow start time.", nameof(claim));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var botId = claim.TradingBotId.ToString();
            var configurationValid = await dbContext.TradingBotConfigurationVersions.AsNoTracking().AnyAsync(
                x => x.Id == claim.ConfigurationVersionId.ToString() && x.TradingBotId == botId, cancellationToken).ConfigureAwait(false);
            var snapshotValid = await dbContext.PortfolioDecisionSnapshots.AsNoTracking().AnyAsync(
                x => x.Id == claim.PortfolioSnapshotId.ToString() && x.TradingBotId == botId && x.ConfigurationVersionId == claim.ConfigurationVersionId.ToString(), cancellationToken).ConfigureAwait(false);
            if (!configurationValid || !snapshotValid) throw new InvalidOperationException("The pinned configuration and snapshot must belong to the claimed bot.");

            var startedAt = UtcUnixMilliseconds.ToProvider(claim.StartedAt);
            var pending = await dbContext.BotRunTriggers.Where(x => x.TradingBotId == botId && x.ConsumedByRunId == null && x.OccurredAt <= startedAt)
                .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var entity = new BotRunEntity
            {
                Id = claim.RunId.ToString(),
                TradingBotId = botId,
                ConfigurationVersionId = claim.ConfigurationVersionId.ToString(),
                PortfolioSnapshotId = claim.PortfolioSnapshotId.ToString(),
                Status = CanonicalEnumeration.Format(BotRunStatus.PreparingSnapshot),
                LeaseOwner = Require(claim.LeaseOwner),
                LeaseExpiresAt = UtcUnixMilliseconds.ToProvider(claim.LeaseExpiresAt),
                StartedAt = UtcUnixMilliseconds.ToProvider(claim.StartedAt),
                UsageJson = SerializeUsage(claim.InitialUsage),
                ModelTranscriptSchemaVersion = claim.ModelTranscriptSchemaVersion,
                ModelTranscriptJson = Require(claim.ModelTranscriptJson),
                InputRenderingVersion = Require(claim.InputRenderingVersion),
                Version = 1,
            };
            dbContext.BotRuns.Add(entity);
            foreach (var trigger in pending) trigger.ConsumedByRunId = entity.Id;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new BotRunLeaseResult.Acquired(ToDomain(entity, pending, []));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            var activeId = await dbContext.BotRuns.AsNoTracking().Where(x => x.TradingBotId == claim.TradingBotId.ToString() &&
                (x.Status == "Pending" || x.Status == "AcquiringLease" || x.Status == "PreparingSnapshot" || x.Status == "Reasoning" || x.Status == "WaitingForTool"))
                .Select(x => x.Id).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return new BotRunLeaseResult.ActiveLeaseConflict(activeId is null ? null : BotRunId.Parse(activeId));
        }
    }

    public async Task<bool> RenewLeaseAsync(BotRunId runId, string leaseOwner, DateTimeOffset newExpiry, long expectedVersion, CancellationToken cancellationToken)
    {
        var expiry = UtcUnixMilliseconds.ToProvider(newExpiry);
        var rows = await dbContext.BotRuns.Where(x => x.Id == runId.ToString() && x.LeaseOwner == leaseOwner &&
            x.LeaseExpiresAt != null && x.LeaseExpiresAt < expiry && x.Version == expectedVersion &&
            (x.Status == "PreparingSnapshot" || x.Status == "Reasoning" || x.Status == "WaitingForTool"))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LeaseExpiresAt, expiry).SetProperty(x => x.Version, expectedVersion + 1), cancellationToken)
            .ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return rows == 1;
    }

    public async Task<PersistenceWriteResult> SaveAsync(BotRun run, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var entity = await dbContext.BotRuns.SingleOrDefaultAsync(x => x.Id == run.Id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        var storedTools = await dbContext.BotToolInvocations.Where(x => x.BotRunId == entity.Id).OrderBy(x => x.SequenceNumber).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (storedTools.Count > run.ToolInvocations.Count) throw new InvalidOperationException("Tool invocation audit history cannot be deleted.");
        for (var i = 0; i < storedTools.Count; i++)
        {
            if (storedTools[i].Id != run.ToolInvocations[i].Id.ToString()) throw new InvalidOperationException("Tool invocation audit history is append-only.");
            CopyTool(run.ToolInvocations[i], storedTools[i], i + 1, entity.Id);
        }
        for (var i = storedTools.Count; i < run.ToolInvocations.Count; i++)
            dbContext.BotToolInvocations.Add(ToEntity(run.ToolInvocations[i], i + 1, entity.Id));
        CopyRun(run, entity); entity.Version = expectedVersion + 1;
        return await RepositoryWrites.SaveAsync(dbContext, "bot_run_audit", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistenceWriteResult> StoreInputRenderingAsync(BotRunId runId, long expectedVersion,
        string renderingVersion, string renderingHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renderingVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(renderingHash);
        var entity = await dbContext.BotRuns.SingleOrDefaultAsync(x => x.Id == runId.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion)
            return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        if (entity.Status != "PreparingSnapshot") throw new InvalidOperationException("Bot Run input can be recorded only while preparing its snapshot.");
        entity.InputRenderingVersion = renderingVersion.Trim();
        entity.InputRenderingHash = renderingHash.Trim().ToLowerInvariant();
        entity.Version = expectedVersion + 1;
        var result = await RepositoryWrites.SaveAsync(dbContext, "bot_run_input", cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    public async Task<IReadOnlyList<BotRunId>> GetExpiredLeaseRunIdsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.BotRuns.AsNoTracking().Where(x => x.LeaseExpiresAt != null && x.LeaseExpiresAt <= UtcUnixMilliseconds.ToProvider(now) &&
            (x.Status == "PreparingSnapshot" || x.Status == "Reasoning" || x.Status == "WaitingForTool"))
            .OrderBy(x => x.LeaseExpiresAt).ThenBy(x => x.Id).Select(x => BotRunId.Parse(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PersistenceWriteResult> RecoverExpiredAsync(BotRun run, long expectedVersion,
        PendingBotRunTrigger? followUpTrigger, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var saved = await SaveAsync(run, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (saved is not PersistenceWriteResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return saved;
        }
        if (followUpTrigger is not null)
        {
            dbContext.BotRunTriggers.Add(new BotRunTriggerEntity
            {
                Id = followUpTrigger.Id.ToString(),
                TradingBotId = followUpTrigger.TradingBotId.ToString(),
                TriggerType = CanonicalEnumeration.Format(followUpTrigger.Type),
                Reason = Require(followUpTrigger.Reason),
                SourceType = followUpTrigger.SourceType,
                SourceId = followUpTrigger.SourceId,
                OccurredAt = UtcUnixMilliseconds.ToProvider(followUpTrigger.OccurredAt),
                CreatedAt = UtcUnixMilliseconds.ToProvider(followUpTrigger.CreatedAt),
            });
            try { await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
            catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
                return new PersistenceWriteResult.UniquenessConflict("runtime_recovery_trigger");
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return saved;
    }

    private static BotRun ToDomain(BotRunEntity e, IReadOnlyList<BotRunTriggerEntity> triggers, IReadOnlyList<BotToolInvocationEntity> tools)
    {
        var finish = e.FinishStatus is null ? null : new FinishResult(CanonicalEnumeration.Parse<FinishStatus>(e.FinishStatus), e.FinishSummary!,
            e.RequestedNextRunAt is null ? null : UtcUnixMilliseconds.FromProvider(e.RequestedNextRunAt.Value), e.RequestedWakeReason);
        return BotRun.Rehydrate(new BotRunState(BotRunId.Parse(e.Id), TradingBotId.Parse(e.TradingBotId),
            TradingBotConfigurationVersionId.Parse(e.ConfigurationVersionId), PortfolioDecisionSnapshotId.Parse(e.PortfolioSnapshotId!),
            CanonicalEnumeration.Parse<BotRunStatus>(e.Status), UtcUnixMilliseconds.FromProvider(e.StartedAt),
            e.CompletedAt is null ? null : UtcUnixMilliseconds.FromProvider(e.CompletedAt.Value), e.LeaseOwner,
            e.LeaseExpiresAt is null ? null : UtcUnixMilliseconds.FromProvider(e.LeaseExpiresAt.Value), finish,
            e.AcceptedNextRunAt is null ? null : UtcUnixMilliseconds.FromProvider(e.AcceptedNextRunAt.Value), DeserializeUsage(e.UsageJson),
            triggers.Select((x, i) => new BotRunTriggerState(i + 1, BotRunTriggerId.Parse(x.Id), CanonicalEnumeration.Parse<BotRunTriggerType>(x.TriggerType), x.Reason, UtcUnixMilliseconds.FromProvider(x.OccurredAt), x.SourceId)).ToArray(),
            tools.Select(x => new ToolInvocationState(x.SequenceNumber, ToolInvocationId.Parse(x.Id), x.ToolName, x.ArgumentsJson,
                x.Status == "Started" ? ToolInvocationStatus.Running : CanonicalEnumeration.Parse<ToolInvocationStatus>(x.Status), UtcUnixMilliseconds.FromProvider(x.StartedAt),
                x.CompletedAt is null ? null : UtcUnixMilliseconds.FromProvider(x.CompletedAt.Value), x.ResultJson ?? x.ResultArtifactId, x.ErrorCode ?? x.ErrorDetail,
                x.UsageJson is null ? null : DeserializeUsage(x.UsageJson))).ToArray(), e.ModelTranscriptSchemaVersion, e.ModelTranscriptJson,
            e.InputRenderingVersion, e.InputRenderingHash, e.TerminalReason, e.Version));
    }

    private static void CopyRun(BotRun run, BotRunEntity e)
    {
        e.Status = CanonicalEnumeration.Format(run.Status); e.CompletedAt = run.CompletedAt is null ? null : UtcUnixMilliseconds.ToProvider(run.CompletedAt.Value);
        e.LeaseOwner = run.LeaseOwner; e.LeaseExpiresAt = run.LeaseExpiresAt is null ? null : UtcUnixMilliseconds.ToProvider(run.LeaseExpiresAt.Value);
        e.FinishStatus = run.FinishResult is null ? null : CanonicalEnumeration.Format(run.FinishResult.Status); e.FinishSummary = run.FinishResult?.Summary;
        e.RequestedNextRunAt = run.RequestedNextRunAt is null ? null : UtcUnixMilliseconds.ToProvider(run.RequestedNextRunAt.Value);
        e.RequestedWakeReason = run.FinishResult?.WakeReason; e.AcceptedNextRunAt = run.AcceptedNextRunAt is null ? null : UtcUnixMilliseconds.ToProvider(run.AcceptedNextRunAt.Value);
        e.TerminalReason = run.TerminalReason; e.UsageJson = SerializeUsage(run.Usage); e.ModelTranscriptSchemaVersion = run.ModelTranscriptSchemaVersion;
        e.ModelTranscriptJson = run.ModelTranscriptJson; e.InputRenderingVersion = run.InputRenderingVersion; e.InputRenderingHash = run.InputRenderingHash;
    }

    private static BotToolInvocationEntity ToEntity(ToolInvocation tool, int sequence, string runId) { var e = new BotToolInvocationEntity(); CopyTool(tool, e, sequence, runId); return e; }
    private static void CopyTool(ToolInvocation tool, BotToolInvocationEntity e, int sequence, string runId)
    {
        e.Id = tool.Id.ToString(); e.BotRunId = runId; e.SequenceNumber = sequence; e.ToolName = tool.ToolName; e.ToolSchemaVersion = 1;
        e.ArgumentsJson = tool.Arguments; e.Status = tool.Status == ToolInvocationStatus.Running ? "Started" : CanonicalEnumeration.Format(tool.Status);
        e.StartedAt = UtcUnixMilliseconds.ToProvider(tool.StartedAt); e.CompletedAt = tool.CompletedAt is null ? null : UtcUnixMilliseconds.ToProvider(tool.CompletedAt.Value);
        e.ResultJson = tool.ResultReference; e.ErrorCode = tool.Error; e.UsageJson = tool.Usage is null ? null : SerializeUsage(tool.Usage);
    }

    private static string SerializeUsage(Usage usage) => CanonicalJsonSerializer.Serialize(UsageSchemaVersion,
        new UsageDto(usage.Elapsed.Ticks, usage.Tokens, CanonicalDecimal.Format(usage.Cost.Amount), usage.Cost.Currency.Code, usage.ToolCalls, usage.ResearchRequests, usage.Proposals));
    private static Usage DeserializeUsage(string json) { var x = CanonicalJsonSerializer.Deserialize<UsageDto>(UsageSchemaVersion, json); return new Usage(TimeSpan.FromTicks(x.ElapsedTicks), x.Tokens, new Money(CanonicalDecimal.Parse(x.Cost), new Currency(x.Currency)), x.ToolCalls, x.ResearchRequests, x.Proposals); }
    private sealed record UsageDto(long ElapsedTicks, long Tokens, string Cost, string Currency, int ToolCalls, int ResearchRequests, int Proposals);
    private static string Require(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
}

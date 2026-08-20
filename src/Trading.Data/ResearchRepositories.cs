using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;

namespace Trading.Data;

public sealed class ResearchRequestDecisionRepository(TradingDbContext db) : IResearchRequestDecisionRepository
{
    public async Task<ResearchRequestPersistenceDecision> DecideAsync(AuthorizedResearchRequest candidate,
        ResearchPrincipal principal, DateTimeOffset now, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await db.Database.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection())
            .BeginTransaction(IsolationLevel.Serializable, deferred: false);
        await db.Database.UseTransactionAsync(transaction, token).ConfigureAwait(false);
        try
        {
            var request = candidate.Request;
            if (candidate.RefreshReportId is not null)
            {
                var refresh = await db.ResearchReports.AsNoTracking()
                    .Join(db.ResearchRequests.AsNoTracking(), r => r.ResearchRequestId, q => q.Id, (r, q) => new { r, q })
                    .SingleOrDefaultAsync(x => x.r.Id == candidate.RefreshReportId.ToString(), token).ConfigureAwait(false);
                if (refresh is null || !Authorized(principal, refresh.r.Visibility, refresh.q.RequestingBotId, refresh.q.RequestJson) ||
                    !string.Equals(refresh.r.SubjectId ?? refresh.r.SubjectType, request.Subject, StringComparison.Ordinal))
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
                    return new ResearchRequestPersistenceDecision.RefreshUnauthorized();
                }
            }
            else
            {
                var at = UtcUnixMilliseconds.ToProvider(now);
                var reports = await db.ResearchReports.AsNoTracking()
                    .Join(db.ResearchRequests.AsNoTracking(), r => r.ResearchRequestId, q => q.Id, (r, q) => new { r, q })
                    .Where(x => x.q.NormalizedResearchKey == request.NormalizedResearchKey && x.r.Status == "Published" && x.r.ExpiresAt >= at)
                    .OrderByDescending(x => x.r.GeneratedAt).ThenByDescending(x => x.r.VersionNumber).ToListAsync(token).ConfigureAwait(false);
                var reusable = reports.FirstOrDefault(x => Authorized(principal, x.r.Visibility, x.q.RequestingBotId, x.q.RequestJson));
                if (reusable is not null)
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
                    return new ResearchRequestPersistenceDecision.Reused(ResearchReportId.Parse(reusable.r.Id));
                }

                var active = await db.ResearchRequests.AsNoTracking()
                    .Where(x => x.NormalizedResearchKey == request.NormalizedResearchKey &&
                        (x.Status == "Queued" || x.Status == "Running" || x.Status == "WaitingForTool"))
                    .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(token).ConfigureAwait(false);
                if (active is not null && Authorized(principal, active.Visibility, active.RequestingBotId, active.RequestJson))
                {
                    var botId = request.RequestingBotId.ToString();
                    var existing = await db.ResearchSubscriptions.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.ResearchRequestId == active.Id && x.TradingBotId == botId, token).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                        await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
                        return new ResearchRequestPersistenceDecision.Subscribed(ResearchRequestId.Parse(active.Id), ResearchSubscriptionId.Parse(existing.Id));
                    }
                    db.ResearchSubscriptions.Add(new ResearchSubscriptionEntity
                    {
                        Id = candidate.SubscriptionId.ToString(),
                        ResearchRequestId = active.Id,
                        TradingBotId = botId,
                        SubscribedAt = UtcUnixMilliseconds.ToProvider(now),
                        NotificationStatus = "Pending"
                    });
                    await db.SaveChangesAsync(token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
                    return new ResearchRequestPersistenceDecision.Subscribed(ResearchRequestId.Parse(active.Id), candidate.SubscriptionId);
                }
            }

            var entity = ResearchPersistenceMapper.ToEntity(request, 1);
            entity.RequestJson = ResearchPersistenceMapper.WithDecisionMetadata(entity.RequestJson,
                candidate.CanonicalSpecification, candidate.RefreshReportId);
            db.ResearchRequests.Add(entity);
            db.ResearchSubscriptions.Add(ResearchPersistenceMapper.ToEntity(request.Id, request.Subscriptions.Single()));
            await db.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
            return new ResearchRequestPersistenceDecision.Queued(request.Id, candidate.SubscriptionId);
        }
        catch
        {
            try { await transaction.RollbackAsync(token).ConfigureAwait(false); } catch (InvalidOperationException) { }
            await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool Authorized(ResearchPrincipal principal, string visibility, string? owner, string requestJson) =>
        principal.Kind == ResearchPrincipalKind.Administrator ||
        visibility == "Shared" && principal.Kind == ResearchPrincipalKind.TradingBot ||
        visibility == "BotPrivate" && principal.Id == owner ||
        visibility == "Restricted" && principal.RestrictedGroups.Contains(ResearchPersistenceMapper.RestrictedGroup(requestJson)!, StringComparer.Ordinal);
}

public sealed class ResearchRequestRepository(TradingDbContext db) : IResearchRequestRepository
{
    public async Task<ResearchRequest?> GetAsync(ResearchRequestId id, CancellationToken token)
    {
        var entity = await db.ResearchRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), token).ConfigureAwait(false);
        if (entity is null) return null;
        var subscriptions = await db.ResearchSubscriptions.AsNoTracking().Where(x => x.ResearchRequestId == entity.Id)
            .OrderBy(x => x.SubscribedAt).ThenBy(x => x.Id).ToListAsync(token).ConfigureAwait(false);
        return ResearchPersistenceMapper.ToDomain(entity, subscriptions);
    }

    public async Task<PersistenceWriteResult> AddAsync(ResearchRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        db.ResearchRequests.Add(ResearchPersistenceMapper.ToEntity(request, 1));
        foreach (var subscription in request.Subscriptions) db.ResearchSubscriptions.Add(ResearchPersistenceMapper.ToEntity(request.Id, subscription));
        return await RepositoryWrites.SaveAsync(db, "research_request", token).ConfigureAwait(false);
    }

    public async Task<PersistenceWriteResult> SaveAsync(ResearchRequest request, long expectedVersion, CancellationToken token)
    {
        var entity = await db.ResearchRequests.SingleOrDefaultAsync(x => x.Id == request.Id.ToString(), token).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        ResearchPersistenceMapper.Copy(request, entity); entity.Version++;
        var stored = await db.ResearchSubscriptions.Where(x => x.ResearchRequestId == entity.Id).OrderBy(x => x.SubscribedAt).ThenBy(x => x.Id).ToListAsync(token).ConfigureAwait(false);
        if (stored.Count > request.Subscriptions.Count) throw new InvalidOperationException("Research subscriptions are append-only.");
        for (var i = 0; i < stored.Count; i++)
        {
            if (stored[i].Id != request.Subscriptions[i].Id.ToString()) throw new InvalidOperationException("Research subscriptions are append-only.");
            stored[i].NotificationStatus = CanonicalEnumeration.Format(request.Subscriptions[i].NotificationStatus);
        }
        for (var i = stored.Count; i < request.Subscriptions.Count; i++) db.ResearchSubscriptions.Add(ResearchPersistenceMapper.ToEntity(request.Id, request.Subscriptions[i]));
        return await RepositoryWrites.SaveAsync(db, "research_request", token).ConfigureAwait(false);
    }

    public async Task<ResearchClaimResult> TryClaimQueuedAsync(ResearchRequestId requestId, ResearchAttemptClaim claim, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        try
        {
            var request = await db.ResearchRequests.SingleOrDefaultAsync(x => x.Id == requestId.ToString(), token).ConfigureAwait(false);
            if (request is null || request.Status != "Queued") return new ResearchClaimResult.ConcurrencyConflict();
            var active = await db.ResearchRuns.AsNoTracking().Where(x => x.ResearchRequestId == request.Id &&
                (x.Status == "Pending" || x.Status == "Running" || x.Status == "WaitingForTool"))
                .Select(x => x.Id).SingleOrDefaultAsync(token).ConfigureAwait(false);
            if (active is not null) return new ResearchClaimResult.ActiveAttemptConflict(ResearchRunAttemptId.Parse(active));
            var attempt = claim.Attempt;
            if (attempt.RequestId != requestId) throw new ArgumentException("Attempt must belong to the claimed request.", nameof(claim));
            request.Status = "Running"; request.StartedAt = UtcUnixMilliseconds.ToProvider(attempt.CreatedAt); request.Version++;
            var started = ResearchRunAttempt.Rehydrate(new ResearchRunAttemptState(attempt.Id, attempt.RequestId, attempt.Versions,
                attempt.Budget, ResearchRunAttemptStatus.Running, attempt.CreatedAt, attempt.CreatedAt, null, null, null));
            db.ResearchRuns.Add(ResearchPersistenceMapper.ToEntity(started, claim.AttemptNumber, 1));
            await db.SaveChangesAsync(token).ConfigureAwait(false); await transaction.CommitAsync(token).ConfigureAwait(false);
            return new ResearchClaimResult.Acquired(started);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            await transaction.RollbackAsync(token).ConfigureAwait(false); db.ChangeTracker.Clear();
            var active = await db.ResearchRuns.AsNoTracking().Where(x => x.ResearchRequestId == requestId.ToString() &&
                (x.Status == "Pending" || x.Status == "Running" || x.Status == "WaitingForTool")).Select(x => x.Id).FirstOrDefaultAsync(token).ConfigureAwait(false);
            return new ResearchClaimResult.ActiveAttemptConflict(active is null ? null : ResearchRunAttemptId.Parse(active));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(token).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new ResearchClaimResult.ConcurrencyConflict();
        }
    }
}

public sealed class ResearchRunAttemptRepository(TradingDbContext db) : IResearchRunAttemptRepository
{
    public async Task<ResearchRunAttempt?> GetAsync(ResearchRunAttemptId id, CancellationToken token)
    {
        var entity = await db.ResearchRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), token).ConfigureAwait(false);
        return entity is null ? null : ResearchPersistenceMapper.ToDomain(entity);
    }
    public async Task<PersistenceWriteResult> SaveAsync(ResearchRunAttempt attempt, long expectedVersion, CancellationToken token)
    {
        var entity = await db.ResearchRuns.SingleOrDefaultAsync(x => x.Id == attempt.Id.ToString(), token).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        ResearchPersistenceMapper.Copy(attempt, entity); entity.Version++;
        return await RepositoryWrites.SaveAsync(db, "research_attempt", token).ConfigureAwait(false);
    }
    public async Task<PersistenceWriteResult> AppendToolAuditAsync(ResearchToolAudit audit, CancellationToken token)
    {
        db.ResearchToolInvocations.Add(new ResearchToolInvocationEntity
        {
            Id = audit.Id,
            ResearchRunId = audit.AttemptId.ToString(),
            SequenceNumber = audit.SequenceNumber,
            ToolName = audit.ToolName,
            ToolSchemaVersion = audit.SchemaVersion,
            ArgumentsJson = audit.ArgumentsJson,
            Status = audit.Status,
            StartedAt = UtcUnixMilliseconds.ToProvider(audit.StartedAt),
            CompletedAt = audit.CompletedAt is null ? null : UtcUnixMilliseconds.ToProvider(audit.CompletedAt.Value),
            ResultJson = audit.ResultJson,
            ErrorCode = audit.ErrorCode,
            ErrorDetail = audit.ErrorDetail,
            UsageJson = audit.UsageJson
        });
        var result = await RepositoryWrites.SaveAsync(db, "research_tool_audit", token).ConfigureAwait(false);
        db.ChangeTracker.Clear();
        return result;
    }
    public async Task<IReadOnlyList<ResearchToolAudit>> GetToolAuditAsync(ResearchRunAttemptId id, CancellationToken token) =>
        await db.ResearchToolInvocations.AsNoTracking().Where(x => x.ResearchRunId == id.ToString()).OrderBy(x => x.SequenceNumber)
            .Select(x => new ResearchToolAudit(x.Id, id, x.SequenceNumber, x.ToolName, x.ToolSchemaVersion, x.ArgumentsJson, x.Status,
                UtcUnixMilliseconds.FromProvider(x.StartedAt), x.CompletedAt == null ? null : UtcUnixMilliseconds.FromProvider(x.CompletedAt.Value),
                x.ResultJson, x.ErrorCode, x.ErrorDetail, x.UsageJson)).ToListAsync(token).ConfigureAwait(false);
}

public sealed class ResearchReportRepository(TradingDbContext db) : IResearchReportRepository
{
    public async Task<ResearchReport?> GetAsync(ResearchReportId id, CancellationToken token) => await ResearchPersistenceMapper.LoadReportAsync(db, id.ToString(), token).ConfigureAwait(false);
    public async Task<PersistenceWriteResult> PublishAsync(ResearchReport report, ResearchRunAttemptId attemptId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(report);
        db.ResearchReports.Add(ResearchPersistenceMapper.ToEntity(report, attemptId));
        var sequence = 0;
        foreach (var source in report.Provenance.Sources) db.ResearchReportSources.Add(ResearchPersistenceMapper.ToEntity(report.Id, ++sequence, source));
        return await RepositoryWrites.SaveAsync(db, "research_report", token).ConfigureAwait(false);
    }

    public async Task<ResearchReport> PublishCompletedAsync(ResearchPublication publication, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(publication);
        await db.Database.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection())
            .BeginTransaction(IsolationLevel.Serializable, deferred: false);
        await db.Database.UseTransactionAsync(transaction, token).ConfigureAwait(false);
        try
        {
            var attemptId = publication.Attempt.Id.ToString();
            var existing = await db.ResearchReports.AsNoTracking().SingleOrDefaultAsync(x => x.ResearchRunId == attemptId, token).ConfigureAwait(false);
            if (existing is not null)
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
                await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
                return (await ResearchPersistenceMapper.LoadReportAsync(db, existing.Id, token).ConfigureAwait(false))!;
            }
            var requestEntity = await db.ResearchRequests.SingleAsync(x => x.Id == publication.Request.Id.ToString(), token).ConfigureAwait(false);
            var attemptEntity = await db.ResearchRuns.AsNoTracking().SingleAsync(x => x.Id == attemptId, token).ConfigureAwait(false);
            if (requestEntity.Status != "Running" || attemptEntity.Status != "Completed" || attemptEntity.TerminalReason != "research.success")
                throw new InvalidOperationException("Publication requires a running request and successfully completed attempt.");

            ResearchReportEntity? predecessor = null;
            string seriesId; int version;
            if (publication.RefreshReportId is null)
            {
                seriesId = publication.ReportId.ToString(); version = 1;
            }
            else
            {
                predecessor = await db.ResearchReports.SingleAsync(x => x.Id == publication.RefreshReportId.ToString(), token).ConfigureAwait(false);
                seriesId = predecessor.ReportSeriesId;
                var latest = await db.ResearchReports.Where(x => x.ReportSeriesId == seriesId).OrderByDescending(x => x.VersionNumber).FirstAsync(token).ConfigureAwait(false);
                predecessor = latest; version = latest.VersionNumber + 1;
            }
            var report = new ResearchReport(publication.ReportId, seriesId, version, publication.Request.Id,
                publication.Request.Subject, publication.Request.Question, publication.Request.Visibility,
                publication.DataCutoff,
                publication.GeneratedAt, publication.ExpiresAt, predecessor is null ? null : ResearchReportId.Parse(predecessor.Id),
                publication.CanonicalContent, publication.ContentHash, publication.Provenance, publication.GeneratorMetadata);
            db.ResearchReports.Add(ResearchPersistenceMapper.ToEntity(report, publication.Attempt.Id));
            var sequence = 0; foreach (var source in report.Provenance.Sources)
                db.ResearchReportSources.Add(ResearchPersistenceMapper.ToEntity(report.Id, ++sequence, source));
            if (predecessor is not null) predecessor.Status = "Superseded";
            requestEntity.Status = "Completed"; requestEntity.CompletedAt = UtcUnixMilliseconds.ToProvider(publication.GeneratedAt);
            requestEntity.ResultReportId = report.Id.ToString(); requestEntity.Version++;
            await db.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false);
            return report;
        }
        catch
        {
            try { await transaction.RollbackAsync(token).ConfigureAwait(false); } catch (InvalidOperationException) { }
            await db.Database.UseTransactionAsync(null, token).ConfigureAwait(false); db.ChangeTracker.Clear(); throw;
        }
    }
}

public sealed class ResearchReportCatalogQueries(TradingDbContext db) : IResearchReportCatalogQueries
{
    public async Task<IReadOnlyList<ResearchReportSummary>> SearchAsync(ResearchReportSearch search, CancellationToken token)
    {
        if (search.Offset < 0 || search.Size is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(search));
        var at = UtcUnixMilliseconds.ToProvider(search.At); var principal = search.Principal;
        var query = db.ResearchReports.AsNoTracking().Join(db.ResearchRequests.AsNoTracking(), r => r.ResearchRequestId, q => q.Id, (r, q) => new { r, q });
        if (search.Subject is not null) query = query.Where(x => x.r.SubjectId == search.Subject || x.r.SubjectType == search.Subject);
        if (search.NormalizedResearchKey is not null) query = query.Where(x => x.q.NormalizedResearchKey == search.NormalizedResearchKey);
        if (search.FreshOnly) query = query.Where(x => x.r.Status == "Published" && x.r.ExpiresAt >= at);
        if (principal.Kind != ResearchPrincipalKind.Administrator)
        {
            var id = principal.Id;
            query = query.Where(x => x.r.Visibility == "Shared" ||
                (x.r.Visibility == "BotPrivate" && x.q.RequestingBotId == id) ||
                x.r.Visibility == "Restricted");
        }
        var candidates = await query.OrderByDescending(x => x.r.GeneratedAt).ThenBy(x => x.r.ReportSeriesId).ThenByDescending(x => x.r.VersionNumber).ThenBy(x => x.r.Id)
            .ToListAsync(token).ConfigureAwait(false);
        return candidates.Where(x => Authorized(search.Principal, x.r.Visibility, x.q.RequestingBotId, x.q.RequestJson))
            .Skip(search.Offset).Take(search.Size).Select(x => new ResearchReportSummary(ResearchReportId.Parse(x.r.Id), x.r.ReportSeriesId,
                x.r.VersionNumber, x.r.SubjectId ?? x.r.SubjectType, CanonicalEnumeration.Parse<ResearchReportStatus>(x.r.Status),
                UtcUnixMilliseconds.FromProvider(x.r.DataCutoff), UtcUnixMilliseconds.FromProvider(x.r.GeneratedAt),
                UtcUnixMilliseconds.FromProvider(x.r.ExpiresAt!.Value), x.r.Status == "Published" && x.r.ExpiresAt >= at)).ToArray();
    }
    public Task<ResearchReport?> GetAuthorizedAsync(ResearchPrincipal principal, ResearchReportId id, CancellationToken token) => Get(principal, x => x.Id == id.ToString(), token);
    public Task<ResearchReport?> GetAuthorizedVersionAsync(ResearchPrincipal principal, string seriesId, int version, CancellationToken token) => Get(principal, x => x.ReportSeriesId == seriesId && x.VersionNumber == version, token);
    private async Task<ResearchReport?> Get(ResearchPrincipal principal, System.Linq.Expressions.Expression<Func<ResearchReportEntity, bool>> predicate, CancellationToken token)
    {
        var row = await db.ResearchReports.AsNoTracking().Where(predicate).Join(db.ResearchRequests.AsNoTracking(), r => r.ResearchRequestId, q => q.Id, (r, q) => new { r, q }).SingleOrDefaultAsync(token).ConfigureAwait(false);
        if (row is null || !Authorized(principal, row.r.Visibility, row.q.RequestingBotId, row.q.RequestJson)) return null;
        return await ResearchPersistenceMapper.LoadReportAsync(db, row.r.Id, token).ConfigureAwait(false);
    }
    private static bool Authorized(ResearchPrincipal principal, string visibility, string? owner, string requestJson) => principal.Kind == ResearchPrincipalKind.Administrator ||
        visibility == "Shared" && principal.Kind == ResearchPrincipalKind.TradingBot || visibility == "BotPrivate" && principal.Id == owner ||
        visibility == "Restricted" && principal.RestrictedGroups.Contains(ResearchPersistenceMapper.RestrictedGroup(requestJson)!, StringComparer.Ordinal);
}

internal static class ResearchPersistenceMapper
{
    private const int Schema = 1;
    internal static ResearchRequestEntity ToEntity(ResearchRequest x, long version) { var e = new ResearchRequestEntity { Id = x.Id.ToString(), Version = version }; Copy(x, e); return e; }
    internal static void Copy(ResearchRequest x, ResearchRequestEntity e)
    {
        var existing = string.IsNullOrEmpty(e.RequestJson) ? null : CanonicalJsonSerializer.Deserialize<RequestDto>(Schema, e.RequestJson);
        e.SubjectType = "Instrument"; e.SubjectId = x.Subject; e.Question = x.Question; e.NormalizedResearchKey = x.NormalizedResearchKey;
        e.AsOf = UtcUnixMilliseconds.ToProvider(x.AsOf); e.Status = CanonicalEnumeration.Format(x.Status); e.Visibility = CanonicalEnumeration.Format(x.Visibility);
        e.RequestingBotId = x.RequestingBotId.ToString(); e.FreshnessRequirementJson = CanonicalJsonSerializer.Serialize(Schema, new FreshnessDto(x.FreshnessRequirement.SourceAsOf, x.FreshnessRequirement.RetrievedAt, x.FreshnessRequirement.MaximumAge.Ticks));
        e.RequestJson = CanonicalJsonSerializer.Serialize(Schema, new RequestDto(x.HasPrivateInputs, x.AuthorizedSubscriberIds.Select(i => i.ToString()).Order(StringComparer.Ordinal).ToArray(), x.RestrictedGroup, existing?.CanonicalSpecification, existing?.RefreshReportId));
        e.StartedAt = x.StartedAt is null ? null : UtcUnixMilliseconds.ToProvider(x.StartedAt.Value); e.CompletedAt = x.CompletedAt is null ? null : UtcUnixMilliseconds.ToProvider(x.CompletedAt.Value);
        e.ResultReportId = x.ResultReportId?.ToString(); e.CreatedAt = UtcUnixMilliseconds.ToProvider(x.RequestedAt);
    }
    internal static string WithDecisionMetadata(string requestJson, string specification, ResearchReportId? refreshReportId)
    {
        var request = CanonicalJsonSerializer.Deserialize<RequestDto>(Schema, requestJson);
        return CanonicalJsonSerializer.Serialize(Schema, request with { CanonicalSpecification = specification, RefreshReportId = refreshReportId?.ToString() });
    }
    internal static ResearchSubscriptionEntity ToEntity(ResearchRequestId requestId, ResearchSubscription x) => new() { Id = x.Id.ToString(), ResearchRequestId = requestId.ToString(), TradingBotId = x.TradingBotId.ToString(), SubscribedAt = UtcUnixMilliseconds.ToProvider(x.SubscribedAt), NotificationStatus = CanonicalEnumeration.Format(x.NotificationStatus) };
    internal static ResearchRequest ToDomain(ResearchRequestEntity e, IReadOnlyList<ResearchSubscriptionEntity> subscriptions)
    {
        var freshness = CanonicalJsonSerializer.Deserialize<FreshnessDto>(Schema, e.FreshnessRequirementJson); var request = CanonicalJsonSerializer.Deserialize<RequestDto>(Schema, e.RequestJson);
        return ResearchRequest.Rehydrate(new ResearchRequestState(ResearchRequestId.Parse(e.Id), TradingBotId.Parse(e.RequestingBotId!), e.SubjectId ?? e.SubjectType, e.Question,
            UtcUnixMilliseconds.FromProvider(e.AsOf), CanonicalEnumeration.Parse<ResearchRequestStatus>(e.Status), CanonicalEnumeration.Parse<ResearchVisibility>(e.Visibility),
            new DataFreshness(freshness.SourceAsOf, freshness.RetrievedAt, TimeSpan.FromTicks(freshness.MaximumAgeTicks)), e.NormalizedResearchKey,
            UtcUnixMilliseconds.FromProvider(e.CreatedAt), e.StartedAt is null ? null : UtcUnixMilliseconds.FromProvider(e.StartedAt.Value), e.CompletedAt is null ? null : UtcUnixMilliseconds.FromProvider(e.CompletedAt.Value),
            e.ResultReportId is null ? null : ResearchReportId.Parse(e.ResultReportId), request.HasPrivateInputs, request.AuthorizedSubscribers.Select(TradingBotId.Parse).ToArray(), request.RestrictedGroup,
            subscriptions.Select(s => new ResearchSubscriptionState(ResearchSubscriptionId.Parse(s.Id), TradingBotId.Parse(s.TradingBotId), UtcUnixMilliseconds.FromProvider(s.SubscribedAt), CanonicalEnumeration.Parse<ResearchNotificationStatus>(s.NotificationStatus))).ToArray()));
    }
    internal static ResearchRunEntity ToEntity(ResearchRunAttempt x, int number, long version) { var e = new ResearchRunEntity { Id = x.Id.ToString(), ResearchRequestId = x.RequestId.ToString(), AttemptNumber = number, Version = version }; Copy(x, e); return e; }
    internal static void Copy(ResearchRunAttempt x, ResearchRunEntity e)
    {
        e.Status = x.Status == ResearchRunAttemptStatus.Created ? "Pending" : CanonicalEnumeration.Format(x.Status); e.ModelConfigurationJson = CanonicalJsonSerializer.Serialize(Schema, new AttemptDto(x.Versions.ModelProvider, x.Versions.ModelId, x.Versions.ModelVersion,
            x.Budget.WallClock.Ticks, x.Budget.TokenLimit, x.Budget.CostLimit.Amount, x.Budget.CostLimit.Currency.Code, x.Budget.ToolCallLimit, x.Budget.DocumentLimit, x.Budget.RetainedByteLimit, x.Budget.ConsecutiveFailureLimit, x.CreatedAt));
        e.PromptVersion = x.Versions.PromptVersion; e.ToolSetVersion = x.Versions.ToolSetVersion; e.ReportSchemaVersion = x.Versions.ReportSchemaVersion;
        e.StartedAt = UtcUnixMilliseconds.ToProvider(x.StartedAt ?? x.CreatedAt); e.CompletedAt = x.CompletedAt is null ? null : UtcUnixMilliseconds.ToProvider(x.CompletedAt.Value); e.TerminalReason = x.ResultCode;
        e.UsageJson = CanonicalJsonSerializer.Serialize(Schema, x.Usage is null
            ? new UsageDto(false, 0, 0, 0, "USD", 0, 0, 0, 0)
            : new UsageDto(true, x.Usage.Elapsed.Ticks, x.Usage.Tokens, x.Usage.Cost.Amount, x.Usage.Cost.Currency.Code, x.Usage.ToolCalls, x.Usage.Documents, x.Usage.RetainedBytes, x.Usage.ConsecutiveFailures));
    }
    internal static ResearchRunAttempt ToDomain(ResearchRunEntity e)
    {
        var a = CanonicalJsonSerializer.Deserialize<AttemptDto>(Schema, e.ModelConfigurationJson); var u = CanonicalJsonSerializer.Deserialize<UsageDto>(Schema, e.UsageJson);
        var versions = new ResearchVersionPins(a.Provider, a.ModelId, a.ModelVersion, e.PromptVersion, e.ToolSetVersion, e.ReportSchemaVersion);
        var budget = new ResearchBudget(TimeSpan.FromTicks(a.WallClockTicks), a.TokenLimit, new Money(a.CostLimit, new Currency(a.Currency)), a.ToolCallLimit, a.DocumentLimit, a.RetainedByteLimit, a.FailureLimit);
        var status = e.Status == "Pending" ? ResearchRunAttemptStatus.Created : CanonicalEnumeration.Parse<ResearchRunAttemptStatus>(e.Status);
        return ResearchRunAttempt.Rehydrate(new ResearchRunAttemptState(ResearchRunAttemptId.Parse(e.Id), ResearchRequestId.Parse(e.ResearchRequestId), versions, budget, status, a.CreatedAt,
            status == ResearchRunAttemptStatus.Created ? null : UtcUnixMilliseconds.FromProvider(e.StartedAt), e.CompletedAt is null ? null : UtcUnixMilliseconds.FromProvider(e.CompletedAt.Value),
            !u.HasValue ? null : new ResearchUsage(TimeSpan.FromTicks(u.ElapsedTicks), u.Tokens, new Money(u.Cost, new Currency(u.Currency)), u.ToolCalls, u.Documents, u.RetainedBytes, u.Failures), e.TerminalReason));
    }
    internal static ResearchReportEntity ToEntity(ResearchReport x, ResearchRunAttemptId attemptId) => new() { Id = x.Id.ToString(), ReportSeriesId = x.ReportSeriesId, VersionNumber = x.VersionNumber, ResearchRequestId = x.ResearchRequestId.ToString(), ResearchRunId = attemptId.ToString(), SubjectType = "Instrument", SubjectId = x.Subject, Question = x.Question, Visibility = CanonicalEnumeration.Format(x.Visibility), DataCutoff = UtcUnixMilliseconds.ToProvider(x.DataCutoff), GeneratedAt = UtcUnixMilliseconds.ToProvider(x.GeneratedAt), ExpiresAt = UtcUnixMilliseconds.ToProvider(x.ExpiresAt), Status = CanonicalEnumeration.Format(x.Status), SupersedesReportId = x.SupersedesReportId?.ToString(), ReportSchemaVersion = x.GeneratorMetadata.ReportSchemaVersion, ContentJson = x.Content, ContentHash = x.ContentHash, GeneratorMetadataJson = CanonicalJsonSerializer.Serialize(Schema, new GeneratorDto(x.GeneratorMetadata.Model.Provider, x.GeneratorMetadata.Model.Model, x.GeneratorMetadata.Model.Temperature, x.GeneratorMetadata.Model.MaximumOutputTokens, x.GeneratorMetadata.PromptVersion, x.GeneratorMetadata.ToolSetVersion, x.GeneratorMetadata.ReportSchemaVersion)) };
    internal static ResearchReportSourceEntity ToEntity(ResearchReportId id, int sequence, SourceCitation x) => new() { Id = $"{id}:{sequence}", ResearchReportId = id.ToString(), SourceSequence = sequence, SourceType = x.Provider, StableSourceId = x.SourceIdentifier, Title = x.SourceIdentifier, PublishedAt = x.PublishedAt is null ? null : UtcUnixMilliseconds.ToProvider(x.PublishedAt.Value), RetrievedAt = UtcUnixMilliseconds.ToProvider(x.RetrievedAt), ContentHash = x.ContentHash, MetadataJson = CanonicalJsonSerializer.Serialize(Schema, new { }) };
    internal static async Task<ResearchReport?> LoadReportAsync(TradingDbContext db, string id, CancellationToken token)
    {
        var e = await db.ResearchReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token).ConfigureAwait(false); if (e is null) return null;
        var sources = await db.ResearchReportSources.AsNoTracking().Where(x => x.ResearchReportId == id).OrderBy(x => x.SourceSequence).ToListAsync(token).ConfigureAwait(false);
        var g = CanonicalJsonSerializer.Deserialize<GeneratorDto>(Schema, e.GeneratorMetadataJson);
        return ResearchReport.Rehydrate(new ResearchReportState(ResearchReportId.Parse(e.Id), e.ReportSeriesId, e.VersionNumber, ResearchRequestId.Parse(e.ResearchRequestId), e.SubjectId ?? e.SubjectType, e.Question, CanonicalEnumeration.Parse<ResearchVisibility>(e.Visibility), UtcUnixMilliseconds.FromProvider(e.DataCutoff), UtcUnixMilliseconds.FromProvider(e.GeneratedAt), UtcUnixMilliseconds.FromProvider(e.ExpiresAt!.Value), e.SupersedesReportId is null ? null : ResearchReportId.Parse(e.SupersedesReportId), e.ContentJson, e.ContentHash, new ReportProvenance(sources.Select(s => new SourceCitation(s.SourceType, s.StableSourceId ?? s.Title, s.PublishedAt is null ? null : UtcUnixMilliseconds.FromProvider(s.PublishedAt.Value), UtcUnixMilliseconds.FromProvider(s.RetrievedAt), s.ContentHash))), new GeneratorMetadata(new ModelConfiguration(g.Provider, g.Model, g.Temperature, g.MaximumTokens), g.PromptVersion, g.ToolSetVersion, g.SchemaVersion), CanonicalEnumeration.Parse<ResearchReportStatus>(e.Status)));
    }
    private sealed record FreshnessDto(DateTimeOffset SourceAsOf, DateTimeOffset RetrievedAt, long MaximumAgeTicks);
    internal static string? RestrictedGroup(string requestJson) => CanonicalJsonSerializer.Deserialize<RequestDto>(Schema, requestJson).RestrictedGroup;
    internal static ResearchReportId? RefreshReportId(string requestJson)
    {
        var value = CanonicalJsonSerializer.Deserialize<RequestDto>(Schema, requestJson).RefreshReportId;
        return value is null ? null : ResearchReportId.Parse(value);
    }
    private sealed record RequestDto(bool HasPrivateInputs, string[] AuthorizedSubscribers, string? RestrictedGroup,
        string? CanonicalSpecification = null, string? RefreshReportId = null);
    private sealed record AttemptDto(string Provider, string ModelId, string ModelVersion, long WallClockTicks, long TokenLimit, decimal CostLimit, string Currency, int ToolCallLimit, int DocumentLimit, long RetainedByteLimit, int FailureLimit, DateTimeOffset CreatedAt);
    private sealed record UsageDto(bool HasValue, long ElapsedTicks, long Tokens, decimal Cost, string Currency, int ToolCalls, int Documents, long RetainedBytes, int Failures);
    private sealed record GeneratorDto(string Provider, string Model, decimal Temperature, int MaximumTokens, string PromptVersion, string ToolSetVersion, string SchemaVersion);
}

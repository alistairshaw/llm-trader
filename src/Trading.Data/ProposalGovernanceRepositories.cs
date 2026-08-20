using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Core.Research;

namespace Trading.Data;

public sealed class HypothesisRepository(TradingDbContext db) : IHypothesisRepository
{
    public Task<Hypothesis?> GetAsync(HypothesisId id, CancellationToken token) =>
        ProposalPersistenceMapper.LoadHypothesisAsync(db, id.ToString(), token);

    public async Task<HypothesisVersion?> GetVersionAsync(HypothesisVersionId id, CancellationToken token)
    {
        var row = await db.HypothesisVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), token)
            .ConfigureAwait(false);
        if (row is null) return null;
        var evidence = await db.HypothesisEvidenceReports.AsNoTracking()
            .Where(x => x.HypothesisVersionId == row.Id).OrderBy(x => x.ResearchReportId)
            .Select(x => x.ResearchReportId).ToArrayAsync(token).ConfigureAwait(false);
        return ProposalPersistenceMapper.ToDomain(row, evidence);
    }

    public async Task<PersistenceWriteResult> AddAsync(Hypothesis hypothesis, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(hypothesis);
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var root = ProposalPersistenceMapper.ToEntity(hypothesis, Math.Max(1, hypothesis.Version));
        var currentVersionId = root.CurrentVersionId; root.CurrentVersionId = null;
        db.Hypotheses.Add(root);
        var rootResult = await RepositoryWrites.SaveAsync(db, "hypothesis_identity", token).ConfigureAwait(false);
        if (rootResult is not PersistenceWriteResult.Succeeded) return rootResult;
        AddVersions(hypothesis.Id, hypothesis.Versions);
        root.CurrentVersionId = currentVersionId;
        var result = await RepositoryWrites.SaveAsync(db, "hypothesis_identity_or_version", token).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded) await transaction.CommitAsync(token).ConfigureAwait(false);
        return result;
    }

    public async Task<PersistenceWriteResult> SaveAsync(Hypothesis hypothesis, long expectedVersion, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(hypothesis);
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var row = await db.Hypotheses.SingleOrDefaultAsync(x => x.Id == hypothesis.Id.ToString(), token).ConfigureAwait(false);
        if (row is null || row.Version != expectedVersion)
            return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, row?.Version);
        row.Name = hypothesis.Name; row.Status = CanonicalEnumeration.Format(hypothesis.Status);
        row.CurrentVersionId = hypothesis.CurrentVersionId?.ToString();
        row.UpdatedAt = hypothesis.Versions.Select(x => x.FrozenAt ?? x.CreatedAt).DefaultIfEmpty(hypothesis.CreatedAt).Max().ToUnixTimeMilliseconds();
        row.Version = hypothesis.Version;
        var existing = await db.HypothesisVersions.Where(x => x.HypothesisId == row.Id).ToDictionaryAsync(x => x.Id, token).ConfigureAwait(false);
        AddVersions(hypothesis.Id, hypothesis.Versions.Where(x => !existing.ContainsKey(x.Id.ToString())));
        foreach (var version in hypothesis.Versions.Where(x => existing.ContainsKey(x.Id.ToString()) && x.FrozenAt is not null))
        {
            var versionRow = existing[version.Id.ToString()];
            if (versionRow.FrozenAt is null) versionRow.FrozenAt = UtcUnixMilliseconds.ToProvider(version.FrozenAt!.Value);
        }
        var result = await RepositoryWrites.SaveAsync(db, "hypothesis_identity_or_version", token).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded) await transaction.CommitAsync(token).ConfigureAwait(false);
        return result;
    }

    private void AddVersions(HypothesisId hypothesisId, IEnumerable<HypothesisVersion> versions)
    {
        foreach (var version in versions)
        {
            db.HypothesisVersions.Add(ProposalPersistenceMapper.ToEntity(hypothesisId, version));
            db.HypothesisEvidenceReports.AddRange(version.EvidenceReportIds.Select(id => new HypothesisEvidenceReportEntity
            { HypothesisVersionId = version.Id.ToString(), ResearchReportId = id.ToString(), RelationshipType = "Supporting" }));
        }
    }
}

public sealed class TradeProposalRepository(TradingDbContext db) : ITradeProposalRepository
{
    public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token) =>
        ProposalPersistenceMapper.LoadProposalAsync(db, id.ToString(), token);

    public async Task<ProposalRecordResult> RecordAsync(TradeProposal proposal, string idempotencyKey, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(proposal); ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (proposal.Status != ProposalStatus.Recorded || proposal.Version != 0)
            throw new InvalidOperationException("Only a newly recorded proposal can establish an idempotency boundary.");
        var existing = await db.TradeProposals.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, token).ConfigureAwait(false);
        if (existing is not null)
        {
            var loaded = (await ProposalPersistenceMapper.LoadProposalAsync(db, existing.Id, token).ConfigureAwait(false))!;
            return existing.Id == proposal.Id.ToString() ? new ProposalRecordResult.AlreadyRecorded(loaded)
                : new ProposalRecordResult.IdempotencyConflict(TradeProposalId.Parse(existing.Id));
        }
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        db.TradeProposals.Add(ProposalPersistenceMapper.ToEntity(proposal, idempotencyKey));
        db.TradeProposalEvidenceReports.AddRange(proposal.ReportEvidence.Select(x => new TradeProposalEvidenceReportEntity
        { TradeProposalId = proposal.Id.ToString(), ResearchReportId = x.ReportId.ToString() }));
        var result = await RepositoryWrites.SaveAsync(db, "trade_proposal_idempotency", token).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded)
        {
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new ProposalRecordResult.Recorded(proposal);
        }
        var winner = await db.TradeProposals.AsNoTracking().SingleAsync(x => x.IdempotencyKey == idempotencyKey, token).ConfigureAwait(false);
        return winner.Id == proposal.Id.ToString()
            ? new ProposalRecordResult.AlreadyRecorded((await ProposalPersistenceMapper.LoadProposalAsync(db, winner.Id, token).ConfigureAwait(false))!)
            : new ProposalRecordResult.IdempotencyConflict(TradeProposalId.Parse(winner.Id));
    }

    public async Task<PersistenceWriteResult> SaveAsync(TradeProposal proposal, long expectedVersion, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var result = await SaveTrackedAsync(proposal, expectedVersion, token).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded) await transaction.CommitAsync(token).ConfigureAwait(false);
        return result;
    }

    internal async Task<PersistenceWriteResult> SaveTrackedAsync(TradeProposal proposal, long expectedVersion, CancellationToken token)
    {
        var row = await db.TradeProposals.SingleOrDefaultAsync(x => x.Id == proposal.Id.ToString(), token).ConfigureAwait(false);
        if (row is null || row.Version != expectedVersion)
            return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, row?.Version);
        row.Status = ProposalPersistenceMapper.Status(proposal.Status); row.Version = expectedVersion + 1;
        var evaluationIds = await db.GuardrailEvaluations.Where(x => x.TradeProposalId == row.Id).Select(x => x.Id).ToArrayAsync(token).ConfigureAwait(false);
        db.GuardrailEvaluations.AddRange(proposal.GuardrailEvaluations.Where(x => !evaluationIds.Contains(x.Id.ToString(), StringComparer.Ordinal)).Select(x => ProposalPersistenceMapper.ToEntity(row.Id, x)));
        var approvalIds = await db.ProposalApprovals.Where(x => x.TradeProposalId == row.Id).Select(x => x.Id).ToArrayAsync(token).ConfigureAwait(false);
        db.ProposalApprovals.AddRange(proposal.ApprovalHistory.Where(x => !approvalIds.Contains(x.Id.ToString(), StringComparer.Ordinal)).Select(x => ProposalPersistenceMapper.ToEntity(row.Id, x)));
        return await RepositoryWrites.SaveAsync(db, "proposal_audit_identity_or_sequence", token).ConfigureAwait(false);
    }
}

public sealed class CapitalReservationRepository(TradingDbContext db) : ICapitalReservationRepository
{
    public async Task<CapitalReservation?> GetAsync(CapitalReservationId id, CancellationToken token) =>
        ProposalPersistenceMapper.ToDomain(await db.CapitalReservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), token).ConfigureAwait(false));
    public async Task<CapitalReservation?> GetActiveAsync(TradeProposalId proposalId, CancellationToken token) =>
        ProposalPersistenceMapper.ToDomain(await db.CapitalReservations.AsNoTracking().SingleOrDefaultAsync(x => x.TradeProposalId == proposalId.ToString() && x.Status == "Active", token).ConfigureAwait(false));
    public async Task<IReadOnlyList<CapitalReservation>> GetActiveForPortfolioAsync(PortfolioId portfolioId, DateTimeOffset at, CancellationToken token)
    {
        var timestamp = UtcUnixMilliseconds.ToProvider(at);
        var rows = await db.CapitalReservations.AsNoTracking().Where(x => x.PortfolioId == portfolioId.ToString() && x.Status == "Active" && x.ExpiresAt > timestamp)
            .OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id).ToArrayAsync(token).ConfigureAwait(false);
        return rows.Select(x => ProposalPersistenceMapper.ToDomain(x)!).ToArray();
    }
    public Task<PersistenceWriteResult> AddAsync(CapitalReservation reservation, CancellationToken token) =>
        RepositoryWrites.AddAsync(db, ProposalPersistenceMapper.ToEntity(reservation), "active_reservation_per_proposal", token);
    public async Task<PersistenceWriteResult> SaveAsync(CapitalReservation reservation, long expectedVersion, CancellationToken token)
    {
        var row = await db.CapitalReservations.SingleOrDefaultAsync(x => x.Id == reservation.Id.ToString(), token).ConfigureAwait(false);
        if (row is null || row.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, row?.Version);
        ProposalPersistenceMapper.Copy(reservation, row); row.Version = expectedVersion + 1;
        return await RepositoryWrites.SaveAsync(db, "active_reservation_per_proposal", token).ConfigureAwait(false);
    }
    public async Task<int> ExpireAsync(PortfolioId portfolioId, DateTimeOffset at, CancellationToken token)
    {
        var timestamp = UtcUnixMilliseconds.ToProvider(at);
        return await db.CapitalReservations.Where(x => x.PortfolioId == portfolioId.ToString() && x.Status == "Active" && x.ExpiresAt <= timestamp)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Expired").SetProperty(x => x.ReleasedAt, timestamp).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);
    }
}

public sealed class ProposalGovernanceTransactionRepository(TradingDbContext db) : IProposalGovernanceTransactionRepository
{
    public async Task<PersistenceWriteResult> SaveDecisionAndReservationAsync(TradeProposal proposal,
        long expectedProposalVersion, CapitalReservation? reservation, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var result = await new TradeProposalRepository(db).SaveTrackedAsync(proposal, expectedProposalVersion, token).ConfigureAwait(false);
        if (result is not PersistenceWriteResult.Succeeded) return result;
        if (reservation is not null)
        {
            db.CapitalReservations.Add(ProposalPersistenceMapper.ToEntity(reservation));
            result = await RepositoryWrites.SaveAsync(db, "active_reservation_per_proposal", token).ConfigureAwait(false);
            if (result is not PersistenceWriteResult.Succeeded) return result;
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return result;
    }
}

internal static class ProposalPersistenceMapper
{
    private const int Schema = 1;
    internal static HypothesisEntity ToEntity(Hypothesis value, long version) => new() { Id = value.Id.ToString(), Name = value.Name, Status = CanonicalEnumeration.Format(value.Status), CurrentVersionId = value.CurrentVersionId?.ToString(), CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt), UpdatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt), Version = version };
    internal static HypothesisVersionEntity ToEntity(HypothesisId hypothesisId, HypothesisVersion value)
    {
        var json = CanonicalJsonSerializer.Serialize(Schema, new HypothesisDto(value.Claim, value.UniverseDefinition.AssetClasses.ToArray(), value.UniverseDefinition.Markets.ToArray(), value.UniverseDefinition.Currencies.Select(x => x.Code).ToArray(), value.InputDefinitions, value.SignalRules, value.EvaluationPlan, value.SuccessCriteria, value.InvalidationCriteria));
        return new HypothesisVersionEntity { Id = value.Id.ToString(), HypothesisId = hypothesisId.ToString(), VersionNumber = value.VersionNumber, SpecificationSchemaVersion = Schema, SpecificationJson = json, ContentHash = CanonicalJsonSerializer.Sha256(json), CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt), FrozenAt = value.FrozenAt is null ? null : UtcUnixMilliseconds.ToProvider(value.FrozenAt.Value) };
    }
    internal static HypothesisVersion ToDomain(HypothesisVersionEntity row, IReadOnlyList<string> evidence)
    {
        var dto = CanonicalJsonSerializer.Deserialize<HypothesisDto>(Schema, row.SpecificationJson);
        var versionId = HypothesisVersionId.Parse(row.Id);
        return HypothesisVersion.Rehydrate(new HypothesisVersionState(versionId, row.VersionNumber, dto.Claim,
            new UniverseDefinition(dto.AssetClasses, dto.Markets, dto.Currencies.Select(x => new Currency(x))),
            dto.InputDefinitions, dto.SignalRules, dto.EvaluationPlan, dto.SuccessCriteria,
            dto.InvalidationCriteria, evidence.Select(ResearchReportId.Parse).ToArray(),
            UtcUnixMilliseconds.FromProvider(row.CreatedAt),
            row.FrozenAt is null ? null : UtcUnixMilliseconds.FromProvider(row.FrozenAt.Value)));
    }
    internal static async Task<Hypothesis?> LoadHypothesisAsync(TradingDbContext db, string id, CancellationToken token)
    {
        var root = await db.Hypotheses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token).ConfigureAwait(false); if (root is null) return null;
        var versions = await db.HypothesisVersions.AsNoTracking().Where(x => x.HypothesisId == id).OrderBy(x => x.VersionNumber).ToArrayAsync(token).ConfigureAwait(false);
        var states = new List<HypothesisVersionState>();
        foreach (var row in versions)
        {
            var evidence = await db.HypothesisEvidenceReports.AsNoTracking().Where(x => x.HypothesisVersionId == row.Id).OrderBy(x => x.ResearchReportId).Select(x => x.ResearchReportId).ToArrayAsync(token).ConfigureAwait(false);
            var dto = CanonicalJsonSerializer.Deserialize<HypothesisDto>(Schema, row.SpecificationJson);
            states.Add(new(HypothesisVersionId.Parse(row.Id), row.VersionNumber, dto.Claim, new UniverseDefinition(dto.AssetClasses, dto.Markets, dto.Currencies.Select(x => new Currency(x))), dto.InputDefinitions, dto.SignalRules, dto.EvaluationPlan, dto.SuccessCriteria, dto.InvalidationCriteria, evidence.Select(ResearchReportId.Parse).ToArray(), UtcUnixMilliseconds.FromProvider(row.CreatedAt), row.FrozenAt is null ? null : UtcUnixMilliseconds.FromProvider(row.FrozenAt.Value)));
        }
        return Hypothesis.Rehydrate(new(HypothesisId.Parse(root.Id), root.Name, CanonicalEnumeration.Parse<HypothesisStatus>(root.Status), root.CurrentVersionId is null ? null : HypothesisVersionId.Parse(root.CurrentVersionId), UtcUnixMilliseconds.FromProvider(root.CreatedAt), root.Version, states));
    }
    internal static TradeProposalEntity ToEntity(TradeProposal value, string key) => new() { Id = value.Id.ToString(), TradingBotId = value.TradingBotId.ToString(), BotRunId = value.BotRunId.ToString(), PortfolioId = value.PortfolioId.ToString(), PortfolioSnapshotId = value.PortfolioSnapshotId.ToString(), ConfigurationVersionId = value.ConfigurationVersionId.ToString(), InstrumentId = value.InstrumentId.ToString(), ProposalType = CanonicalEnumeration.Format(value.ProposalType), RequestedActionJson = Action(value), Rationale = value.Rationale, HypothesisVersionId = value.HypothesisVersionId?.ToString(), Status = Status(value.Status), CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt), ValidUntil = UtcUnixMilliseconds.ToProvider(value.ValidUntil), IdempotencyKey = key.Trim(), Version = 1 };
    internal static string Status(ProposalStatus status) => CanonicalEnumeration.Format(status);
    private static ProposalStatus Status(string status) => CanonicalEnumeration.Parse<ProposalStatus>(status);
    private static string Action(TradeProposal value) => value.RequestedAction switch
    {
        DirectTradeAction x => CanonicalJsonSerializer.Serialize(Schema, new ActionDto("DirectTrade", x.Side.ToString(), x.Quantity.Amount, x.Quantity.Unit, x.OrderType, x.LimitPrice?.Amount, x.LimitPrice?.Currency.Code, x.TimeInForce, null, value.ContentVersion.Version, value.ContentVersion.ContentHash)),
        TargetAllocationAction x => CanonicalJsonSerializer.Serialize(Schema, new ActionDto("TargetAllocation", null, null, null, null, null, null, null, x.TargetPercentage.Value, value.ContentVersion.Version, value.ContentVersion.ContentHash)),
        _ => throw new InvalidOperationException("Unsupported proposal action.")
    };
    internal static async Task<TradeProposal?> LoadProposalAsync(TradingDbContext db, string id, CancellationToken token)
    {
        var row = await db.TradeProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token).ConfigureAwait(false); if (row is null) return null;
        var action = CanonicalJsonSerializer.Deserialize<ActionDto>(Schema, row.RequestedActionJson);
        RequestedAction requested = action.Kind == "DirectTrade" ? new DirectTradeAction(CanonicalEnumeration.Parse<TradeSide>(action.Side!), new Quantity(action.Quantity!.Value, action.QuantityUnit!), action.OrderType!, action.LimitPrice is null ? null : new Price(action.LimitPrice.Value, new Currency(action.LimitCurrency!)), action.TimeInForce!) : new TargetAllocationAction(new Percentage(action.TargetPercentage!.Value));
        var reportRows = await db.TradeProposalEvidenceReports.AsNoTracking().Where(x => x.TradeProposalId == id).Join(db.ResearchReports.AsNoTracking(), x => x.ResearchReportId, x => x.Id, (_, report) => report).OrderBy(x => x.ReportSeriesId).ThenBy(x => x.VersionNumber).ThenBy(x => x.Id).ToArrayAsync(token).ConfigureAwait(false);
        HypothesisEvidenceReference? hypothesis = null;
        if (row.HypothesisVersionId is not null) { var h = await db.HypothesisVersions.AsNoTracking().SingleAsync(x => x.Id == row.HypothesisVersionId, token).ConfigureAwait(false); hypothesis = new(HypothesisVersionId.Parse(h.Id), h.ContentHash); }
        var evaluations = await db.GuardrailEvaluations.AsNoTracking().Where(x => x.TradeProposalId == id).OrderBy(x => x.EvaluationSequence).ToArrayAsync(token).ConfigureAwait(false);
        var approvals = await db.ProposalApprovals.AsNoTracking().Where(x => x.TradeProposalId == id).OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArrayAsync(token).ConfigureAwait(false);
        var evaluationStates = evaluations.Select(ToState).ToArray();
        return TradeProposal.Rehydrate(new(TradeProposalId.Parse(row.Id), TradingBotId.Parse(row.TradingBotId), BotRunId.Parse(row.BotRunId), PortfolioId.Parse(row.PortfolioId), TradingBotConfigurationVersionId.Parse(row.ConfigurationVersionId), PortfolioDecisionSnapshotId.Parse(row.PortfolioSnapshotId), InstrumentId.Parse(row.InstrumentId), requested, row.Rationale, new(action.ContentVersion, action.ContentHash), hypothesis, reportRows.Select(x => new ReportEvidenceReference(ResearchReportId.Parse(x.Id), x.ReportSeriesId, x.VersionNumber, x.ContentHash)).ToArray(), Status(row.Status), UtcUnixMilliseconds.FromProvider(row.CreatedAt), UtcUnixMilliseconds.FromProvider(row.ValidUntil), row.Version, evaluationStates, approvals.Select(x => ToState(x, action, evaluationStates)).ToArray()));
    }
    internal static GuardrailEvaluationEntity ToEntity(string proposalId, GuardrailEvaluation value) => new() { Id = value.Id.ToString(), TradeProposalId = proposalId, EvaluationSequence = value.Sequence, EvaluationStage = value.EvaluationStage, PolicyVersion = value.PolicyVersion, Outcome = CanonicalEnumeration.Format(value.Outcome), StateSnapshotId = value.StateSnapshotId.ToString(), RuleResultsJson = EvaluationJson(value), ContentHash = value.ContentHash ?? CanonicalJsonSerializer.Sha256(EvaluationJson(value)), EvaluatedAt = UtcUnixMilliseconds.ToProvider(value.EvaluatedAt) };
    private static string EvaluationJson(GuardrailEvaluation value) => CanonicalJsonSerializer.Serialize(Schema, new EvaluationDto(value.RuleResults.Select(x => new RuleDto(x.Rule, x.Outcome.ToString(), x.Reason, x.PolicyLevel?.ToString(), x.PolicyVersion, x.ObservedValue, x.ThresholdValue, x.ReasonCode)).ToArray(), value.PolicyReference?.Level.ToString(), value.PolicyReference?.PolicyId, value.FreshState?.ObservedAt, value.FreshState?.ContentHash, value.EvaluatedPolicies.Select(x => new PolicyDto(x.Level.ToString(), x.PolicyId, x.Version)).ToArray(), value.ProposalContentVersion?.Version, value.ProposalContentVersion?.ContentHash, value.ConfigurationVersionId?.ToString(), value.DiagnosticCode));
    private static GuardrailEvaluationState ToState(GuardrailEvaluationEntity row) { var dto = CanonicalJsonSerializer.Deserialize<EvaluationDto>(Schema, row.RuleResultsJson); var policy = dto.PolicyLevel is null ? null : new GuardrailPolicyReference(CanonicalEnumeration.Parse<GuardrailPolicyLevel>(dto.PolicyLevel), dto.PolicyId!, row.PolicyVersion); var fresh = dto.ObservedAt is null ? null : new FreshStateReference(PortfolioDecisionSnapshotId.Parse(row.StateSnapshotId), dto.ObservedAt.Value, dto.StateHash!); var policies = dto.Policies.Select(x => new GuardrailPolicyReference(CanonicalEnumeration.Parse<GuardrailPolicyLevel>(x.Level), x.PolicyId, x.Version)).ToArray(); return new(GuardrailEvaluationId.Parse(row.Id), row.EvaluationSequence, row.EvaluationStage, row.PolicyVersion, CanonicalEnumeration.Parse<GuardrailOutcome>(row.Outcome), dto.Rules.Select(x => x.PolicyLevel is null ? new GuardrailRuleResult(x.Rule, CanonicalEnumeration.Parse<GuardrailOutcome>(x.Outcome), x.Reason) : new GuardrailRuleResult(x.Rule, CanonicalEnumeration.Parse<GuardrailOutcome>(x.Outcome), x.Reason, CanonicalEnumeration.Parse<GuardrailPolicyLevel>(x.PolicyLevel), x.PolicyVersion!, x.ObservedValue!, x.ThresholdValue!, x.ReasonCode!)).ToArray(), UtcUnixMilliseconds.FromProvider(row.EvaluatedAt), PortfolioDecisionSnapshotId.Parse(row.StateSnapshotId), policy, fresh, policies, dto.ProposalVersion is null ? null : new ProposalContentVersion(dto.ProposalVersion.Value, dto.ProposalHash!), dto.ConfigurationVersionId is null ? null : TradingBotConfigurationVersionId.Parse(dto.ConfigurationVersionId), row.ContentHash, dto.DiagnosticCode); }
    internal static ProposalApprovalEntity ToEntity(string proposalId, ProposalApproval value) => new() { Id = value.Id.ToString(), TradeProposalId = proposalId, Decision = CanonicalEnumeration.Format(value.Decision), ActorType = CanonicalEnumeration.Format(value.ActorType), ActorId = value.ActorId, Reason = value.Reason, DecidedAt = UtcUnixMilliseconds.ToProvider(value.DecidedAt), ProposalVersion = value.ProposalVersion, StateSnapshotId = value.StateSnapshotId.ToString() };
    private static ProposalApprovalState ToState(ProposalApprovalEntity row, ActionDto action,
        IReadOnlyList<GuardrailEvaluationState> evaluations) => new(ProposalApprovalId.Parse(row.Id),
            CanonicalEnumeration.Parse<ApprovalDecision>(row.Decision),
            CanonicalEnumeration.Parse<ApprovalActorType>(row.ActorType), row.ActorId, row.Reason,
            UtcUnixMilliseconds.FromProvider(row.DecidedAt), row.ProposalVersion,
            PortfolioDecisionSnapshotId.Parse(row.StateSnapshotId), new(action.ContentVersion, action.ContentHash),
            evaluations.LastOrDefault(x => x.StateSnapshotId.ToString() == row.StateSnapshotId)?.FreshState);
    internal static CapitalReservationEntity ToEntity(CapitalReservation value) { var row = new CapitalReservationEntity { Id = value.Id.ToString(), Version = 1 }; Copy(value, row); return row; }
    internal static void Copy(CapitalReservation value, CapitalReservationEntity row) { row.PortfolioId = value.PortfolioId.ToString(); row.TradeProposalId = value.TradeProposalId.ToString(); row.OrderId = value.OrderId?.ToString(); row.Amount = CanonicalDecimal.Format(value.Amount.Amount); row.Currency = value.Currency.Code; row.Status = CanonicalEnumeration.Format(value.Status); row.CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt); row.ExpiresAt = UtcUnixMilliseconds.ToProvider(value.ExpiresAt); row.ConsumedAt = value.ConsumedAt is null ? null : UtcUnixMilliseconds.ToProvider(value.ConsumedAt.Value); row.ReleasedAt = value.ReleasedAt is null ? null : UtcUnixMilliseconds.ToProvider(value.ReleasedAt.Value); }
    internal static CapitalReservation? ToDomain(CapitalReservationEntity? row) => row is null ? null : CapitalReservation.Rehydrate(new(CapitalReservationId.Parse(row.Id), PortfolioId.Parse(row.PortfolioId), TradeProposalId.Parse(row.TradeProposalId), row.OrderId is null ? null : OrderId.Parse(row.OrderId), new Money(CanonicalDecimal.Parse(row.Amount), new Currency(row.Currency)), CanonicalEnumeration.Parse<CapitalReservationStatus>(row.Status), UtcUnixMilliseconds.FromProvider(row.CreatedAt), UtcUnixMilliseconds.FromProvider(row.ExpiresAt), row.ConsumedAt is null ? null : UtcUnixMilliseconds.FromProvider(row.ConsumedAt.Value), row.ReleasedAt is null ? null : UtcUnixMilliseconds.FromProvider(row.ReleasedAt.Value), row.Version));
    private sealed record HypothesisDto(string Claim, string[] AssetClasses, string[] Markets, string[] Currencies, string InputDefinitions, string SignalRules, string EvaluationPlan, string SuccessCriteria, string InvalidationCriteria);
    private sealed record ActionDto(string Kind, string? Side, decimal? Quantity, string? QuantityUnit, string? OrderType, decimal? LimitPrice, string? LimitCurrency, string? TimeInForce, decimal? TargetPercentage, int ContentVersion, string ContentHash);
    private sealed record RuleDto(string Rule, string Outcome, string Reason, string? PolicyLevel, string? PolicyVersion, string? ObservedValue, string? ThresholdValue, string? ReasonCode);
    private sealed record PolicyDto(string Level, string PolicyId, string Version);
    private sealed record EvaluationDto(RuleDto[] Rules, string? PolicyLevel, string? PolicyId, DateTimeOffset? ObservedAt, string? StateHash, PolicyDto[] Policies, int? ProposalVersion, string? ProposalHash, string? ConfigurationVersionId, string? DiagnosticCode);
}

using Microsoft.EntityFrameworkCore;

namespace Trading.Data;

public sealed class TradingDbContext(DbContextOptions<TradingDbContext> options) : DbContext(options)
{
    internal DbSet<BrokerConnectionEntity> BrokerConnections => Set<BrokerConnectionEntity>();
    internal DbSet<BrokerAccountEntity> BrokerAccounts => Set<BrokerAccountEntity>();
    internal DbSet<InstrumentEntity> Instruments => Set<InstrumentEntity>();
    internal DbSet<InstrumentBrokerMappingEntity> InstrumentBrokerMappings => Set<InstrumentBrokerMappingEntity>();
    internal DbSet<TradingBotEntity> TradingBots => Set<TradingBotEntity>();
    internal DbSet<TradingBotConfigurationVersionEntity> TradingBotConfigurationVersions => Set<TradingBotConfigurationVersionEntity>();
    internal DbSet<BotRunTriggerEntity> BotRunTriggers => Set<BotRunTriggerEntity>();
    internal DbSet<BotRunEntity> BotRuns => Set<BotRunEntity>();
    internal DbSet<BotToolInvocationEntity> BotToolInvocations => Set<BotToolInvocationEntity>();
    internal DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();
    internal DbSet<PositionEntity> Positions => Set<PositionEntity>();
    internal DbSet<PositionAppliedFillEntity> PositionAppliedFills => Set<PositionAppliedFillEntity>();
    internal DbSet<PortfolioLedgerEntryEntity> PortfolioLedgerEntries => Set<PortfolioLedgerEntryEntity>();
    internal DbSet<PortfolioDecisionSnapshotEntity> PortfolioDecisionSnapshots => Set<PortfolioDecisionSnapshotEntity>();
    internal DbSet<ResearchRequestEntity> ResearchRequests => Set<ResearchRequestEntity>();
    internal DbSet<ResearchSubscriptionEntity> ResearchSubscriptions => Set<ResearchSubscriptionEntity>();
    internal DbSet<ResearchRunEntity> ResearchRuns => Set<ResearchRunEntity>();
    internal DbSet<ResearchToolInvocationEntity> ResearchToolInvocations => Set<ResearchToolInvocationEntity>();
    internal DbSet<ResearchReportEntity> ResearchReports => Set<ResearchReportEntity>();
    internal DbSet<ResearchReportSourceEntity> ResearchReportSources => Set<ResearchReportSourceEntity>();
    internal DbSet<HypothesisEntity> Hypotheses => Set<HypothesisEntity>();
    internal DbSet<HypothesisVersionEntity> HypothesisVersions => Set<HypothesisVersionEntity>();
    internal DbSet<HypothesisEvidenceReportEntity> HypothesisEvidenceReports => Set<HypothesisEvidenceReportEntity>();
    internal DbSet<HypothesisTestResultEntity> HypothesisTestResults => Set<HypothesisTestResultEntity>();
    internal DbSet<TradeProposalEntity> TradeProposals => Set<TradeProposalEntity>();
    internal DbSet<TradeProposalEvidenceReportEntity> TradeProposalEvidenceReports => Set<TradeProposalEvidenceReportEntity>();
    internal DbSet<GuardrailEvaluationEntity> GuardrailEvaluations => Set<GuardrailEvaluationEntity>();
    internal DbSet<ProposalApprovalEntity> ProposalApprovals => Set<ProposalApprovalEntity>();
    internal DbSet<CapitalReservationEntity> CapitalReservations => Set<CapitalReservationEntity>();
    internal DbSet<OrderEntity> Orders => Set<OrderEntity>();
    internal DbSet<OrderTransitionEntity> OrderTransitions => Set<OrderTransitionEntity>();
    internal DbSet<FillEntity> Fills => Set<FillEntity>();
    internal DbSet<BrokerReconciliationEntity> BrokerReconciliations => Set<BrokerReconciliationEntity>();
    internal DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    internal DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();
    internal DbSet<SchemaMetadataEntity> SchemaMetadata => Set<SchemaMetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradingDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) { GuardPublishedFacts(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { GuardPublishedFacts(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    private void GuardPublishedFacts()
    {
        foreach (var entry in ChangeTracker.Entries<ResearchReportEntity>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Published reports are immutable.");
            var changed = entry.Properties.Where(x => x.IsModified).Select(x => x.Metadata.Name).ToArray();
            if (changed.Length != 1 || changed[0] != nameof(ResearchReportEntity.Status) || entry.OriginalValues.GetValue<string>(nameof(ResearchReportEntity.Status)) != "Published" || entry.CurrentValues.GetValue<string>(nameof(ResearchReportEntity.Status)) != "Superseded")
                throw new InvalidOperationException("Published report facts are immutable; only supersession is permitted.");
        }
        if (ChangeTracker.Entries<ResearchReportSourceEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Published report provenance is immutable.");
        if (ChangeTracker.Entries<HypothesisVersionEntity>().Any(x => x.State == EntityState.Deleted || (x.State == EntityState.Modified && x.OriginalValues.GetValue<long?>(nameof(HypothesisVersionEntity.FrozenAt)) is not null)))
            throw new InvalidOperationException("Frozen hypothesis versions are immutable.");
        if (ChangeTracker.Entries<HypothesisEvidenceReportEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<HypothesisTestResultEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<TradeProposalEvidenceReportEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<GuardrailEvaluationEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<ProposalApprovalEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Proposal governance audit facts are immutable.");
        if (ChangeTracker.Entries<OrderTransitionEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<FillEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<BrokerReconciliationEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Order execution audit facts are immutable.");
    }
}

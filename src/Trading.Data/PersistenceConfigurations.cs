using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trading.Core.Operations;
using Trading.Core.Orders;

namespace Trading.Data;

internal sealed class KillSwitchConfiguration : IEntityTypeConfiguration<KillSwitchEntity>
{
    public void Configure(EntityTypeBuilder<KillSwitchEntity> b)
    {
        b.ToTable("kill_switches", t =>
        {
            t.HasCheckConstraint("ck_kill_switch_scope", "scope_kind IN ('Platform','BrokerAccount','Portfolio','TradingBot')");
            t.HasCheckConstraint("ck_kill_switch_state", "state IN ('Clear','Active')");
            t.HasCheckConstraint("ck_kill_switch_version", "version > 0");
        });
        b.HasKey(x => new { x.ScopeKind, x.ScopeId });
        b.Property(x => x.ScopeKind).HasColumnName("scope_kind").HasMaxLength(32);
        b.Property(x => x.ScopeId).HasColumnName("scope_id").HasMaxLength(200);
        b.Property(x => x.State).HasColumnName("state").HasMaxLength(16);
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        b.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(200);
        b.Property(x => x.Confirmation).HasColumnName("confirmation").HasMaxLength(500);
        b.Property(x => x.ChangedAt).HasColumnName("changed_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}

internal sealed class KillSwitchHistoryConfiguration : EntityConfiguration<KillSwitchHistoryEntity>
{
    public KillSwitchHistoryConfiguration() : base("kill_switch_history") { }
    protected override void ConfigureEntity(EntityTypeBuilder<KillSwitchHistoryEntity> b)
    {
        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.ScopeKind).HasMaxLength(32).IsRequired();
        b.Property(x => x.ScopeId).HasMaxLength(200).IsRequired();
        b.Property(x => x.PriorState).HasMaxLength(16).IsRequired();
        b.Property(x => x.ResultingState).HasMaxLength(16).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Confirmation).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.ScopeKind, x.ScopeId, x.Version }).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_kill_switch_history_scope", "scope_kind IN ('Platform','BrokerAccount','Portfolio','TradingBot')");
            t.HasCheckConstraint("ck_kill_switch_history_state", "prior_state IN ('Clear','Active') AND resulting_state IN ('Clear','Active')");
            t.HasCheckConstraint("ck_kill_switch_history_version", "version > 0");
        });
    }
}

internal abstract class EntityConfiguration<TEntity>(string tableName) : IEntityTypeConfiguration<TEntity>
    where TEntity : PersistenceEntity
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(tableName);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("TEXT");
        ConfigureEntity(builder);
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetColumnName(ToSnakeCase(property.Name));
        }
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);

    private static string ToSnakeCase(string value) => string.Concat(value.Select((character, index) =>
        index > 0 && char.IsUpper(character) ? $"_{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));
}

internal sealed class BrokerConnectionConfiguration : EntityConfiguration<BrokerConnectionEntity>
{
    public BrokerConnectionConfiguration() : base("broker_connections") { }
    protected override void ConfigureEntity(EntityTypeBuilder<BrokerConnectionEntity> builder)
    {
        builder.Property(x => x.BrokerType).IsRequired(); builder.Property(x => x.DisplayName).IsRequired();
        builder.Property(x => x.Environment).IsRequired(); builder.Property(x => x.CredentialReference).IsRequired();
        builder.Property(x => x.Status).IsRequired(); builder.Property(x => x.CapabilitiesJson).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.ToTable(t => { t.HasCheckConstraint("ck_broker_connections_environment", "environment IN ('Paper', 'Live')"); t.HasCheckConstraint("ck_broker_connections_status", "status IN ('Enabled', 'Disabled', 'Disconnected')"); });
    }
}

internal sealed class BrokerAccountConfiguration : EntityConfiguration<BrokerAccountEntity>
{
    public BrokerAccountConfiguration() : base("broker_accounts") { }
    protected override void ConfigureEntity(EntityTypeBuilder<BrokerAccountEntity> builder)
    {
        builder.Property(x => x.BrokerConnectionId).IsRequired(); builder.Property(x => x.ExternalAccountId).IsRequired();
        builder.Property(x => x.DisplayName).IsRequired(); builder.Property(x => x.AccountType).IsRequired();
        builder.Property(x => x.BaseCurrency).IsRequired(); builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CapabilitiesJson).IsRequired(); builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.BrokerConnectionId, x.ExternalAccountId }).IsUnique();
        builder.HasOne<BrokerConnectionEntity>().WithMany().HasForeignKey(x => x.BrokerConnectionId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_broker_accounts_status", "status IN ('Active', 'Restricted', 'Disabled')"));
    }
}

internal sealed class InstrumentConfiguration : EntityConfiguration<InstrumentEntity>
{
    public InstrumentConfiguration() : base("instruments") { }
    protected override void ConfigureEntity(EntityTypeBuilder<InstrumentEntity> builder)
    {
        builder.Property(x => x.InstrumentType).IsRequired(); builder.Property(x => x.PrimarySymbol).IsRequired();
        builder.Property(x => x.DisplayName).IsRequired(); builder.Property(x => x.Currency).IsRequired();
        builder.Property(x => x.Exchange).IsRequired(); builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.InstrumentType, x.PrimarySymbol, x.Exchange }).IsUnique();
        builder.ToTable(t => { t.HasCheckConstraint("ck_instruments_type", "instrument_type IN ('Equity', 'Option', 'Fund', 'Bond', 'Cash', 'Crypto')"); t.HasCheckConstraint("ck_instruments_status", "status IN ('Active', 'Inactive')"); t.HasCheckConstraint("ck_instruments_price_precision", "price_precision BETWEEN 0 AND 8"); t.HasCheckConstraint("ck_instruments_quantity_precision", "quantity_precision BETWEEN 0 AND 8"); });
    }
}

internal sealed class InstrumentBrokerMappingConfiguration : EntityConfiguration<InstrumentBrokerMappingEntity>
{
    public InstrumentBrokerMappingConfiguration() : base("instrument_broker_mappings") { }
    protected override void ConfigureEntity(EntityTypeBuilder<InstrumentBrokerMappingEntity> builder)
    {
        builder.Property(x => x.InstrumentId).IsRequired(); builder.Property(x => x.BrokerConnectionId).IsRequired();
        builder.Property(x => x.ExternalInstrumentId).IsRequired(); builder.Property(x => x.Symbol).IsRequired();
        builder.Property(x => x.Exchange).IsRequired(); builder.Property(x => x.MetadataJson).IsRequired();
        builder.HasIndex(x => new { x.BrokerConnectionId, x.ExternalInstrumentId, x.EffectiveFrom }).IsUnique();
        builder.HasIndex(x => new { x.BrokerConnectionId, x.ExternalInstrumentId });
        builder.HasOne<InstrumentEntity>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BrokerConnectionEntity>().WithMany().HasForeignKey(x => x.BrokerConnectionId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_instrument_broker_mappings_interval", "effective_to IS NULL OR effective_to > effective_from"));
    }
}

internal sealed class TradingBotConfiguration : EntityConfiguration<TradingBotEntity>
{
    public TradingBotConfiguration() : base("trading_bots") { }
    protected override void ConfigureEntity(EntityTypeBuilder<TradingBotEntity> builder)
    {
        builder.Property(x => x.Name).IsRequired(); builder.Property(x => x.Status).IsRequired(); builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.Name).IsUnique(); builder.HasIndex(x => new { x.Status, x.AcceptedNextRunAt });
        builder.HasOne<TradingBotConfigurationVersionEntity>().WithMany().HasForeignKey(x => x.ActiveConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BotRunEntity>().WithMany().HasForeignKey(x => x.LastCompletedRunId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_trading_bots_status", "status IN ('Enabled', 'Paused', 'Retired')"));
    }
}

internal sealed class TradingBotConfigurationVersionConfiguration : EntityConfiguration<TradingBotConfigurationVersionEntity>
{
    public TradingBotConfigurationVersionConfiguration() : base("trading_bot_configuration_versions") { }
    protected override void ConfigureEntity(EntityTypeBuilder<TradingBotConfigurationVersionEntity> builder)
    {
        builder.Property(x => x.TradingBotId).IsRequired(); builder.Property(x => x.InvestmentMandateJson).IsRequired();
        builder.Property(x => x.RiskPolicyJson).IsRequired(); builder.Property(x => x.ToolPolicyJson).IsRequired();
        builder.Property(x => x.RunBudgetJson).IsRequired(); builder.Property(x => x.SchedulingPolicyJson).IsRequired();
        builder.Property(x => x.ExecutionMode).IsRequired(); builder.Property(x => x.ModelConfigurationJson).IsRequired();
        builder.Property(x => x.PromptVersion).IsRequired(); builder.Property(x => x.ContentHash).IsRequired();
        builder.HasIndex(x => new { x.TradingBotId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.TradingBotId, x.ContentHash }).IsUnique();
        builder.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.TradingBotId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_trading_bot_configuration_versions_number", "version_number > 0"); t.HasCheckConstraint("ck_trading_bot_configuration_versions_execution_mode", "execution_mode IN ('ResearchOnly', 'HumanApproval', 'PaperTrading', 'LiveTrading')"); t.HasCheckConstraint("ck_trading_bot_configuration_versions_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)"); });
    }
}

internal sealed class BotRunTriggerConfiguration : EntityConfiguration<BotRunTriggerEntity>
{
    public BotRunTriggerConfiguration() : base("bot_run_triggers") { }
    protected override void ConfigureEntity(EntityTypeBuilder<BotRunTriggerEntity> builder)
    {
        builder.Property(x => x.TradingBotId).IsRequired();
        builder.Property(x => x.TriggerType).IsRequired();
        builder.Property(x => x.Reason).IsRequired();
        builder.HasIndex(x => new { x.TradingBotId, x.SourceType, x.SourceId }).IsUnique()
            .HasFilter("source_id IS NOT NULL");
        builder.HasIndex(x => new { x.TradingBotId, x.ConsumedByRunId, x.OccurredAt });
        builder.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.TradingBotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BotRunEntity>().WithMany().HasForeignKey(x => x.ConsumedByRunId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_bot_run_triggers_type", "trigger_type IN ('Manual', 'BaselineSchedule', 'AcceptedNextRun', 'ResearchCompleted', 'ResearchFailed', 'PortfolioEvent', 'RiskOrReconciliation')");
            t.HasCheckConstraint("ck_bot_run_triggers_source", "(source_type IS NULL AND source_id IS NULL) OR (source_type IS NOT NULL AND source_id IS NOT NULL)");
            t.HasCheckConstraint("ck_bot_run_triggers_reason", "length(reason) > 0");
        });
    }
}

internal sealed class BotRunConfiguration : EntityConfiguration<BotRunEntity>
{
    public BotRunConfiguration() : base("bot_runs") { }
    protected override void ConfigureEntity(EntityTypeBuilder<BotRunEntity> builder)
    {
        builder.Property(x => x.TradingBotId).IsRequired();
        builder.Property(x => x.ConfigurationVersionId).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.UsageJson).IsRequired();
        builder.Property(x => x.ModelTranscriptJson).IsRequired();
        builder.Property(x => x.InputRenderingVersion).IsRequired(); builder.Property(x => x.InputRenderingHash);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.TradingBotId).IsUnique()
            .HasFilter("status IN ('Pending', 'AcquiringLease', 'PreparingSnapshot', 'Reasoning', 'WaitingForTool')");
        builder.HasIndex(x => new { x.TradingBotId, x.StartedAt }).IsDescending(false, true);
        builder.HasIndex(x => new { x.Status, x.LeaseExpiresAt });
        builder.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.TradingBotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradingBotConfigurationVersionEntity>().WithMany().HasForeignKey(x => x.ConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PortfolioDecisionSnapshotEntity>().WithMany().HasForeignKey(x => x.PortfolioSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_bot_runs_status", "status IN ('Pending', 'AcquiringLease', 'PreparingSnapshot', 'Reasoning', 'WaitingForTool', 'Completed', 'TimedOut', 'BudgetExceeded', 'Cancelled', 'Faulted')");
            t.HasCheckConstraint("ck_bot_runs_lease", "(lease_owner IS NULL AND lease_expires_at IS NULL) OR (lease_owner IS NOT NULL AND lease_expires_at IS NOT NULL)");
            t.HasCheckConstraint("ck_bot_runs_completion", "(status IN ('Pending', 'AcquiringLease', 'PreparingSnapshot', 'Reasoning', 'WaitingForTool') AND completed_at IS NULL) OR (status IN ('Completed', 'TimedOut', 'BudgetExceeded', 'Cancelled', 'Faulted') AND completed_at IS NOT NULL)");
            t.HasCheckConstraint("ck_bot_runs_transcript_schema", "model_transcript_schema_version > 0");
            t.HasCheckConstraint("ck_bot_runs_rendering_version", "length(input_rendering_version) > 0");
            t.HasCheckConstraint("ck_bot_runs_version", "version > 0");
        });
    }
}

internal sealed class BotToolInvocationConfiguration : EntityConfiguration<BotToolInvocationEntity>
{
    public BotToolInvocationConfiguration() : base("bot_tool_invocations") { }
    protected override void ConfigureEntity(EntityTypeBuilder<BotToolInvocationEntity> builder)
    {
        builder.Property(x => x.BotRunId).IsRequired();
        builder.Property(x => x.ToolName).IsRequired();
        builder.Property(x => x.ArgumentsJson).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.HasIndex(x => new { x.BotRunId, x.SequenceNumber }).IsUnique();
        builder.HasOne<BotRunEntity>().WithMany().HasForeignKey(x => x.BotRunId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_bot_tool_invocations_sequence", "sequence_number > 0");
            t.HasCheckConstraint("ck_bot_tool_invocations_schema", "tool_schema_version > 0");
            t.HasCheckConstraint("ck_bot_tool_invocations_status", "status IN ('Started', 'Completed', 'Failed', 'Cancelled')");
            t.HasCheckConstraint("ck_bot_tool_invocations_completion", "(status = 'Started' AND completed_at IS NULL) OR (status IN ('Completed', 'Failed', 'Cancelled') AND completed_at IS NOT NULL)");
        });
    }
}

internal sealed class PortfolioConfiguration : EntityConfiguration<PortfolioEntity>
{
    public PortfolioConfiguration() : base("portfolios") { }
    protected override void ConfigureEntity(EntityTypeBuilder<PortfolioEntity> builder)
    {
        builder.Property(x => x.Name).IsRequired(); builder.Property(x => x.BaseCurrency).IsRequired();
        builder.Property(x => x.Status).IsRequired(); builder.Property(x => x.CapitalAllocationAmount).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.CashReservePolicyJson).IsRequired(); builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.BrokerAccountId).IsUnique().HasFilter("broker_account_id IS NOT NULL AND status = 'Active'");
        builder.HasIndex(x => x.AssignedTradingBotId).IsUnique().HasFilter("assigned_trading_bot_id IS NOT NULL AND status = 'Active'");
        builder.HasOne<BrokerAccountEntity>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.AssignedTradingBotId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_portfolios_status", "status IN ('Active', 'Paused', 'Closed')"));
    }
}

internal sealed class PositionConfiguration : EntityConfiguration<PositionEntity>
{
    public PositionConfiguration() : base("positions") { }
    protected override void ConfigureEntity(EntityTypeBuilder<PositionEntity> builder)
    {
        builder.Property(x => x.PortfolioId).IsRequired(); builder.Property(x => x.InstrumentId).IsRequired();
        builder.Property(x => x.QuantityUnit).IsRequired();
        builder.Property(x => x.Quantity).IsRequired().HasColumnType("TEXT"); builder.Property(x => x.AverageCostAmount).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.AverageCostCurrency).IsRequired(); builder.Property(x => x.RealizedPnlAmount).IsRequired().HasColumnType("TEXT");
        builder.Property(x => x.RealizedPnlCurrency).IsRequired(); builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.PortfolioId, x.InstrumentId }).IsUnique();
        builder.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstrumentEntity>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PositionAppliedFillConfiguration : IEntityTypeConfiguration<PositionAppliedFillEntity>
{
    public void Configure(EntityTypeBuilder<PositionAppliedFillEntity> builder)
    {
        builder.ToTable("position_applied_fills"); builder.HasKey(x => new { x.PositionId, x.FillId });
        builder.Property(x => x.PositionId).HasColumnName("position_id"); builder.Property(x => x.FillId).HasColumnName("fill_id");
        builder.Property(x => x.AppliedAt).HasColumnName("applied_at");
        builder.HasOne<PositionEntity>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PortfolioLedgerEntryConfiguration : EntityConfiguration<PortfolioLedgerEntryEntity>
{
    public PortfolioLedgerEntryConfiguration() : base("portfolio_ledger_entries") { }
    protected override void ConfigureEntity(EntityTypeBuilder<PortfolioLedgerEntryEntity> builder)
    {
        builder.Property(x => x.PortfolioId).IsRequired(); builder.Property(x => x.EntryType).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("TEXT"); builder.Property(x => x.Quantity).HasColumnType("TEXT");
        builder.Property(x => x.SourceType).IsRequired(); builder.Property(x => x.SourceId).IsRequired();
        builder.HasIndex(x => new { x.PortfolioId, x.SourceType, x.SourceId }).IsUnique();
        builder.HasIndex(x => new { x.PortfolioId, x.EffectiveAt });
        builder.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstrumentEntity>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PortfolioLedgerEntryEntity>().WithMany().HasForeignKey(x => x.ReversesEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_portfolio_ledger_entries_type", "entry_type IN ('Deposit', 'Withdrawal', 'Settlement', 'Fee', 'Dividend', 'Interest', 'Tax', 'CorporateAction', 'ManualCorrection')"); t.HasCheckConstraint("ck_portfolio_ledger_entries_source_type", "source_type IN ('BrokerExecution', 'BrokerEvent', 'AuditedAdjustment')"); });
    }
}

internal sealed class PortfolioDecisionSnapshotConfiguration : EntityConfiguration<PortfolioDecisionSnapshotEntity>
{
    public PortfolioDecisionSnapshotConfiguration() : base("portfolio_decision_snapshots") { }
    protected override void ConfigureEntity(EntityTypeBuilder<PortfolioDecisionSnapshotEntity> builder)
    {
        builder.Property(x => x.PortfolioId).IsRequired(); builder.Property(x => x.TradingBotId).IsRequired();
        builder.Property(x => x.ConfigurationVersionId).IsRequired(); builder.Property(x => x.ReconciliationStatus).IsRequired();
        builder.Property(x => x.DataFreshnessJson).IsRequired(); builder.Property(x => x.SnapshotJson).IsRequired();
        builder.Property(x => x.ContentHash).IsRequired(); builder.HasIndex(x => new { x.PortfolioId, x.AsOf }).IsDescending(false, true);
        builder.HasIndex(x => new { x.TradingBotId, x.AsOf }).IsDescending(false, true);
        builder.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.TradingBotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradingBotConfigurationVersionEntity>().WithMany().HasForeignKey(x => x.ConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_portfolio_decision_snapshots_reconciliation_status", "reconciliation_status IN ('Reconciled', 'Pending', 'Uncertain')"); t.HasCheckConstraint("ck_portfolio_decision_snapshots_schema_version", "snapshot_schema_version > 0"); t.HasCheckConstraint("ck_portfolio_decision_snapshots_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)"); });
    }
}

internal sealed class ResearchRequestConfiguration : EntityConfiguration<ResearchRequestEntity>
{
    public ResearchRequestConfiguration() : base("research_requests") { }
    protected override void ConfigureEntity(EntityTypeBuilder<ResearchRequestEntity> builder)
    {
        builder.Property(x => x.SubjectType).IsRequired(); builder.Property(x => x.Question).IsRequired();
        builder.Property(x => x.NormalizedResearchKey).IsRequired(); builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Visibility).IsRequired(); builder.Property(x => x.FreshnessRequirementJson).IsRequired();
        builder.Property(x => x.RequestJson).IsRequired(); builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.NormalizedResearchKey, x.Status });
        builder.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.RequestingBotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ResearchReportEntity>().WithMany().HasForeignKey(x => x.ResultReportId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_research_requests_status", "status IN ('Requested','Validating','Queued','Running','Completed','Failed','TimedOut','BudgetExceeded','Cancelled')"); t.HasCheckConstraint("ck_research_requests_visibility", "visibility IN ('Shared','BotPrivate','Restricted')"); t.HasCheckConstraint("ck_research_requests_version", "version > 0"); });
    }
}

internal sealed class ResearchSubscriptionConfiguration : EntityConfiguration<ResearchSubscriptionEntity>
{
    public ResearchSubscriptionConfiguration() : base("research_subscriptions") { }
    protected override void ConfigureEntity(EntityTypeBuilder<ResearchSubscriptionEntity> builder)
    {
        builder.Property(x => x.ResearchRequestId).IsRequired(); builder.Property(x => x.TradingBotId).IsRequired();
        builder.Property(x => x.NotificationStatus).IsRequired(); builder.HasIndex(x => new { x.ResearchRequestId, x.TradingBotId }).IsUnique();
        builder.HasOne<ResearchRequestEntity>().WithMany().HasForeignKey(x => x.ResearchRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.TradingBotId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_research_subscriptions_notification", "notification_status IN ('Pending','Delivered','Failed')"));
    }
}

internal sealed class ResearchRunConfiguration : EntityConfiguration<ResearchRunEntity>
{
    public ResearchRunConfiguration() : base("research_runs") { }
    protected override void ConfigureEntity(EntityTypeBuilder<ResearchRunEntity> builder)
    {
        builder.Property(x => x.ResearchRequestId).IsRequired(); builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.ModelConfigurationJson).IsRequired(); builder.Property(x => x.PromptVersion).IsRequired();
        builder.Property(x => x.ToolSetVersion).IsRequired(); builder.Property(x => x.ReportSchemaVersion).IsRequired(); builder.Property(x => x.UsageJson).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken(); builder.HasIndex(x => new { x.ResearchRequestId, x.AttemptNumber }).IsUnique();
        builder.HasOne<ResearchRequestEntity>().WithMany().HasForeignKey(x => x.ResearchRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_research_runs_attempt", "attempt_number > 0"); t.HasCheckConstraint("ck_research_runs_status", "status IN ('Pending','Running','WaitingForTool','Completed','Failed','TimedOut','BudgetExceeded','Cancelled')"); t.HasCheckConstraint("ck_research_runs_version", "version > 0"); });
    }
}

internal sealed class ResearchToolInvocationConfiguration : EntityConfiguration<ResearchToolInvocationEntity>
{
    public ResearchToolInvocationConfiguration() : base("research_tool_invocations") { }
    protected override void ConfigureEntity(EntityTypeBuilder<ResearchToolInvocationEntity> builder)
    {
        builder.Property(x => x.ResearchRunId).IsRequired(); builder.Property(x => x.ToolName).IsRequired();
        builder.Property(x => x.ArgumentsJson).IsRequired(); builder.Property(x => x.Status).IsRequired();
        builder.HasIndex(x => new { x.ResearchRunId, x.SequenceNumber }).IsUnique();
        builder.HasOne<ResearchRunEntity>().WithMany().HasForeignKey(x => x.ResearchRunId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_research_tool_invocations_sequence", "sequence_number > 0"); t.HasCheckConstraint("ck_research_tool_invocations_schema", "tool_schema_version > 0"); t.HasCheckConstraint("ck_research_tool_invocations_status", "status IN ('Started','Succeeded','Failed','Rejected','Cancelled')"); });
    }
}

internal sealed class ResearchReportConfiguration : EntityConfiguration<ResearchReportEntity>
{
    public ResearchReportConfiguration() : base("research_reports") { }
    protected override void ConfigureEntity(EntityTypeBuilder<ResearchReportEntity> builder)
    {
        builder.Property(x => x.ReportSeriesId).IsRequired(); builder.Property(x => x.ResearchRequestId).IsRequired(); builder.Property(x => x.ResearchRunId).IsRequired();
        builder.Property(x => x.SubjectType).IsRequired(); builder.Property(x => x.Question).IsRequired(); builder.Property(x => x.Visibility).IsRequired();
        builder.Property(x => x.Status).IsRequired(); builder.Property(x => x.ReportSchemaVersion).IsRequired(); builder.Property(x => x.ContentJson).IsRequired();
        builder.Property(x => x.ContentHash).IsRequired(); builder.Property(x => x.GeneratorMetadataJson).IsRequired();
        builder.HasIndex(x => new { x.ReportSeriesId, x.VersionNumber }).IsUnique(); builder.HasIndex(x => new { x.ReportSeriesId, x.ContentHash }).IsUnique();
        builder.HasIndex(x => new { x.SubjectId, x.GeneratedAt }).IsDescending(false, true);
        builder.HasOne<ResearchRequestEntity>().WithMany().HasForeignKey(x => x.ResearchRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ResearchRunEntity>().WithMany().HasForeignKey(x => x.ResearchRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ResearchReportEntity>().WithMany().HasForeignKey(x => x.SupersedesReportId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_research_reports_version", "version_number > 0"); t.HasCheckConstraint("ck_research_reports_visibility", "visibility IN ('Shared','BotPrivate','Restricted')"); t.HasCheckConstraint("ck_research_reports_status", "status IN ('Published','Expired','Superseded','Retracted')"); t.HasCheckConstraint("ck_research_reports_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)"); });
    }
}

internal sealed class ResearchReportSourceConfiguration : EntityConfiguration<ResearchReportSourceEntity>
{
    public ResearchReportSourceConfiguration() : base("research_report_sources") { }
    protected override void ConfigureEntity(EntityTypeBuilder<ResearchReportSourceEntity> builder)
    {
        builder.Property(x => x.ResearchReportId).IsRequired(); builder.Property(x => x.SourceType).IsRequired(); builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.ContentHash).IsRequired(); builder.Property(x => x.MetadataJson).IsRequired();
        builder.HasIndex(x => new { x.ResearchReportId, x.SourceSequence }).IsUnique();
        builder.HasOne<ResearchReportEntity>().WithMany().HasForeignKey(x => x.ResearchReportId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_research_report_sources_sequence", "source_sequence > 0"); t.HasCheckConstraint("ck_research_report_sources_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)"); });
    }
}

internal sealed class HypothesisConfiguration : EntityConfiguration<HypothesisEntity>
{
    public HypothesisConfiguration() : base("hypotheses") { }
    protected override void ConfigureEntity(EntityTypeBuilder<HypothesisEntity> b) { b.Property(x => x.Name).IsRequired(); b.Property(x => x.Status).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken(); b.HasOne<HypothesisVersionEntity>().WithMany().HasForeignKey(x => x.CurrentVersionId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_hypotheses_status", "status IN ('Draft','Frozen','Testing','Validated','Rejected','Retired')"); t.HasCheckConstraint("ck_hypotheses_version", "version > 0"); }); }
}
internal sealed class HypothesisVersionConfiguration : EntityConfiguration<HypothesisVersionEntity>
{
    public HypothesisVersionConfiguration() : base("hypothesis_versions") { }
    protected override void ConfigureEntity(EntityTypeBuilder<HypothesisVersionEntity> b) { b.Property(x => x.HypothesisId).IsRequired(); b.Property(x => x.SpecificationJson).IsRequired(); b.Property(x => x.ContentHash).IsRequired(); b.HasIndex(x => new { x.HypothesisId, x.VersionNumber }).IsUnique(); b.HasIndex(x => new { x.HypothesisId, x.ContentHash }).IsUnique(); b.HasOne<HypothesisEntity>().WithMany().HasForeignKey(x => x.HypothesisId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_hypothesis_versions_number", "version_number > 0 AND specification_schema_version > 0"); t.HasCheckConstraint("ck_hypothesis_versions_hash", "length(content_hash)=64 AND content_hash=lower(content_hash)"); }); }
}
internal sealed class HypothesisEvidenceReportConfiguration : IEntityTypeConfiguration<HypothesisEvidenceReportEntity>
{
    public void Configure(EntityTypeBuilder<HypothesisEvidenceReportEntity> b) { b.ToTable("hypothesis_evidence_reports", t => t.HasCheckConstraint("ck_hypothesis_evidence_relationship", "relationship_type IN ('Supporting','Contradictory','Contextual')")); b.HasKey(x => new { x.HypothesisVersionId, x.ResearchReportId }); b.Property(x => x.HypothesisVersionId).HasColumnName("hypothesis_version_id"); b.Property(x => x.ResearchReportId).HasColumnName("research_report_id"); b.Property(x => x.RelationshipType).HasColumnName("relationship_type").IsRequired(); b.HasOne<HypothesisVersionEntity>().WithMany().HasForeignKey(x => x.HypothesisVersionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<ResearchReportEntity>().WithMany().HasForeignKey(x => x.ResearchReportId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class HypothesisTestResultConfiguration : EntityConfiguration<HypothesisTestResultEntity>
{
    public HypothesisTestResultConfiguration() : base("hypothesis_test_results") { }
    protected override void ConfigureEntity(EntityTypeBuilder<HypothesisTestResultEntity> b) { b.Property(x => x.HypothesisVersionId).IsRequired(); b.Property(x => x.DatasetVersion).IsRequired(); b.Property(x => x.CodeVersion).IsRequired(); b.Property(x => x.ParametersHash).IsRequired(); b.Property(x => x.Status).IsRequired(); b.Property(x => x.MetricsJson).IsRequired(); b.Property(x => x.ArtifactsJson).IsRequired(); b.Property(x => x.ResultHash).IsRequired(); b.HasOne<HypothesisVersionEntity>().WithMany().HasForeignKey(x => x.HypothesisVersionId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_hypothesis_test_status", "status IN ('Pending','Running','Completed','Failed','Cancelled')"); t.HasCheckConstraint("ck_hypothesis_test_hashes", "length(parameters_hash)=64 AND parameters_hash=lower(parameters_hash) AND length(result_hash)=64 AND result_hash=lower(result_hash)"); }); }
}
internal sealed class TradeProposalConfiguration : EntityConfiguration<TradeProposalEntity>
{
    public TradeProposalConfiguration() : base("trade_proposals") { }
    protected override void ConfigureEntity(EntityTypeBuilder<TradeProposalEntity> b) { b.Property(x => x.TradingBotId).IsRequired(); b.Property(x => x.BotRunId).IsRequired(); b.Property(x => x.PortfolioId).IsRequired(); b.Property(x => x.PortfolioSnapshotId).IsRequired(); b.Property(x => x.ConfigurationVersionId).IsRequired(); b.Property(x => x.InstrumentId).IsRequired(); b.Property(x => x.ProposalType).IsRequired(); b.Property(x => x.RequestedActionJson).IsRequired(); b.Property(x => x.Rationale).IsRequired(); b.Property(x => x.Status).IsRequired(); b.Property(x => x.IdempotencyKey).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.HasIndex(x => new { x.PortfolioId, x.Status, x.CreatedAt }); b.HasOne<TradingBotEntity>().WithMany().HasForeignKey(x => x.TradingBotId).OnDelete(DeleteBehavior.Restrict); b.HasOne<BotRunEntity>().WithMany().HasForeignKey(x => x.BotRunId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PortfolioDecisionSnapshotEntity>().WithMany().HasForeignKey(x => x.PortfolioSnapshotId).OnDelete(DeleteBehavior.Restrict); b.HasOne<TradingBotConfigurationVersionEntity>().WithMany().HasForeignKey(x => x.ConfigurationVersionId).OnDelete(DeleteBehavior.Restrict); b.HasOne<InstrumentEntity>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict); b.HasOne<HypothesisVersionEntity>().WithMany().HasForeignKey(x => x.HypothesisVersionId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_trade_proposals_type", "proposal_type IN ('DirectTrade','TargetAllocation')"); t.HasCheckConstraint("ck_trade_proposals_status", "status IN ('Recorded','Validating','AwaitingHumanApproval','Approved','Rejected','Expired','Cancelled','ConvertedToOrder')"); t.HasCheckConstraint("ck_trade_proposals_time", "valid_until > created_at"); t.HasCheckConstraint("ck_trade_proposals_version", "version > 0"); }); }
}
internal sealed class TradeProposalEvidenceReportConfiguration : IEntityTypeConfiguration<TradeProposalEvidenceReportEntity>
{
    public void Configure(EntityTypeBuilder<TradeProposalEvidenceReportEntity> b) { b.ToTable("trade_proposal_evidence_reports"); b.HasKey(x => new { x.TradeProposalId, x.ResearchReportId }); b.Property(x => x.TradeProposalId).HasColumnName("trade_proposal_id"); b.Property(x => x.ResearchReportId).HasColumnName("research_report_id"); b.HasOne<TradeProposalEntity>().WithMany().HasForeignKey(x => x.TradeProposalId).OnDelete(DeleteBehavior.Restrict); b.HasOne<ResearchReportEntity>().WithMany().HasForeignKey(x => x.ResearchReportId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class GuardrailEvaluationConfiguration : EntityConfiguration<GuardrailEvaluationEntity>
{
    public GuardrailEvaluationConfiguration() : base("guardrail_evaluations") { }
    protected override void ConfigureEntity(EntityTypeBuilder<GuardrailEvaluationEntity> b) { b.Property(x => x.TradeProposalId).IsRequired(); b.Property(x => x.EvaluationStage).IsRequired(); b.Property(x => x.PolicyVersion).IsRequired(); b.Property(x => x.Outcome).IsRequired(); b.Property(x => x.StateSnapshotId).IsRequired(); b.Property(x => x.RuleResultsJson).IsRequired(); b.Property(x => x.ContentHash).IsRequired().HasMaxLength(64); b.HasIndex(x => new { x.TradeProposalId, x.EvaluationSequence }).IsUnique(); b.HasIndex(x => x.ContentHash).IsUnique(); b.HasOne<TradeProposalEntity>().WithMany().HasForeignKey(x => x.TradeProposalId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PortfolioDecisionSnapshotEntity>().WithMany().HasForeignKey(x => x.StateSnapshotId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_guardrail_evaluation_sequence", "evaluation_sequence > 0"); t.HasCheckConstraint("ck_guardrail_evaluation_stage", "evaluation_stage IN ('Initial','ApprovalRevalidation','ReservationRevalidation','Hierarchical')"); t.HasCheckConstraint("ck_guardrail_evaluation_outcome", "outcome IN ('Passed','Failed')"); t.HasCheckConstraint("ck_guardrail_evaluation_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)"); }); }
}
internal sealed class ProposalApprovalConfiguration : EntityConfiguration<ProposalApprovalEntity>
{
    public ProposalApprovalConfiguration() : base("proposal_approvals") { }
    protected override void ConfigureEntity(EntityTypeBuilder<ProposalApprovalEntity> b) { b.Property(x => x.TradeProposalId).IsRequired(); b.Property(x => x.Decision).IsRequired(); b.Property(x => x.ActorType).IsRequired(); b.Property(x => x.ActorId).IsRequired(); b.Property(x => x.StateSnapshotId).IsRequired(); b.HasOne<TradeProposalEntity>().WithMany().HasForeignKey(x => x.TradeProposalId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PortfolioDecisionSnapshotEntity>().WithMany().HasForeignKey(x => x.StateSnapshotId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_proposal_approvals_decision", "decision IN ('Approved','Rejected')"); t.HasCheckConstraint("ck_proposal_approvals_actor", "actor_type IN ('User','AuthorizedPolicy')"); t.HasCheckConstraint("ck_proposal_approvals_version", "proposal_version > 0"); }); }
}
internal sealed class CapitalReservationConfiguration : EntityConfiguration<CapitalReservationEntity>
{
    public CapitalReservationConfiguration() : base("capital_reservations") { }
    protected override void ConfigureEntity(EntityTypeBuilder<CapitalReservationEntity> b) { b.Property(x => x.PortfolioId).IsRequired(); b.Property(x => x.TradeProposalId).IsRequired(); b.Property(x => x.Amount).IsRequired().HasColumnType("TEXT"); b.Property(x => x.Currency).IsRequired(); b.Property(x => x.Status).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => x.TradeProposalId).IsUnique().HasFilter("status = 'Active'"); b.HasIndex(x => new { x.PortfolioId, x.Status, x.ExpiresAt }); b.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict); b.HasOne<TradeProposalEntity>().WithMany().HasForeignKey(x => x.TradeProposalId).OnDelete(DeleteBehavior.Restrict); b.HasOne<OrderEntity>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_capital_reservations_amount", "CAST(amount AS NUMERIC) > 0"); t.HasCheckConstraint("ck_capital_reservations_status", "status IN ('Active','Consumed','Released','Expired')"); t.HasCheckConstraint("ck_capital_reservations_time", "expires_at > created_at"); t.HasCheckConstraint("ck_capital_reservations_terminal", "(status='Active' AND consumed_at IS NULL AND released_at IS NULL) OR (status='Consumed' AND consumed_at IS NOT NULL AND released_at IS NULL) OR (status IN ('Released','Expired') AND released_at IS NOT NULL AND consumed_at IS NULL)"); t.HasCheckConstraint("ck_capital_reservations_version", "version > 0"); }); }
}

internal sealed class OrderConfiguration : EntityConfiguration<OrderEntity>
{
    public OrderConfiguration() : base("orders") { }
    protected override void ConfigureEntity(EntityTypeBuilder<OrderEntity> b)
    {
        b.Property(x => x.ClientOrderId).IsRequired(); b.Property(x => x.PortfolioId).IsRequired(); b.Property(x => x.BrokerAccountId).IsRequired();
        b.Property(x => x.TradeProposalId).IsRequired(); b.Property(x => x.InstrumentId).IsRequired(); b.Property(x => x.Side).IsRequired();
        b.Property(x => x.Quantity).IsRequired().HasColumnType("TEXT"); b.Property(x => x.QuantityUnit).IsRequired().HasMaxLength(32);
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3); b.Property(x => x.LimitPrice).HasColumnType("TEXT");
        b.Property(x => x.OrderType).IsRequired(); b.Property(x => x.TimeInForce).HasConversion(CanonicalPersistenceConverters.Enumeration<TimeInForce>()).IsRequired();
        b.Property(x => x.Status).HasConversion(CanonicalPersistenceConverters.Enumeration<OrderStatus>()).IsRequired();
        b.Property(x => x.CorrelationId).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.ClientOrderId).IsUnique(); b.HasIndex(x => new { x.BrokerAccountId, x.BrokerOrderId }).IsUnique().HasFilter("broker_order_id IS NOT NULL");
        b.HasIndex(x => new { x.PortfolioId, x.Status, x.CreatedAt }); b.HasIndex(x => x.TradeProposalId); b.HasIndex(x => x.CorrelationId).IsUnique();
        b.HasOne<PortfolioEntity>().WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BrokerAccountEntity>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TradeProposalEntity>().WithMany().HasForeignKey(x => x.TradeProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstrumentEntity>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasCheckConstraint("ck_orders_side", "side IN ('Buy','Sell')"); t.HasCheckConstraint("ck_orders_quantity", "CAST(quantity AS NUMERIC) > 0"); t.HasCheckConstraint("ck_orders_quantity_unit", "length(quantity_unit) BETWEEN 1 AND 32 AND quantity_unit NOT GLOB '*[^a-z]*'"); t.HasCheckConstraint("ck_orders_currency", "length(currency)=3 AND currency NOT GLOB '*[^A-Z]*'"); t.HasCheckConstraint("ck_orders_type", "order_type IN ('Market','Limit')"); t.HasCheckConstraint("ck_orders_limit", "(order_type='Market' AND limit_price IS NULL) OR (order_type='Limit' AND CAST(limit_price AS NUMERIC) > 0)"); t.HasCheckConstraint("ck_orders_time_in_force", "time_in_force IN ('Day','GoodTillCancelled','ImmediateOrCancel','FillOrKill')"); t.HasCheckConstraint("ck_orders_status", "status IN ('Created','Submitting','Submitted','Acknowledged','PartiallyFilled','Filled','CancelPending','Cancelled','Rejected','Expired','Unknown')"); t.HasCheckConstraint("ck_orders_version", "version >= 0"); });
    }
}
internal sealed class OrderTransitionConfiguration : EntityConfiguration<OrderTransitionEntity>
{
    public OrderTransitionConfiguration() : base("order_transitions") { }
    protected override void ConfigureEntity(EntityTypeBuilder<OrderTransitionEntity> b) { b.Property(x => x.OrderId).IsRequired(); b.Property(x => x.PreviousStatus).HasConversion(CanonicalPersistenceConverters.Enumeration<OrderStatus>()).IsRequired(); b.Property(x => x.NewStatus).HasConversion(CanonicalPersistenceConverters.Enumeration<OrderStatus>()).IsRequired(); b.Property(x => x.ReasonCode).IsRequired(); b.Property(x => x.Source).IsRequired(); b.Property(x => x.CorrelationId).IsRequired(); b.HasIndex(x => new { x.OrderId, x.SequenceNumber }).IsUnique(); b.HasIndex(x => x.CorrelationId); b.HasOne<OrderEntity>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_order_transitions_sequence", "sequence_number > 0"); t.HasCheckConstraint("ck_order_transitions_previous_status", "previous_status IN ('Created','Submitting','Submitted','Acknowledged','PartiallyFilled','Filled','CancelPending','Cancelled','Rejected','Expired','Unknown')"); t.HasCheckConstraint("ck_order_transitions_new_status", "new_status IN ('Created','Submitting','Submitted','Acknowledged','PartiallyFilled','Filled','CancelPending','Cancelled','Rejected','Expired','Unknown')"); }); }
}
internal sealed class FillConfiguration : EntityConfiguration<FillEntity>
{
    public FillConfiguration() : base("fills") { }
    protected override void ConfigureEntity(EntityTypeBuilder<FillEntity> b) { b.Property(x => x.OrderId).IsRequired(); b.Property(x => x.BrokerAccountId).IsRequired(); b.Property(x => x.BrokerExecutionId).IsRequired(); b.Property(x => x.Quantity).IsRequired().HasColumnType("TEXT"); b.Property(x => x.Price).IsRequired().HasColumnType("TEXT"); b.Property(x => x.Currency).IsRequired(); b.Property(x => x.FeeAmount).IsRequired().HasColumnType("TEXT"); b.Property(x => x.FeeCurrency).IsRequired(); b.HasIndex(x => new { x.BrokerAccountId, x.BrokerExecutionId }).IsUnique(); b.HasIndex(x => new { x.OrderId, x.ExecutedAt }); b.HasOne<OrderEntity>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne<BrokerAccountEntity>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_fills_quantity", "CAST(quantity AS NUMERIC) > 0"); t.HasCheckConstraint("ck_fills_price", "CAST(price AS NUMERIC) > 0"); t.HasCheckConstraint("ck_fills_fee", "CAST(fee_amount AS NUMERIC) >= 0"); }); }
}
internal sealed class BrokerSubmissionAttemptConfiguration : EntityConfiguration<BrokerSubmissionAttemptEntity>
{
    public BrokerSubmissionAttemptConfiguration() : base("broker_submission_attempts") { }
    protected override void ConfigureEntity(EntityTypeBuilder<BrokerSubmissionAttemptEntity> b)
    {
        b.Property(x => x.OrderId).HasMaxLength(26).IsRequired(); b.Property(x => x.WorkItemId).HasMaxLength(26).IsRequired();
        b.Property(x => x.ClientOrderId).HasMaxLength(200).IsRequired(); b.Property(x => x.CommandHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.AdapterIdentity).HasMaxLength(200).IsRequired(); b.Property(x => x.Environment).HasMaxLength(100).IsRequired();
        b.Property(x => x.Outcome).HasMaxLength(32).IsRequired(); b.Property(x => x.ResultCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.BrokerOrderId).HasMaxLength(200); b.Property(x => x.DiagnosticCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.WorkItemId, x.AttemptNumber }).IsUnique(); b.HasIndex(x => new { x.OrderId, x.StartedAt });
        b.HasOne<OrderEntity>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OutboxMessageEntity>().WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_broker_submission_attempt_number", "attempt_number > 0");
            t.HasCheckConstraint("ck_broker_submission_attempt_hash", "length(command_hash)=64 AND command_hash=lower(command_hash) AND command_hash NOT GLOB '*[^0-9a-f]*'");
            t.HasCheckConstraint("ck_broker_submission_attempt_time", "completed_at >= started_at");
            t.HasCheckConstraint("ck_broker_submission_attempt_outcome", "outcome IN ('Accepted','Rejected','Unknown','TerminalFailure','Duplicate')");
            t.HasCheckConstraint("ck_broker_submission_attempt_broker_id", "(outcome IN ('Accepted','Duplicate') AND broker_order_id IS NOT NULL) OR (outcome NOT IN ('Accepted','Duplicate'))");
        });
    }
}

internal sealed class BrokerReconciliationConfiguration : EntityConfiguration<BrokerReconciliationEntity>
{
    public BrokerReconciliationConfiguration() : base("broker_reconciliations") { }
    protected override void ConfigureEntity(EntityTypeBuilder<BrokerReconciliationEntity> b) { b.Property(x => x.BrokerAccountId).IsRequired(); b.Property(x => x.Status).IsRequired(); b.Property(x => x.BrokerSnapshotJson).IsRequired(); b.Property(x => x.DifferencesJson).IsRequired(); b.Property(x => x.ResolutionJson).IsRequired(); b.Property(x => x.CorrelationId).IsRequired(); b.Property(x => x.ContentHash).IsRequired(); b.HasIndex(x => new { x.BrokerAccountId, x.StartedAt }); b.HasIndex(x => x.CorrelationId).IsUnique(); b.HasOne<BrokerAccountEntity>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_broker_reconciliations_status", "status IN ('Pending','Matched','Discrepancy','Failed')"); t.HasCheckConstraint("ck_broker_reconciliations_hash", "length(content_hash)=64 AND content_hash=lower(content_hash)"); }); }
}
internal sealed class OutboxMessageConfiguration : EntityConfiguration<OutboxMessageEntity>
{
    public OutboxMessageConfiguration() : base("outbox_messages") { }
    protected override void ConfigureEntity(EntityTypeBuilder<OutboxMessageEntity> b) { b.Property(x => x.OrderId).HasMaxLength(26).IsRequired(); b.Property(x => x.WorkKind).HasMaxLength(32).IsRequired(); b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired(); b.Property(x => x.PayloadJson).HasMaxLength(16_384).IsRequired(); b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired(); b.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired(); b.Property(x => x.Status).HasMaxLength(16).IsRequired(); b.Property(x => x.LeaseOwner).HasMaxLength(200); b.Property(x => x.LastError).HasMaxLength(2_000); b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.HasIndex(x => new { x.Status, x.AvailableAt, x.CreatedAt, x.Id }); b.HasIndex(x => new { x.Status, x.LeaseExpiresAt }); b.HasOne<OrderEntity>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_outbox_work_kind", "work_kind IN ('Submit','Reconcile','Cancel','ApplyBrokerEvent')"); t.HasCheckConstraint("ck_outbox_status", "status IN ('Pending','Claimed','Completed','Failed')"); t.HasCheckConstraint("ck_outbox_attempt_count", "attempt_count >= 0"); t.HasCheckConstraint("ck_outbox_hash", "length(payload_hash)=64 AND payload_hash=lower(payload_hash) AND payload_hash NOT GLOB '*[^0-9a-f]*'"); t.HasCheckConstraint("ck_outbox_times", "available_at >= created_at AND (completed_at IS NULL OR completed_at >= created_at)"); t.HasCheckConstraint("ck_outbox_lease", "(status='Claimed' AND lease_owner IS NOT NULL AND lease_expires_at IS NOT NULL) OR (status<>'Claimed' AND lease_owner IS NULL AND lease_expires_at IS NULL)"); t.HasCheckConstraint("ck_outbox_completion", "(status IN ('Completed','Failed') AND completed_at IS NOT NULL) OR (status IN ('Pending','Claimed') AND completed_at IS NULL)"); t.HasCheckConstraint("ck_outbox_version", "version > 0"); }); }
}
internal sealed class InboxMessageConfiguration : EntityConfiguration<InboxMessageEntity>
{
    public InboxMessageConfiguration() : base("inbox_messages") { }
    protected override void ConfigureEntity(EntityTypeBuilder<InboxMessageEntity> b) { b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired(); b.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired(); b.Property(x => x.Status).HasMaxLength(16).IsRequired(); b.Property(x => x.PayloadJson).HasMaxLength(16_384).IsRequired(); b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired(); b.Property(x => x.LeaseOwner).HasMaxLength(200); b.Property(x => x.LastError).HasMaxLength(2_000); b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.HasIndex(x => new { x.Status, x.AvailableAt, x.ReceivedAt, x.Id }); b.HasIndex(x => new { x.Status, x.LeaseExpiresAt }); b.ToTable(t => { t.HasCheckConstraint("ck_inbox_status", "status IN ('Pending','Claimed','Completed','Failed')"); t.HasCheckConstraint("ck_inbox_attempt_count", "attempt_count >= 0"); t.HasCheckConstraint("ck_inbox_hash", "length(payload_hash)=64 AND payload_hash=lower(payload_hash) AND payload_hash NOT GLOB '*[^0-9a-f]*'"); t.HasCheckConstraint("ck_inbox_times", "available_at >= received_at AND (completed_at IS NULL OR completed_at >= received_at)"); t.HasCheckConstraint("ck_inbox_lease", "(status='Claimed' AND lease_owner IS NOT NULL AND lease_expires_at IS NOT NULL) OR (status<>'Claimed' AND lease_owner IS NULL AND lease_expires_at IS NULL)"); t.HasCheckConstraint("ck_inbox_completion", "(status IN ('Completed','Failed') AND completed_at IS NOT NULL) OR (status IN ('Pending','Claimed') AND completed_at IS NULL)"); t.HasCheckConstraint("ck_inbox_version", "version > 0"); }); }
}

internal sealed class SchemaMetadataConfiguration : IEntityTypeConfiguration<SchemaMetadataEntity>
{
    public void Configure(EntityTypeBuilder<SchemaMetadataEntity> builder)
    {
        builder.ToTable("schema_metadata"); builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key"); builder.Property(x => x.Value).HasColumnName("value").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasData(new SchemaMetadataEntity { Key = "application_data_format_version", Value = "7", UpdatedAt = 0 });
    }
}

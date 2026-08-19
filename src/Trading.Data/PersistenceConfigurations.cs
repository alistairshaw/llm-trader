using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trading.Data;

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

internal sealed class SchemaMetadataConfiguration : IEntityTypeConfiguration<SchemaMetadataEntity>
{
    public void Configure(EntityTypeBuilder<SchemaMetadataEntity> builder)
    {
        builder.ToTable("schema_metadata"); builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key"); builder.Property(x => x.Value).HasColumnName("value").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasData(new SchemaMetadataEntity { Key = "application_data_format_version", Value = "3", UpdatedAt = 0 });
    }
}

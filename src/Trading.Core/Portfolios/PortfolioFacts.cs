using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Portfolios;

public enum ReconciliationStatus { Reconciled, Pending, Uncertain }
public enum PortfolioLedgerEntryType { Deposit, Withdrawal, Settlement, Fee, Dividend, Interest, Tax, CorporateAction, ManualCorrection }
public enum LedgerSourceType { BrokerExecution, BrokerEvent, AuditedAdjustment }
public sealed record PositionSnapshot(InstrumentId InstrumentId, decimal Quantity, Money MarketValue);
public sealed record OpenOrderSnapshot(OrderId OrderId, InstrumentId InstrumentId, decimal Quantity);
public sealed record CashFlowSnapshot(Money Amount, DateTimeOffset EffectiveAt, string SourceId);

public sealed class PortfolioDecisionSnapshot
{
    public PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId id, PortfolioId portfolioId, TradingBotId tradingBotId,
        TradingBotConfigurationVersionId configurationVersionId, DateTimeOffset asOf, ReconciliationStatus reconciliationStatus,
        Money cash, Money buyingPower, Money reservedCapital, IEnumerable<PositionSnapshot> positions,
        IEnumerable<OpenOrderSnapshot> openOrders, decimal riskUtilization, IEnumerable<CashFlowSnapshot> cashFlows,
        DateTimeOffset dataFreshAsOf, string contentHash)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)); PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId)); TradingBotId = tradingBotId ?? throw new ArgumentNullException(nameof(tradingBotId)); ConfigurationVersionId = configurationVersionId ?? throw new ArgumentNullException(nameof(configurationVersionId));
        AsOf = PortfolioValidation.Utc(asOf, nameof(asOf)); DataFreshAsOf = PortfolioValidation.Utc(dataFreshAsOf, nameof(dataFreshAsOf)); if (dataFreshAsOf > asOf) throw new ArgumentException("Freshness cannot be later than snapshot time.");
        if (cash.Currency != buyingPower.Currency || cash.Currency != reservedCapital.Currency) throw new ArgumentException("Snapshot monetary values must share a currency.");
        ReconciliationStatus = reconciliationStatus; Cash = cash; BuyingPower = buyingPower; ReservedCapital = reservedCapital; PositionSnapshots = Array.AsReadOnly(positions.ToArray()); OpenOrderSnapshots = Array.AsReadOnly(openOrders.ToArray()); RiskUtilization = riskUtilization; RelevantCashFlows = Array.AsReadOnly(cashFlows.ToArray()); ContentHash = PortfolioValidation.Required(contentHash, nameof(contentHash));
    }
    public PortfolioDecisionSnapshotId Id { get; }
    public PortfolioId PortfolioId { get; }
    public TradingBotId TradingBotId { get; }
    public TradingBotConfigurationVersionId ConfigurationVersionId { get; }
    public DateTimeOffset AsOf { get; }
    public ReconciliationStatus ReconciliationStatus { get; }
    public Money Cash { get; }
    public Money BuyingPower { get; }
    public Money ReservedCapital { get; }
    public IReadOnlyList<PositionSnapshot> PositionSnapshots { get; }
    public IReadOnlyList<OpenOrderSnapshot> OpenOrderSnapshots { get; }
    public decimal RiskUtilization { get; }
    public IReadOnlyList<CashFlowSnapshot> RelevantCashFlows { get; }
    public DateTimeOffset DataFreshAsOf { get; }
    public string ContentHash { get; }
}

public sealed class PortfolioLedgerEntry
{
    public PortfolioLedgerEntry(PortfolioLedgerEntryId id, PortfolioId portfolioId, PortfolioLedgerEntryType entryType,
        Money amount, InstrumentId? instrumentId, decimal? quantity, DateTimeOffset effectiveAt, LedgerSourceType sourceType, string sourceId,
        DateTimeOffset? recordedAt = null, PortfolioLedgerEntryId? reversesEntryId = null, string? description = null, string? metadataJson = null)
    { Id = id ?? throw new ArgumentNullException(nameof(id)); PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId)); EntryType = entryType; Amount = amount ?? throw new ArgumentNullException(nameof(amount)); InstrumentId = instrumentId; Quantity = quantity; EffectiveAt = PortfolioValidation.Utc(effectiveAt, nameof(effectiveAt)); SourceType = sourceType; SourceId = PortfolioValidation.Required(sourceId, nameof(sourceId)); RecordedAt = PortfolioValidation.Utc(recordedAt ?? effectiveAt, nameof(recordedAt)); ReversesEntryId = reversesEntryId; Description = description; MetadataJson = metadataJson; if ((instrumentId is null) != (quantity is null)) throw new ArgumentException("Instrument and quantity must be supplied together."); if ((entryType == PortfolioLedgerEntryType.ManualCorrection) != (reversesEntryId is not null)) throw new ArgumentException("Corrections must reference the entry they reverse."); }
    public PortfolioLedgerEntryId Id { get; }
    public PortfolioId PortfolioId { get; }
    public PortfolioLedgerEntryType EntryType { get; }
    public Money Amount { get; }
    public Currency Currency => Amount.Currency; public InstrumentId? InstrumentId { get; }
    public decimal? Quantity { get; }
    public DateTimeOffset EffectiveAt { get; }
    public LedgerSourceType SourceType { get; }
    public string SourceId { get; }
    public DateTimeOffset RecordedAt { get; }
    public PortfolioLedgerEntryId? ReversesEntryId { get; }
    public string? Description { get; }
    public string? MetadataJson { get; }
}

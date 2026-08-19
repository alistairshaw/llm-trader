using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Portfolios;

public enum ReconciliationStatus { Reconciled, Pending, Uncertain }
public enum PortfolioLedgerEntryType { Deposit, Withdrawal, Settlement, Fee, Dividend, Interest, Tax, CorporateAction, ManualCorrection }
public enum LedgerSourceType { BrokerExecution, BrokerEvent, AuditedAdjustment }
public sealed record PositionSnapshot
{
    public PositionSnapshot(InstrumentId instrumentId, decimal quantity, Money marketValue)
    {
        InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
        MarketValue = marketValue ?? throw new ArgumentNullException(nameof(marketValue));
        Quantity = quantity;
    }

    public InstrumentId InstrumentId { get; }
    public decimal Quantity { get; }
    public Money MarketValue { get; }
}

public sealed record OpenOrderSnapshot
{
    public OpenOrderSnapshot(OrderId orderId, InstrumentId instrumentId, decimal quantity)
    {
        OrderId = orderId ?? throw new ArgumentNullException(nameof(orderId));
        InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
        Quantity = quantity;
    }

    public OrderId OrderId { get; }
    public InstrumentId InstrumentId { get; }
    public decimal Quantity { get; }
}

public sealed record CashFlowSnapshot
{
    public CashFlowSnapshot(Money amount, DateTimeOffset effectiveAt, string sourceId)
    {
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
        EffectiveAt = PortfolioValidation.Utc(effectiveAt, nameof(effectiveAt));
        SourceId = PortfolioValidation.Required(sourceId, nameof(sourceId));
    }

    public Money Amount { get; }
    public DateTimeOffset EffectiveAt { get; }
    public string SourceId { get; }
}

public sealed class PortfolioDecisionSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId id, PortfolioId portfolioId, TradingBotId tradingBotId,
        TradingBotConfigurationVersionId configurationVersionId, DateTimeOffset asOf, ReconciliationStatus reconciliationStatus,
        Money cash, Money buyingPower, Money reservedCapital, IEnumerable<PositionSnapshot> positions,
        IEnumerable<OpenOrderSnapshot> openOrders, decimal riskUtilization, IEnumerable<CashFlowSnapshot> cashFlows,
        DataFreshness dataFreshness, DateTimeOffset createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        TradingBotId = tradingBotId ?? throw new ArgumentNullException(nameof(tradingBotId));
        ConfigurationVersionId = configurationVersionId ?? throw new ArgumentNullException(nameof(configurationVersionId));
        AsOf = MillisecondUtc(asOf, nameof(asOf));
        CreatedAt = MillisecondUtc(createdAt, nameof(createdAt));
        DataFreshness = dataFreshness ?? throw new ArgumentNullException(nameof(dataFreshness));
        MillisecondUtc(dataFreshness.SourceAsOf, nameof(dataFreshness)); MillisecondUtc(dataFreshness.RetrievedAt, nameof(dataFreshness));
        if (dataFreshness.RetrievedAt > asOf) throw new ArgumentException("Freshness retrieval cannot be later than snapshot time.", nameof(dataFreshness));
        if (cash.Currency != buyingPower.Currency || cash.Currency != reservedCapital.Currency) throw new ArgumentException("Snapshot monetary values must share a currency.");
        ArgumentNullException.ThrowIfNull(positions); ArgumentNullException.ThrowIfNull(openOrders); ArgumentNullException.ThrowIfNull(cashFlows);
        ReconciliationStatus = reconciliationStatus; Cash = cash; BuyingPower = buyingPower; ReservedCapital = reservedCapital;
        PositionSnapshots = Array.AsReadOnly(positions.OrderBy(x => x.InstrumentId.ToString(), StringComparer.Ordinal).ToArray());
        OpenOrderSnapshots = Array.AsReadOnly(openOrders.OrderBy(x => x.OrderId.ToString(), StringComparer.Ordinal).ToArray());
        RelevantCashFlows = Array.AsReadOnly(cashFlows.OrderBy(x => x.EffectiveAt).ThenBy(x => x.SourceId, StringComparer.Ordinal).ToArray());
        foreach (var cashFlow in RelevantCashFlows) MillisecondUtc(cashFlow.EffectiveAt, nameof(cashFlows));
        if (PositionSnapshots.Any(x => x.MarketValue.Currency != cash.Currency) || RelevantCashFlows.Any(x => x.Amount.Currency != cash.Currency)) throw new ArgumentException("Snapshot monetary values must share a currency.");
        if (PositionSnapshots.Select(x => x.InstrumentId).Distinct().Count() != PositionSnapshots.Count) throw new ArgumentException("Position instruments must be unique.", nameof(positions));
        if (OpenOrderSnapshots.Select(x => x.OrderId).Distinct().Count() != OpenOrderSnapshots.Count) throw new ArgumentException("Open order identities must be unique.", nameof(openOrders));
        RiskUtilization = riskUtilization;
        SnapshotSchemaVersion = CurrentSchemaVersion;
        CanonicalContent = RenderCanonicalContent(this);
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalContent))).ToLowerInvariant();
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
    public DataFreshness DataFreshness { get; }
    public int SnapshotSchemaVersion { get; }
    public string CanonicalContent { get; }
    public string ContentHash { get; }
    public DateTimeOffset CreatedAt { get; }

    private static string RenderCanonicalContent(PortfolioDecisionSnapshot value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WritePropertyName("content"); writer.WriteStartObject();
            writer.WriteString("asOf", FormatTimestamp(value.AsOf)); writer.WriteString("buyingPower", FormatDecimal(value.BuyingPower.Amount)); writer.WriteString("cash", FormatDecimal(value.Cash.Amount)); writer.WriteString("configurationVersionId", value.ConfigurationVersionId.ToString()); writer.WriteString("currency", value.Cash.Currency.Code);
            writer.WritePropertyName("dataFreshness"); writer.WriteStartObject(); writer.WriteNumber("maximumAgeTicks", value.DataFreshness.MaximumAge.Ticks); writer.WriteString("retrievedAt", FormatTimestamp(value.DataFreshness.RetrievedAt)); writer.WriteString("sourceAsOf", FormatTimestamp(value.DataFreshness.SourceAsOf)); writer.WriteEndObject();
            writer.WritePropertyName("openOrders"); writer.WriteStartArray();
            foreach (var item in value.OpenOrderSnapshots) { writer.WriteStartObject(); writer.WriteString("instrumentId", item.InstrumentId.ToString()); writer.WriteString("orderId", item.OrderId.ToString()); writer.WriteString("quantity", FormatDecimal(item.Quantity)); writer.WriteEndObject(); }
            writer.WriteEndArray();
            writer.WriteString("portfolioId", value.PortfolioId.ToString());
            writer.WritePropertyName("positions"); writer.WriteStartArray();
            foreach (var item in value.PositionSnapshots) { writer.WriteStartObject(); writer.WriteString("instrumentId", item.InstrumentId.ToString()); writer.WriteString("marketValue", FormatDecimal(item.MarketValue.Amount)); writer.WriteString("quantity", FormatDecimal(item.Quantity)); writer.WriteEndObject(); }
            writer.WriteEndArray();
            writer.WriteString("reconciliationStatus", value.ReconciliationStatus.ToString());
            writer.WritePropertyName("relevantCashFlows"); writer.WriteStartArray();
            foreach (var item in value.RelevantCashFlows) { writer.WriteStartObject(); writer.WriteString("amount", FormatDecimal(item.Amount.Amount)); writer.WriteString("effectiveAt", FormatTimestamp(item.EffectiveAt)); writer.WriteString("sourceId", item.SourceId); writer.WriteEndObject(); }
            writer.WriteEndArray();
            writer.WriteString("reservedCapital", FormatDecimal(value.ReservedCapital.Amount)); writer.WriteString("riskUtilization", FormatDecimal(value.RiskUtilization)); writer.WriteString("tradingBotId", value.TradingBotId.ToString()); writer.WriteEndObject(); writer.WriteNumber("schemaVersion", CurrentSchemaVersion); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string FormatDecimal(decimal value) => value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);
    private static string FormatTimestamp(DateTimeOffset value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    private static DateTimeOffset MillisecondUtc(DateTimeOffset value, string parameterName)
    {
        PortfolioValidation.Utc(value, parameterName);
        if (DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds()) != value) throw new ArgumentException("Snapshot timestamps must have millisecond precision.", parameterName);
        return value;
    }
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

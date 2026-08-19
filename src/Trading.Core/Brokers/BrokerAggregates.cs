using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Portfolios;

namespace Trading.Core.Brokers;

public enum BrokerEnvironment { Paper, Live }
public enum BrokerConnectionStatus { Enabled, Disabled, Disconnected }
public enum BrokerAccountStatus { Active, Restricted, Disabled }
public enum InstrumentStatus { Active, Inactive }
public enum InstrumentType { Equity, Option, Fund, Bond, Cash, Crypto }

public sealed class BrokerConnection
{
    public BrokerConnection(BrokerConnectionId id, string brokerType, string displayName, BrokerEnvironment environment,
        string credentialReference, IEnumerable<string> capabilities, DateTimeOffset createdAt)
        : this(id, brokerType, displayName, environment, credentialReference, capabilities,
            BrokerConnectionStatus.Disabled, createdAt, createdAt, 0)
    { }
    private BrokerConnection(BrokerConnectionId id, string brokerType, string displayName, BrokerEnvironment environment,
        string credentialReference, IEnumerable<string> capabilities, BrokerConnectionStatus status,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, long version)
    { Id = id ?? throw new ArgumentNullException(nameof(id)); BrokerType = PortfolioValidation.Required(brokerType, nameof(brokerType)); DisplayName = PortfolioValidation.Required(displayName, nameof(displayName)); Environment = environment; CredentialReference = PortfolioValidation.Required(credentialReference, nameof(credentialReference)); Capabilities = Array.AsReadOnly(capabilities.Select(x => PortfolioValidation.Required(x, nameof(capabilities))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()); Status = status; CreatedAt = PortfolioValidation.Utc(createdAt, nameof(createdAt)); UpdatedAt = PortfolioValidation.Utc(updatedAt, nameof(updatedAt)); ArgumentOutOfRangeException.ThrowIfNegative(version); Version = version; }
    public BrokerConnectionId Id { get; }
    public string BrokerType { get; }
    public string DisplayName { get; }
    public BrokerEnvironment Environment { get; }
    public string CredentialReference { get; }
    public BrokerConnectionStatus Status { get; private set; }
    public IReadOnlyList<string> Capabilities { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }
    public static BrokerConnection Rehydrate(BrokerConnectionId id, string brokerType, string displayName,
        BrokerEnvironment environment, string credentialReference, IEnumerable<string> capabilities,
        BrokerConnectionStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt, long version) =>
        new(id, brokerType, displayName, environment, credentialReference, capabilities, status, createdAt, updatedAt, version);
    public void Enable() => Status = BrokerConnectionStatus.Enabled; public void Disable() => Status = BrokerConnectionStatus.Disabled; public void MarkDisconnected() => Status = BrokerConnectionStatus.Disconnected;
    public void AuthorizeOperation() { if (Status != BrokerConnectionStatus.Enabled) throw new InvalidOperationException("Disabled or disconnected broker connections reject operations."); }
}

public sealed class BrokerAccount
{
    public BrokerAccount(BrokerAccountId id, BrokerConnectionId connectionId, string externalAccountId, string displayName, string accountType, Currency baseCurrency,
        IEnumerable<string>? capabilities = null, DateTimeOffset? createdAt = null)
        : this(id, connectionId, externalAccountId, displayName, accountType, baseCurrency, BrokerAccountStatus.Active,
            null, capabilities ?? [], createdAt ?? DateTimeOffset.UnixEpoch, createdAt ?? DateTimeOffset.UnixEpoch, 0)
    { }
    private BrokerAccount(BrokerAccountId id, BrokerConnectionId connectionId, string externalAccountId, string displayName,
        string accountType, Currency baseCurrency, BrokerAccountStatus status, DateTimeOffset? lastReconciledAt,
        IEnumerable<string> capabilities, DateTimeOffset createdAt, DateTimeOffset updatedAt, long version)
    { Id = id ?? throw new ArgumentNullException(nameof(id)); BrokerConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId)); ExternalAccountId = PortfolioValidation.Required(externalAccountId, nameof(externalAccountId)); DisplayName = PortfolioValidation.Required(displayName, nameof(displayName)); AccountType = PortfolioValidation.Required(accountType, nameof(accountType)); BaseCurrency = baseCurrency ?? throw new ArgumentNullException(nameof(baseCurrency)); Status = status; LastReconciledAt = lastReconciledAt is null ? null : PortfolioValidation.Utc(lastReconciledAt.Value, nameof(lastReconciledAt)); Capabilities = Array.AsReadOnly(capabilities.Select(x => PortfolioValidation.Required(x, nameof(capabilities))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()); CreatedAt = PortfolioValidation.Utc(createdAt, nameof(createdAt)); UpdatedAt = PortfolioValidation.Utc(updatedAt, nameof(updatedAt)); ArgumentOutOfRangeException.ThrowIfNegative(version); Version = version; }
    public BrokerAccountId Id { get; }
    public BrokerConnectionId BrokerConnectionId { get; }
    public string ExternalAccountId { get; }
    public string DisplayName { get; }
    public string AccountType { get; }
    public Currency BaseCurrency { get; }
    public BrokerAccountStatus Status { get; private set; }
    public DateTimeOffset? LastReconciledAt { get; private set; }
    public IReadOnlyList<string> Capabilities { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }
    public PortfolioId? ActivePortfolioId { get; private set; }
    public void Reconcile(DateTimeOffset at) { LastReconciledAt = PortfolioValidation.Utc(at, nameof(at)); }
    public void Restrict() => Status = BrokerAccountStatus.Restricted; public void Disable() => Status = BrokerAccountStatus.Disabled; public void Activate() => Status = BrokerAccountStatus.Active;
    public void AssignPortfolio(PortfolioId id) { ArgumentNullException.ThrowIfNull(id); if (ActivePortfolioId is not null && ActivePortfolioId != id) throw new InvalidOperationException("A broker account supports at most one active portfolio."); ActivePortfolioId = id; }
    public void AuthorizeNewOrder() { if (Status != BrokerAccountStatus.Active || LastReconciledAt is null) throw new InvalidOperationException("Unreconciled, restricted, or disabled accounts reject new orders."); }
    public static BrokerAccount Rehydrate(BrokerAccountId id, BrokerConnectionId connectionId, string externalAccountId,
        string displayName, string accountType, Currency baseCurrency, BrokerAccountStatus status,
        DateTimeOffset? lastReconciledAt, IEnumerable<string> capabilities, DateTimeOffset createdAt,
        DateTimeOffset updatedAt, long version) => new(id, connectionId, externalAccountId, displayName, accountType,
            baseCurrency, status, lastReconciledAt, capabilities, createdAt, updatedAt, version);
}

public sealed class Instrument
{
    private readonly List<InstrumentBrokerMapping> _mappings = [];
    public Instrument(InstrumentId id, InstrumentType instrumentType, string primarySymbol, string displayName, Currency currency, string exchange,
        int pricePrecision = 8, int quantityPrecision = 8, DateTimeOffset? createdAt = null)
        : this(id, instrumentType, primarySymbol, displayName, currency, exchange, pricePrecision, quantityPrecision,
            InstrumentStatus.Active, createdAt ?? DateTimeOffset.UnixEpoch, createdAt ?? DateTimeOffset.UnixEpoch, 0)
    { }
    private Instrument(InstrumentId id, InstrumentType instrumentType, string primarySymbol, string displayName,
        Currency currency, string exchange, int pricePrecision, int quantityPrecision, InstrumentStatus status,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, long version)
    { Id = id ?? throw new ArgumentNullException(nameof(id)); InstrumentType = instrumentType; PrimarySymbol = PortfolioValidation.Required(primarySymbol, nameof(primarySymbol)); DisplayName = PortfolioValidation.Required(displayName, nameof(displayName)); Currency = currency ?? throw new ArgumentNullException(nameof(currency)); Exchange = PortfolioValidation.Required(exchange, nameof(exchange)); ArgumentOutOfRangeException.ThrowIfNegative(pricePrecision); ArgumentOutOfRangeException.ThrowIfGreaterThan(pricePrecision, 8); ArgumentOutOfRangeException.ThrowIfNegative(quantityPrecision); ArgumentOutOfRangeException.ThrowIfGreaterThan(quantityPrecision, 8); PricePrecision = pricePrecision; QuantityPrecision = quantityPrecision; Status = status; CreatedAt = PortfolioValidation.Utc(createdAt, nameof(createdAt)); UpdatedAt = PortfolioValidation.Utc(updatedAt, nameof(updatedAt)); ArgumentOutOfRangeException.ThrowIfNegative(version); Version = version; }
    public InstrumentId Id { get; }
    public InstrumentType InstrumentType { get; }
    public string PrimarySymbol { get; }
    public string DisplayName { get; }
    public Currency Currency { get; }
    public string Exchange { get; }
    public int PricePrecision { get; }
    public int QuantityPrecision { get; }
    public InstrumentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long Version { get; }
    public IReadOnlyList<InstrumentBrokerMapping> BrokerMappings => _mappings.AsReadOnly();
    public void Deactivate() => Status = InstrumentStatus.Inactive; public void AuthorizeTrading() { if (Status != InstrumentStatus.Active) throw new InvalidOperationException("Inactive instruments cannot be traded."); }
    public InstrumentBrokerMapping AddBrokerMapping(InstrumentBrokerMappingId id, BrokerConnectionId connectionId, string externalInstrumentId, string symbol, string exchange, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo = null)
    { var mapping = new InstrumentBrokerMapping(id, connectionId, externalInstrumentId, symbol, exchange, effectiveFrom, effectiveTo); if (_mappings.Any(existing => existing.BrokerConnectionId == connectionId && existing.Overlaps(mapping))) throw new InvalidOperationException("Broker mapping intervals cannot overlap ambiguously."); _mappings.Add(mapping); return mapping; }
    public InstrumentBrokerMapping Resolve(BrokerConnectionId connectionId, DateTimeOffset at) { AuthorizeTrading(); PortfolioValidation.Utc(at, nameof(at)); return _mappings.SingleOrDefault(m => m.BrokerConnectionId == connectionId && m.IsEffectiveAt(at)) ?? throw new InvalidOperationException("Instrument mapping is unresolved."); }
    public static Instrument Rehydrate(InstrumentId id, InstrumentType instrumentType, string primarySymbol,
        string displayName, Currency currency, string exchange, int pricePrecision, int quantityPrecision,
        InstrumentStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt, long version,
        IEnumerable<InstrumentBrokerMappingState> mappings)
    { var instrument = new Instrument(id, instrumentType, primarySymbol, displayName, currency, exchange, pricePrecision, quantityPrecision, status, createdAt, updatedAt, version); foreach (var mapping in mappings.OrderBy(x => x.EffectiveFrom)) instrument.AddBrokerMapping(mapping.Id, mapping.BrokerConnectionId, mapping.ExternalInstrumentId, mapping.Symbol, mapping.Exchange, mapping.EffectiveFrom, mapping.EffectiveTo); return instrument; }
}

public sealed record InstrumentBrokerMappingState(InstrumentBrokerMappingId Id, BrokerConnectionId BrokerConnectionId,
    string ExternalInstrumentId, string Symbol, string Exchange, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);

public sealed class InstrumentBrokerMapping
{
    internal InstrumentBrokerMapping(InstrumentBrokerMappingId id, BrokerConnectionId connectionId, string externalInstrumentId, string symbol, string exchange, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    { Id = id ?? throw new ArgumentNullException(nameof(id)); BrokerConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId)); ExternalInstrumentId = PortfolioValidation.Required(externalInstrumentId, nameof(externalInstrumentId)); Symbol = PortfolioValidation.Required(symbol, nameof(symbol)); Exchange = PortfolioValidation.Required(exchange, nameof(exchange)); EffectiveFrom = PortfolioValidation.Utc(effectiveFrom, nameof(effectiveFrom)); EffectiveTo = effectiveTo is null ? null : PortfolioValidation.Utc(effectiveTo.Value, nameof(effectiveTo)); if (EffectiveTo <= EffectiveFrom) throw new ArgumentException("Mapping end must be after its start.", nameof(effectiveTo)); }
    public InstrumentBrokerMappingId Id { get; }
    public BrokerConnectionId BrokerConnectionId { get; }
    public string ExternalInstrumentId { get; }
    public string Symbol { get; }
    public string Exchange { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public DateTimeOffset? EffectiveTo { get; }
    internal bool IsEffectiveAt(DateTimeOffset at) => at >= EffectiveFrom && (EffectiveTo is null || at < EffectiveTo); internal bool Overlaps(InstrumentBrokerMapping other) => EffectiveFrom < (other.EffectiveTo ?? DateTimeOffset.MaxValue) && other.EffectiveFrom < (EffectiveTo ?? DateTimeOffset.MaxValue);
}

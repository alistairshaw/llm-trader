using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Portfolios;

namespace Trading.Core.Persistence;

public abstract record PersistenceWriteResult
{
    private PersistenceWriteResult() { }

    public sealed record Succeeded : PersistenceWriteResult;

    public sealed record UniquenessConflict : PersistenceWriteResult
    {
        public UniquenessConflict(string constraint) => Constraint = RequireValue(constraint, nameof(constraint));
        public string Constraint { get; }
    }

    public sealed record ConcurrencyConflict : PersistenceWriteResult
    {
        public ConcurrencyConflict(long expectedVersion, long? actualVersion)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
            if (actualVersion is not null) ArgumentOutOfRangeException.ThrowIfNegative(actualVersion.Value);
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        public long ExpectedVersion { get; }
        public long? ActualVersion { get; }
    }

    private static string RequireValue(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IBrokerConnectionRepository
{
    Task<BrokerConnection?> GetAsync(BrokerConnectionId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(BrokerConnection connection, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(BrokerConnection connection, long expectedVersion, CancellationToken cancellationToken);
}

public interface IBrokerAccountRepository
{
    Task<BrokerAccount?> GetAsync(BrokerAccountId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(BrokerAccount account, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(BrokerAccount account, long expectedVersion, CancellationToken cancellationToken);
}

public interface IInstrumentRepository
{
    Task<Instrument?> GetAsync(InstrumentId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(Instrument instrument, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(Instrument instrument, long expectedVersion, CancellationToken cancellationToken);
}

public interface ITradingBotRepository
{
    Task<TradingBot?> GetAsync(TradingBotId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(TradingBot bot, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(TradingBot bot, long expectedVersion, CancellationToken cancellationToken);
}

public interface IPortfolioRepository
{
    Task<Portfolio?> GetAsync(PortfolioId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(Portfolio portfolio, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(Portfolio portfolio, long expectedVersion, CancellationToken cancellationToken);
}

public interface IPositionRepository
{
    Task<Position?> GetAsync(PositionId id, CancellationToken cancellationToken);
    Task<Position?> GetForPortfolioInstrumentAsync(PortfolioId portfolioId, InstrumentId instrumentId, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(Position position, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(Position position, long expectedVersion, CancellationToken cancellationToken);
}

public interface IPortfolioLedgerRepository
{
    Task<PortfolioLedgerEntry?> GetAsync(PortfolioLedgerEntryId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AppendAsync(PortfolioLedgerEntry entry, CancellationToken cancellationToken);
}

public interface IPortfolioDecisionSnapshotRepository
{
    Task<PortfolioDecisionSnapshot?> GetAsync(PortfolioDecisionSnapshotId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> PublishAsync(PortfolioDecisionSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed record PortfolioSummary(
    PortfolioId Id,
    string Name,
    Currency BaseCurrency,
    PortfolioStatus Status,
    Money CapitalAllocation,
    BrokerAccountId? BrokerAccountId,
    TradingBotId? AssignedTradingBotId);

public sealed record PositionView(
    PositionId Id,
    PortfolioId PortfolioId,
    InstrumentId InstrumentId,
    decimal Quantity,
    string QuantityUnit,
    Money AverageCost,
    Money RealizedProfitLoss,
    long Version,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);

public sealed record PortfolioLedgerEntryView(
    PortfolioLedgerEntryId Id,
    PortfolioId PortfolioId,
    PortfolioLedgerEntryType EntryType,
    Money Amount,
    InstrumentId? InstrumentId,
    decimal? Quantity,
    DateTimeOffset EffectiveAt,
    LedgerSourceType SourceType,
    string SourceId);

public sealed record PortfolioDecisionSnapshotSummary(
    PortfolioDecisionSnapshotId Id,
    PortfolioId PortfolioId,
    TradingBotId TradingBotId,
    TradingBotConfigurationVersionId ConfigurationVersionId,
    DateTimeOffset AsOf,
    ReconciliationStatus ReconciliationStatus,
    string ContentHash);

public interface IPortfolioQueries
{
    Task<PortfolioSummary?> GetSummaryAsync(PortfolioId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PositionView>> GetPositionsAsync(PortfolioId portfolioId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioLedgerEntryView>> GetLedgerAsync(PortfolioId portfolioId, DateTimeOffset? effectiveFrom, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioDecisionSnapshotSummary>> GetDecisionSnapshotsAsync(PortfolioId portfolioId, CancellationToken cancellationToken);
}

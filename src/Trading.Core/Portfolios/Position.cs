using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Portfolios;

public enum PositionChangeSource { Execution, AuditedAdjustment }

public sealed class Position
{
    private readonly HashSet<string> _appliedSources = new(StringComparer.Ordinal);
    public Position(PositionId id, PortfolioId portfolioId, InstrumentId instrumentId, string quantityUnit, Currency costCurrency, DateTimeOffset openedAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)); PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId)); InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
        QuantityUnit = PortfolioValidation.Required(quantityUnit, nameof(quantityUnit)); AverageCost = Money.Zero(costCurrency); RealizedProfitLoss = Money.Zero(costCurrency); OpenedAt = UpdatedAt = PortfolioValidation.Utc(openedAt, nameof(openedAt));
    }
    public PositionId Id { get; }
    public PortfolioId PortfolioId { get; }
    public InstrumentId InstrumentId { get; }
    public decimal Quantity { get; private set; }
    public string QuantityUnit { get; }
    public Money AverageCost { get; private set; }
    public Money RealizedProfitLoss { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset OpenedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public IReadOnlyCollection<string> AppliedSources => _appliedSources;

    public bool ApplyChange(decimal quantityDelta, Money averageCost, Money realizedProfitLossDelta,
        PositionChangeSource sourceType, string sourceId, DateTimeOffset changedAt)
    {
        if (!Enum.IsDefined(sourceType)) throw new ArgumentOutOfRangeException(nameof(sourceType));
        var source = $"{sourceType}:{PortfolioValidation.Required(sourceId, nameof(sourceId))}";
        if (_appliedSources.Contains(source)) return false;
        PortfolioValidation.Utc(changedAt, nameof(changedAt));
        if (changedAt < UpdatedAt) throw new ArgumentException("Change time cannot move backwards.", nameof(changedAt));
        if (averageCost.Currency != AverageCost.Currency || realizedProfitLossDelta.Currency != AverageCost.Currency) throw new ArgumentException("Position money must use its cost currency.");
        var nextQuantity = checked(Quantity + quantityDelta);
        if (nextQuantity < 0) throw new InvalidOperationException("Position quantity cannot be negative.");
        Quantity = nextQuantity; AverageCost = nextQuantity == 0 ? Money.Zero(AverageCost.Currency) : averageCost;
        RealizedProfitLoss += realizedProfitLossDelta; UpdatedAt = changedAt; ClosedAt = nextQuantity == 0 ? changedAt : null; Version++; _appliedSources.Add(source); return true;
    }

    public static Position Rehydrate(PositionId id, PortfolioId portfolioId, InstrumentId instrumentId, string quantityUnit,
        decimal quantity, Money averageCost, Money realizedProfitLoss, long version, DateTimeOffset openedAt,
        DateTimeOffset updatedAt, DateTimeOffset? closedAt, IEnumerable<string> appliedSources)
    {
        var position = new Position(id, portfolioId, instrumentId, quantityUnit, averageCost.Currency, openedAt)
        {
            Quantity = quantity,
            AverageCost = averageCost,
            RealizedProfitLoss = realizedProfitLoss,
            Version = version,
            UpdatedAt = PortfolioValidation.Utc(updatedAt, nameof(updatedAt)),
            ClosedAt = closedAt is null ? null : PortfolioValidation.Utc(closedAt.Value, nameof(closedAt))
        };
        foreach (var source in appliedSources) position._appliedSources.Add(PortfolioValidation.Required(source, nameof(appliedSources)));
        return position;
    }
}

using Trading.Core.Identifiers;

namespace Trading.Core.Orders;

public sealed record ExecutionQueryPrincipal(string ActorId, bool IsAdministrator,
    IReadOnlyCollection<TradingBotId> TradingBotIds, IReadOnlyCollection<PortfolioId> PortfolioIds,
    IReadOnlyCollection<BrokerAccountId> BrokerAccountIds, IReadOnlyCollection<string>? RestrictedReportGroups = null);

public readonly record struct ExecutionPageRequest
{
    public const int MaximumSize = 100;
    public ExecutionPageRequest(int offset, int size)
    {
        if (offset < 0 || size is < 1 or > MaximumSize) throw new ArgumentOutOfRangeException(nameof(size));
        Offset = offset; Size = size;
    }
    public int Offset { get; }
    public int Size { get; }
}

public sealed record OrderQueryFilter(TradingBotId? TradingBotId = null, PortfolioId? PortfolioId = null,
    BrokerAccountId? BrokerAccountId = null, TradeProposalId? ProposalId = null, OrderStatus? Status = null,
    string? Environment = null, DateTimeOffset? From = null, DateTimeOffset? Through = null);

public sealed record OrderListItem(OrderId Id, string ClientOrderId, TradingBotId TradingBotId, PortfolioId PortfolioId,
    BrokerAccountId BrokerAccountId, TradeProposalId ProposalId, InstrumentId InstrumentId, OrderSide Side,
    decimal Quantity, string QuantityUnit, string Currency, OrderStatus Status, string CorrelationId,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public sealed record FillProjection(FillId Id, string BrokerExecutionId, decimal Quantity, decimal Price,
    string Currency, decimal Fee, DateTimeOffset ExecutedAt, DateTimeOffset ReceivedAt);
public sealed record ExecutionAuditEvent(string Kind, string Id, DateTimeOffset At, string Status,
    string CorrelationId, string? ReasonCode, string? Summary);
public sealed record OrderExecutionDetail(OrderListItem Order, string? BrokerOrderId, decimal FilledQuantity,
    decimal GrossAmount, decimal Fees, string? ReservationStatus, decimal? RemainingReservation,
    IReadOnlyList<FillProjection> Fills, IReadOnlyList<ExecutionAuditEvent> Audit);

public interface IOrderExecutionQueries
{
    Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(ExecutionQueryPrincipal principal, OrderQueryFilter filter,
        ExecutionPageRequest page, CancellationToken cancellationToken);
    Task<OrderExecutionDetail?> GetOrderAsync(ExecutionQueryPrincipal principal, OrderId id, CancellationToken cancellationToken);
}

using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Proposals;

public enum CapitalReservationStatus { Active, Consumed, Released, Expired }

public sealed class CapitalReservation
{
    public CapitalReservation(CapitalReservationId id, TradeProposal proposal, Money amount,
        DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.Status != ProposalStatus.Approved)
            throw new InvalidOperationException("Capital can only be reserved for an approved proposal.");
        PortfolioId = proposal.PortfolioId;
        TradeProposalId = proposal.Id;
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
        if (amount.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Reservation amount must be positive.");
        CreatedAt = ProposalValidation.Utc(createdAt, nameof(createdAt));
        ExpiresAt = ProposalValidation.Utc(expiresAt, nameof(expiresAt));
        if (expiresAt <= createdAt) throw new ArgumentException("Reservation expiry must follow creation.", nameof(expiresAt));
        Status = CapitalReservationStatus.Active;
    }
    private CapitalReservation(CapitalReservationState state)
    {
        Id = state.Id; PortfolioId = state.PortfolioId; TradeProposalId = state.TradeProposalId;
        OrderId = state.OrderId; Amount = state.Amount; Status = state.Status;
        CreatedAt = state.CreatedAt; ExpiresAt = state.ExpiresAt; ConsumedAt = state.ConsumedAt;
        ReleasedAt = state.ReleasedAt; Version = state.Version;
    }
    public static CapitalReservation Rehydrate(CapitalReservationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Amount.Amount <= 0 || state.ExpiresAt <= state.CreatedAt || state.Version < 0)
            throw new ArgumentException("Persisted reservation state is invalid.", nameof(state));
        return new CapitalReservation(state);
    }
    public CapitalReservationId Id { get; }
    public PortfolioId PortfolioId { get; }
    public TradeProposalId TradeProposalId { get; }
    public OrderId? OrderId { get; private set; }
    public Money Amount { get; }
    public Currency Currency => Amount.Currency;
    public CapitalReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public long Version { get; private set; }

    public bool AttachToOrder(OrderId orderId)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        EnsureActive();
        if (OrderId is not null)
        {
            if (OrderId == orderId) return false;
            throw new InvalidOperationException("Reservation is already attached to another order.");
        }
        OrderId = orderId; Version++; return true;
    }
    public bool Consume(DateTimeOffset at)
    {
        if (Status == CapitalReservationStatus.Consumed) return false;
        EnsureActive();
        ValidateTransitionTime(at);
        Status = CapitalReservationStatus.Consumed; ConsumedAt = at; Version++; return true;
    }
    public bool Release(DateTimeOffset at)
    {
        if (Status == CapitalReservationStatus.Released) return false;
        EnsureActive();
        ValidateTransitionTime(at);
        Status = CapitalReservationStatus.Released; ReleasedAt = at; Version++; return true;
    }
    public bool Expire(DateTimeOffset at)
    {
        if (Status == CapitalReservationStatus.Expired) return false;
        EnsureActive();
        ValidateTransitionTime(at);
        if (at < ExpiresAt) throw new InvalidOperationException("Reservation has not reached its expiry.");
        Status = CapitalReservationStatus.Expired; ReleasedAt = at; Version++; return true;
    }
    private void EnsureActive()
    {
        if (Status != CapitalReservationStatus.Active) throw new InvalidOperationException("Terminal reservation cannot transition.");
    }
    private void ValidateTransitionTime(DateTimeOffset at)
    {
        ProposalValidation.Utc(at, nameof(at));
        if (at < CreatedAt) throw new ArgumentException("Transition cannot precede creation.", nameof(at));
    }
}

public sealed record CapitalReservationState(CapitalReservationId Id, PortfolioId PortfolioId,
    TradeProposalId TradeProposalId, OrderId? OrderId, Money Amount, CapitalReservationStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? ConsumedAt,
    DateTimeOffset? ReleasedAt, long Version);

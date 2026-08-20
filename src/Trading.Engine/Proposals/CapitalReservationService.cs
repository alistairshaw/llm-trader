using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Proposals;

namespace Trading.Engine.Proposals;

public sealed class CapitalReservationService(
    ITradeProposalRepository proposals,
    IAtomicCapitalReservationRepository atomicReservations,
    ICapitalReservationRepository reservations) : ICapitalReservationService
{
    public async Task<CapitalReservationResult> ReserveAsync(
        CapitalReservationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var proposal = await proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null)
            return Reject("capital_reservation.proposal_not_found");
        if (proposal.PortfolioId != command.PortfolioId)
            return Reject(ProposalGovernanceCodes.PortfolioNotAssigned);
        if (proposal.Status != ProposalStatus.Approved)
            return Reject("capital_reservation.proposal_not_approved");
        if (proposal.ContentVersion != command.ApprovedContentVersion)
            return Reject(ProposalGovernanceCodes.VersionMismatch);
        if (command.CreatedAt >= proposal.ValidUntil || command.ExpiresAt > proposal.ValidUntil)
            return Reject(ProposalGovernanceCodes.Expired);
        if (command.ExpiresAt <= command.CreatedAt)
            return Reject("capital_reservation.invalid_expiration");
        if (command.Amount.Amount <= 0 || command.Amount.Currency != command.GrossAvailableCapital.Currency)
            return Reject("capital_reservation.invalid_amount");
        if (!IsDeterministicAmount(proposal.RequestedAction, command.Amount, command.GrossAvailableCapital))
            return Reject("capital_reservation.amount_mismatch");

        var reservation = new CapitalReservation(command.ReservationId, proposal, command.Amount,
            command.CreatedAt, command.ExpiresAt);
        var write = await atomicReservations.TryReserveAsync(new AtomicCapitalReservationRequest(
            reservation, proposal.TradingBotId, command.ApprovedContentVersion, command.ValidatedState,
            command.GrossAvailableCapital, command.CreatedAt), cancellationToken).ConfigureAwait(false);
        return write switch
        {
            AtomicCapitalReservationWriteResult.Reserved result =>
                new(CapitalReservationOutcome.Reserved, "capital_reservation.reserved", result.Reservation),
            AtomicCapitalReservationWriteResult.AlreadyReserved result =>
                new(CapitalReservationOutcome.AlreadyReserved, "capital_reservation.already_reserved", result.Reservation),
            AtomicCapitalReservationWriteResult.Rejected result => Reject(result.Code),
            _ => new(CapitalReservationOutcome.ConcurrencyConflict,
                ProposalGovernanceCodes.ConcurrencyConflict, null),
        };
    }

    private static bool IsDeterministicAmount(RequestedAction action, Money amount, Money grossAvailable) => action switch
    {
        DirectTradeAction { Side: TradeSide.Buy, LimitPrice: not null } direct =>
            direct.LimitPrice.Currency == amount.Currency &&
            direct.LimitPrice.Amount * direct.Quantity.Amount == amount.Amount,
        TargetAllocationAction target =>
            grossAvailable.Amount * target.TargetPercentage.Value / 100m == amount.Amount,
        _ => false,
    };

    private static CapitalReservationResult Reject(string code) =>
        new(CapitalReservationOutcome.Rejected, code, null);

    public async Task<CapitalReservationReleaseResult> ReleaseAsync(TradeProposalId proposalId,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        var proposal = await proposals.GetAsync(proposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null)
            return new(false, "capital_reservation.proposal_not_found", null);
        if (proposal.Status is not ProposalStatus.Rejected and not ProposalStatus.Cancelled)
            return new(false, "capital_reservation.release_not_authorized", null);
        var reservation = await reservations.GetActiveAsync(proposalId, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
            return new(false, "capital_reservation.already_released", null);
        var expectedVersion = reservation.Version;
        if (!reservation.Release(at))
            return new(false, "capital_reservation.already_released", reservation);
        var result = await reservations.SaveAsync(reservation, expectedVersion, cancellationToken).ConfigureAwait(false);
        return result is PersistenceWriteResult.Succeeded
            ? new(true, "capital_reservation.released", reservation)
            : new(false, ProposalGovernanceCodes.ConcurrencyConflict, null);
    }

    public Task<int> ExpireAsync(PortfolioId portfolioId, DateTimeOffset at,
        CancellationToken cancellationToken) => reservations.ExpireAsync(portfolioId, at, cancellationToken);
}

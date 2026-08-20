using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Proposals;

namespace Trading.Engine.Proposals;

public sealed class ProposalGovernanceOrchestrator(
    ITradeProposalRepository proposals,
    IFreshProposalStateProvider freshStates,
    IProposalGovernanceContextProvider contexts,
    IGuardrailEvaluationService evaluations,
    IHumanProposalDecisionService decisions,
    ICapitalReservationService reservations,
    IProposalGovernanceIdentifierSource identifiers,
    IProposalGovernanceClock clock) : IProposalGovernanceOrchestrator
{
    public async Task<ProposalOrchestrationResult> ValidateAsync(
        TradeProposalId proposalId, CancellationToken cancellationToken)
    {
        var proposal = await proposals.GetAsync(proposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null) return Result(ProposalOrchestrationOutcome.NotFound, "proposal_orchestration.not_found");
        if (proposal.Status == ProposalStatus.AwaitingHumanApproval)
            return Result(ProposalOrchestrationOutcome.AlreadyCompleted, "proposal_orchestration.already_validated", proposal);
        if (proposal.Status is ProposalStatus.Rejected or ProposalStatus.Expired or ProposalStatus.Cancelled)
            return Terminal(proposal);
        if (clock.UtcNow >= proposal.ValidUntil) return await ExpireAsync(proposalId, cancellationToken).ConfigureAwait(false);

        return await EvaluateAsync(proposal, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProposalOrchestrationResult> DecideAndReserveAsync(
        HumanProposalDecisionCommand command, TimeSpan reservationLifetime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reservationLifetime, TimeSpan.Zero);

        var decision = await decisions.DecideAsync(command, cancellationToken).ConfigureAwait(false);
        if (decision.Outcome is HumanProposalDecisionOutcome.Unauthorized)
            return Result(ProposalOrchestrationOutcome.Unauthorized, decision.Code);
        if (decision.Outcome is HumanProposalDecisionOutcome.StaleReview)
            return Result(ProposalOrchestrationOutcome.StaleReview, decision.Code);
        if (decision.Outcome is HumanProposalDecisionOutcome.Expired)
            return Result(ProposalOrchestrationOutcome.Expired, decision.Code);
        if (decision.Outcome is HumanProposalDecisionOutcome.NotFound)
            return Result(ProposalOrchestrationOutcome.NotFound, decision.Code);
        if (decision.Outcome is HumanProposalDecisionOutcome.Conflict)
            return Result(ProposalOrchestrationOutcome.Contention, decision.Code);

        var proposal = await proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null) return Result(ProposalOrchestrationOutcome.NotFound, "proposal_orchestration.not_found");
        if (command.Decision == ApprovalDecision.Rejected)
        {
            await reservations.ReleaseAsync(proposal.Id, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            return new(ProposalOrchestrationOutcome.Rejected, "proposal_orchestration.rejected", proposal,
                Approval: decision.Approval);
        }

        FreshProposalState fresh;
        ProposalGovernanceEvaluationContext context;
        try
        {
            fresh = await freshStates.AcquireAsync(proposal, cancellationToken).ConfigureAwait(false);
            context = await contexts.GetAsync(proposal, fresh, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return Result(ProposalOrchestrationOutcome.Failed,
                "proposal_orchestration.state_unavailable", proposal);
        }
        var persisted = await evaluations.EvaluateAndPersistAsync(proposal.Id, context.Policies,
            context.PolicyDefinitions, context.State, fresh.Reference, cancellationToken).ConfigureAwait(false);
        var revalidation = MapEvaluation(persisted, proposal);
        if (revalidation.Outcome != ProposalOrchestrationOutcome.AwaitingHumanApproval)
            return revalidation with { Approval = decision.Approval };

        proposal = revalidation.Proposal!;
        if (proposal.Status != ProposalStatus.Approved)
            return Result(ProposalOrchestrationOutcome.Failed, "proposal_orchestration.approval_lost", proposal);
        var amount = ReservationAmount(proposal, fresh.AvailableCapital);
        if (amount is null)
            return Result(ProposalOrchestrationOutcome.Rejected, "proposal_orchestration.amount_unavailable", proposal);
        var expiresAt = Min(proposal.ValidUntil, clock.UtcNow.Add(reservationLifetime));
        var reservation = await reservations.ReserveAsync(new(identifiers.NewReservationId(), proposal.Id,
            proposal.PortfolioId, proposal.ContentVersion, fresh.Reference, amount,
            fresh.AvailableCapital, clock.UtcNow, expiresAt), cancellationToken).ConfigureAwait(false);
        return reservation.Outcome switch
        {
            CapitalReservationOutcome.Reserved => new(ProposalOrchestrationOutcome.Reserved, reservation.Code,
                proposal, revalidation.Evaluation, decision.Approval, reservation.Reservation),
            CapitalReservationOutcome.AlreadyReserved => new(ProposalOrchestrationOutcome.AlreadyCompleted,
                reservation.Code, proposal, revalidation.Evaluation, decision.Approval, reservation.Reservation),
            CapitalReservationOutcome.ConcurrencyConflict => Result(ProposalOrchestrationOutcome.Contention,
                reservation.Code, proposal),
            _ => Result(ProposalOrchestrationOutcome.Rejected, reservation.Code, proposal),
        };
    }

    public async Task<ProposalOrchestrationResult> ExpireAsync(
        TradeProposalId proposalId, CancellationToken cancellationToken)
    {
        var proposal = await proposals.GetAsync(proposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null) return Result(ProposalOrchestrationOutcome.NotFound, "proposal_orchestration.not_found");
        if (proposal.Status == ProposalStatus.Expired)
            return Result(ProposalOrchestrationOutcome.AlreadyCompleted, ProposalGovernanceCodes.Expired, proposal);
        if (clock.UtcNow < proposal.ValidUntil)
            return Result(ProposalOrchestrationOutcome.Failed, "proposal_orchestration.not_due", proposal);
        var expected = proposal.Version;
        try { proposal.Expire(clock.UtcNow); }
        catch (InvalidOperationException) { return Terminal(proposal); }
        var write = await proposals.SaveAsync(proposal, expected, cancellationToken).ConfigureAwait(false);
        if (write is not PersistenceWriteResult.Succeeded)
            return Result(ProposalOrchestrationOutcome.Contention, ProposalGovernanceCodes.ConcurrencyConflict, proposal);
        await reservations.ExpireAsync(proposal.PortfolioId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        return Result(ProposalOrchestrationOutcome.Expired, ProposalGovernanceCodes.Expired, proposal);
    }

    private async Task<ProposalOrchestrationResult> EvaluateAsync(
        TradeProposal proposal, CancellationToken cancellationToken)
    {
        FreshProposalState fresh;
        ProposalGovernanceEvaluationContext context;
        try
        {
            fresh = await freshStates.AcquireAsync(proposal, cancellationToken).ConfigureAwait(false);
            context = await contexts.GetAsync(proposal, fresh, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result(ProposalOrchestrationOutcome.Failed, "proposal_orchestration.state_unavailable", proposal); }

        var evaluated = await evaluations.EvaluateAndPersistAsync(proposal.Id, context.Policies,
            context.PolicyDefinitions, context.State, fresh.Reference, cancellationToken).ConfigureAwait(false);
        return MapEvaluation(evaluated, proposal);
    }

    private static ProposalOrchestrationResult MapEvaluation(
        GuardrailEvaluationPersistenceResult evaluated, TradeProposal proposal)
    {
        if (evaluated.Outcome == GuardrailEvaluationPersistenceOutcome.ConcurrencyConflict)
            return Result(ProposalOrchestrationOutcome.Contention, evaluated.Code, evaluated.Proposal);
        if (evaluated.Outcome == GuardrailEvaluationPersistenceOutcome.NotFound)
            return Result(ProposalOrchestrationOutcome.NotFound, evaluated.Code);
        var current = evaluated.Proposal ?? proposal;
        if (current.ExecutionMode == Trading.Core.Bots.ExecutionMode.ResearchOnly)
            return new(ProposalOrchestrationOutcome.ResearchOnly, ProposalGovernanceCodes.ResearchOnly,
                current, evaluated.Evaluation);
        if (evaluated.Evaluation?.Outcome == GuardrailOutcome.Failed || current.Status == ProposalStatus.Rejected)
            return new(ProposalOrchestrationOutcome.Rejected, evaluated.Code, current, evaluated.Evaluation);
        return new(ProposalOrchestrationOutcome.AwaitingHumanApproval, evaluated.Code, current, evaluated.Evaluation);
    }

    private static Money? ReservationAmount(TradeProposal proposal, Money available) => proposal.RequestedAction switch
    {
        DirectTradeAction { Side: TradeSide.Buy, LimitPrice: not null } direct =>
            new Money(direct.Quantity.Amount * direct.LimitPrice.Amount, direct.LimitPrice.Currency),
        TargetAllocationAction target => new Money(available.Amount * target.TargetPercentage.Value / 100m,
            available.Currency),
        _ => null,
    };

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
    private static ProposalOrchestrationResult Terminal(TradeProposal proposal) => proposal.Status switch
    {
        ProposalStatus.Expired => Result(ProposalOrchestrationOutcome.Expired, ProposalGovernanceCodes.Expired, proposal),
        ProposalStatus.Cancelled => Result(ProposalOrchestrationOutcome.Cancelled, ProposalGovernanceCodes.Cancelled, proposal),
        ProposalStatus.Rejected => Result(ProposalOrchestrationOutcome.Rejected, ProposalGovernanceCodes.PolicyRejected, proposal),
        _ => Result(ProposalOrchestrationOutcome.AlreadyCompleted, "proposal_orchestration.already_completed", proposal),
    };
    private static ProposalOrchestrationResult Result(ProposalOrchestrationOutcome outcome, string code,
        TradeProposal? proposal = null) => new(outcome, code, proposal);
}

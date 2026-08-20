using Trading.Core.Persistence;
using Trading.Core.Proposals;

namespace Trading.Engine.Proposals;

public sealed class HumanProposalDecisionService(
    ITradeProposalRepository proposals,
    IProposalDecisionAuthorizer authorizer,
    IProposalGovernanceIdentifierSource identifiers,
    IProposalGovernanceClock clock) : IHumanProposalDecisionService
{
    public async Task<HumanProposalDecisionResult> DecideAsync(
        HumanProposalDecisionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Actor.Type != ApprovalActorType.User)
            return Result(HumanProposalDecisionOutcome.Unauthorized, ProposalGovernanceCodes.UnauthorizedActor);

        var authorization = await authorizer.AuthorizeAsync(new ProposalDecisionAuthorizationRequest(
            command.Actor, command.ActorRoles, command.ProposalId, command.ReviewedContentVersion,
            command.ReviewedState, command.Decision), cancellationToken).ConfigureAwait(false);
        if (!authorization.Authorized)
            return Result(HumanProposalDecisionOutcome.Unauthorized, authorization.Code);

        var proposal = await proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null)
            return Result(HumanProposalDecisionOutcome.NotFound, "proposal_decision.not_found");
        if (proposal.ExecutionMode == Trading.Core.Bots.ExecutionMode.ResearchOnly)
            return Result(HumanProposalDecisionOutcome.Unauthorized, ProposalGovernanceCodes.ResearchOnly);

        var identical = proposal.ApprovalHistory.SingleOrDefault(x =>
            x.Actor == command.Actor && x.Decision == command.Decision && x.Reason == Normalize(command.Reason) &&
            x.ReviewedContentVersion == command.ReviewedContentVersion && x.ReviewedState == command.ReviewedState);
        if (identical is not null)
            return new(HumanProposalDecisionOutcome.AlreadyApplied, "proposal_decision.already_applied", identical);

        if (proposal.ApprovalHistory.Count != 0 || proposal.Status is ProposalStatus.Approved or ProposalStatus.Rejected)
            return Result(HumanProposalDecisionOutcome.Conflict, "proposal_decision.conflicting_terminal_decision");
        if (clock.UtcNow >= proposal.ValidUntil)
            return Result(HumanProposalDecisionOutcome.Expired, ProposalGovernanceCodes.Expired);
        if (proposal.Status != ProposalStatus.AwaitingHumanApproval)
            return Result(HumanProposalDecisionOutcome.Conflict, "proposal_decision.not_awaiting_approval");

        var evaluation = proposal.GuardrailEvaluations.Count == 0
            ? null
            : proposal.GuardrailEvaluations[^1];
        if (proposal.ContentVersion != command.ReviewedContentVersion ||
            proposal.ConfigurationVersionId != command.ReviewedConfigurationVersionId ||
            evaluation is null || evaluation.Id != command.ReviewedEvaluationId ||
            evaluation.ContentHash != command.ReviewedEvaluationHash || evaluation.Outcome != GuardrailOutcome.Passed ||
            evaluation.FreshState != command.ReviewedState)
            return Result(HumanProposalDecisionOutcome.StaleReview, "proposal_decision.stale_review");

        var expectedVersion = proposal.Version;
        ProposalApproval decision;
        try
        {
            decision = command.Decision == ApprovalDecision.Approved
                ? proposal.Approve(identifiers.NewApprovalId(), command.Actor, command.Reason, clock.UtcNow,
                    command.ReviewedContentVersion, command.ReviewedState)
                : proposal.Reject(identifiers.NewApprovalId(), command.Actor,
                    command.Reason ?? "Rejected by authorized reviewer.", clock.UtcNow,
                    command.ReviewedContentVersion, command.ReviewedState);
        }
        catch (InvalidOperationException)
        {
            return Result(HumanProposalDecisionOutcome.Conflict, ProposalGovernanceCodes.ConcurrencyConflict);
        }

        var write = await proposals.SaveAsync(proposal, expectedVersion, cancellationToken).ConfigureAwait(false);
        return write is PersistenceWriteResult.Succeeded
            ? new(HumanProposalDecisionOutcome.Applied, "proposal_decision.applied", decision)
            : Result(HumanProposalDecisionOutcome.Conflict, ProposalGovernanceCodes.ConcurrencyConflict);
    }

    private static HumanProposalDecisionResult Result(HumanProposalDecisionOutcome outcome, string code) =>
        new(outcome, code, null);

    private static string? Normalize(string? value) => value?.Trim();
}

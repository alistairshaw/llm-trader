using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;

namespace Trading.Engine.Proposals;

public sealed class GuardrailEvaluationService(
    ITradeProposalRepository proposals,
    IGuardrailPolicyEvaluator evaluator,
    IProposalGovernanceIdentifierSource identifiers,
    IProposalGovernanceClock clock) : IGuardrailEvaluationService
{
    public async Task<GuardrailEvaluationPersistenceResult> EvaluateAndPersistAsync(
        TradeProposalId proposalId, GuardrailPolicySet policies,
        HierarchicalGuardrailPolicySet policyDefinitions, GuardrailState state,
        FreshStateReference freshState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposalId);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(policyDefinitions);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(freshState);

        var proposal = await proposals.GetAsync(proposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null)
            return new GuardrailEvaluationPersistenceResult(GuardrailEvaluationPersistenceOutcome.NotFound,
                "guardrail_evaluation.not_found", null, null);

        var inputHash = HashInput(proposal, policies, state, freshState);
        var prior = proposal.GuardrailEvaluations.LastOrDefault(x => x.ContentHash == inputHash);
        if (prior is not null)
            return new GuardrailEvaluationPersistenceResult(GuardrailEvaluationPersistenceOutcome.AlreadyEvaluated,
                "guardrail_evaluation.already_evaluated", prior, proposal);

        var expectedVersion = proposal.Version;
        if (proposal.Status == ProposalStatus.Recorded) proposal.StartValidation(clock.UtcNow);
        else if (proposal.Status == ProposalStatus.AwaitingHumanApproval) proposal.StartRevalidation(clock.UtcNow);
        else
            return new GuardrailEvaluationPersistenceResult(GuardrailEvaluationPersistenceOutcome.ConcurrencyConflict,
                ProposalGovernanceCodes.ConcurrencyConflict, null, proposal);

        var decision = await evaluator.EvaluateAsync(new GuardrailEvaluationRequest(proposal, policies,
            freshState, policyDefinitions, state), cancellationToken).ConfigureAwait(false);
        var dispositionCode = proposal.ExecutionMode == Trading.Core.Bots.ExecutionMode.ResearchOnly
            ? ProposalGovernanceCodes.ResearchOnly
            : decision.Code;
        var evaluation = proposal.RecordEvaluation(identifiers.NewEvaluationId(), decision.EvaluatedPolicies,
            decision.Outcome, decision.RuleResults, clock.UtcNow, decision.FreshState, inputHash, dispositionCode);
        proposal.CompleteValidation(decision.Outcome, clock.UtcNow);
        var write = await proposals.SaveAsync(proposal, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (write is PersistenceWriteResult.Succeeded)
            return new GuardrailEvaluationPersistenceResult(GuardrailEvaluationPersistenceOutcome.Persisted,
                dispositionCode, evaluation, proposal);

        return new GuardrailEvaluationPersistenceResult(GuardrailEvaluationPersistenceOutcome.ConcurrencyConflict,
            ProposalGovernanceCodes.ConcurrencyConflict, evaluation, proposal);
    }

    private static string HashInput(TradeProposal proposal, GuardrailPolicySet policies, GuardrailState state,
        FreshStateReference freshState)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ProposalId = proposal.Id.ToString(),
            ProposalVersion = proposal.ContentVersion.Version,
            proposal.ContentVersion.ContentHash,
            ConfigurationVersionId = proposal.ConfigurationVersionId.ToString(),
            SnapshotId = freshState.SnapshotId.ToString(),
            freshState.ObservedAt,
            StateHash = freshState.ContentHash,
            Policies = policies.InEvaluationOrder.Select(x => new { Level = x.Level.ToString(), x.PolicyId, x.Version }),
            state.IdentityAuthorized,
            state.WithinMandate,
            EvaluatedAt = state.EvaluatedAt
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

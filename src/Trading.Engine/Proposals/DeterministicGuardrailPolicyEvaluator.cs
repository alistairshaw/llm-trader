using Trading.Core.Proposals;

namespace Trading.Engine.Proposals;

public sealed class DeterministicGuardrailPolicyEvaluator : IGuardrailPolicyEvaluator
{
    public Task<GuardrailEvaluationDecision> EvaluateAsync(
        GuardrailEvaluationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var supplied = request.Policies.InEvaluationOrder;
        var definitions = request.PolicyDefinitions.InEvaluationOrder.Select(x => x.Reference);
        if (!supplied.SequenceEqual(definitions))
            throw new ArgumentException("Policy references must exactly match the supplied immutable definitions.", nameof(request));
        var result = Trading.Core.Policies.HierarchicalGuardrailEvaluator.Evaluate(
            request.Proposal, request.PolicyDefinitions, request.State);
        return Task.FromResult(new GuardrailEvaluationDecision(result.Outcome, result.Code,
            result.RuleResults, result.EvaluatedPolicies, request.FreshState));
    }
}

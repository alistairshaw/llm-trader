using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.Engine.Tests;

[Category("GuardrailPipeline")]
public sealed class GuardrailPipelineTests
{
    [Test]
    public async Task AdapterReturnsPureDeterministicDecisionWithPinnedReferences()
    {
        var definitions = Policies();
        var references = definitions.InEvaluationOrder.Select(x => x.Reference).ToArray();
        var policySet = new GuardrailPolicySet(references[0], references[1], references[2], references[3]);
        var fresh = new FreshStateReference(PortfolioDecisionSnapshotId.New(),
            UtcNow, "fresh-hash");
        var request = new GuardrailEvaluationRequest(
            Proposal(), policySet, fresh, definitions, State());
        var evaluator = new DeterministicGuardrailPolicyEvaluator();

        var first = await evaluator.EvaluateAsync(request, CancellationToken.None);
        var second = await evaluator.EvaluateAsync(request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(second.Code, Is.EqualTo(first.Code));
            Assert.That(second.RuleResults, Is.EqualTo(first.RuleResults));
            Assert.That(first.Outcome, Is.EqualTo(GuardrailOutcome.Passed));
            Assert.That(first.FreshState, Is.SameAs(fresh));
            Assert.That(first.EvaluatedPolicies, Is.EqualTo(references));
        });
    }

    [Test]
    public void AdapterRejectsPolicyReferenceDefinitionMismatch()
    {
        var definitions = Policies();
        var references = definitions.InEvaluationOrder.Select(x => x.Reference).ToArray();
        var mismatched = new GuardrailPolicySet(new(GuardrailPolicyLevel.Platform, "other", "v1"),
            references[1], references[2], references[3]);
        var fresh = new FreshStateReference(PortfolioDecisionSnapshotId.New(),
            UtcNow, "fresh-hash");
        var request = new GuardrailEvaluationRequest(
            Proposal(), mismatched, fresh, definitions, State());

        Assert.That(async () => await new DeterministicGuardrailPolicyEvaluator().EvaluateAsync(request, CancellationToken.None),
            Throws.ArgumentException);
    }

    private static readonly DateTimeOffset UtcNow = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
    private static Money Usd(decimal amount) => new(amount, Currency.USD);
    private static TradeProposal Proposal() => new(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(),
        PortfolioId.New(), TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(),
        new DirectTradeAction(TradeSide.Buy, new Quantity(1, "share"), ProposedOrderType.Market, null, ProposedTimeInForce.Day),
        "rationale", new ProposalContentVersion(1, "hash"), null, [], UtcNow.AddMinutes(-1), UtcNow.AddHours(1));
    private static GuardrailState State() => new(UtcNow, true, true, Usd(100), Usd(1000),
        new Percentage(20), Usd(110), UtcNow.AddMinutes(-5), Usd(10000), true);
    private static HierarchicalGuardrailPolicySet Policies()
    {
        GuardrailPolicy Policy(GuardrailPolicyLevel level) => new(new(level, $"{level}-policy", "v1"), true,
            null, Usd(1000), new Percentage(20), Usd(10), TimeSpan.FromMinutes(5), Usd(10000), true);
        return new(Policy(GuardrailPolicyLevel.Platform), Policy(GuardrailPolicyLevel.Account),
            Policy(GuardrailPolicyLevel.Portfolio), Policy(GuardrailPolicyLevel.TradingBot));
    }
}

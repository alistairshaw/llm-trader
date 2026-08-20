using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;

namespace Trading.Core.Tests.Proposals;

[TestFixture, Category("ResearchOnlyProposal")]
public sealed class ResearchOnlyProposalTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [TestCase(ProposalType.DirectTrade)]
    [TestCase(ProposalType.TargetAllocation)]
    public void PassingEvaluationLeavesResearchOnlyProposalPermanentlyNonExecutable(ProposalType type)
    {
        var proposal = Proposal(type, ExecutionMode.ResearchOnly);
        proposal.StartValidation(Now.AddMinutes(1));
        proposal.RecordEvaluation(GuardrailEvaluationId.New(), "Hierarchical", "v1", GuardrailOutcome.Passed,
            [new GuardrailRuleResult("all", GuardrailOutcome.Passed, "passed")], Now.AddMinutes(1),
            proposal.PortfolioSnapshotId);
        proposal.CompleteValidation(GuardrailOutcome.Passed, Now.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(proposal.ExecutionMode, Is.EqualTo(ExecutionMode.ResearchOnly));
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Rejected));
            Assert.That(proposal.GuardrailEvaluations, Has.Count.EqualTo(1));
            Assert.That(() => proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User, "operator", null,
                Now.AddMinutes(2), proposal.Version, proposal.PortfolioSnapshotId), Throws.InvalidOperationException);
            Assert.That(() => proposal.ConvertToOrder(Now.AddMinutes(2)),
                Throws.InvalidOperationException.With.Message.EqualTo(ProposalGovernanceCodes.ResearchOnly));
        });
    }

    [Test]
    public void ExecutionModeIsPinnedWhenConfigurationChangesElsewhere()
    {
        var proposal = Proposal(ProposalType.DirectTrade, ExecutionMode.ResearchOnly);
        var laterConfigurationMode = ExecutionMode.PaperTrading;

        Assert.Multiple(() =>
        {
            Assert.That(laterConfigurationMode, Is.EqualTo(ExecutionMode.PaperTrading));
            Assert.That(proposal.ExecutionMode, Is.EqualTo(ExecutionMode.ResearchOnly));
        });
    }

    private static TradeProposal Proposal(ProposalType type, ExecutionMode mode)
    {
        RequestedAction action = type == ProposalType.DirectTrade
            ? new DirectTradeAction(TradeSide.Buy, new Quantity(1, "share"), ProposedOrderType.Market, null,
                ProposedTimeInForce.Day)
            : new TargetAllocationAction(new Percentage(10));
        return new(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(),
            TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(), action,
            "research proposal", new ProposalContentVersion(1, new string('a', 64)), null, [], Now,
            Now.AddHours(1), mode);
    }
}

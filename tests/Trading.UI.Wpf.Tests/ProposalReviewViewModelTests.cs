using System.Collections.Immutable;
using System.Xml.Linq;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;
using Trading.Engine.Operators;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Tests;

[Category("ProposalReview")]
public sealed class ProposalReviewViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);
    private static readonly OperatorPrincipal Principal = new("reviewer-a", [OperatorAuthority.ReadOperations, OperatorAuthority.DecideProposals]);

    [Test]
    public async Task QueueFiltersPagesAndExactDetailPreservesEveryReviewedIdentity()
    {
        var summary = Summary(); var gateway = new Gateway { Summaries = Page(summary), Details = Page(Detail(summary)) };
        await using var model = new ProposalReviewViewModel(gateway, gateway, Principal, () => Now)
        { StatusFilter = " AwaitingHumanApproval " };
        await model.RefreshAsync(); model.SelectedProposal = model.Items.Single(); await model.LoadProposalAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gateway.Queries[0].Filter.Status, Is.EqualTo("AwaitingHumanApproval"));
            Assert.That(gateway.Queries[1].Resource, Is.EqualTo(new OperatorResource(OperatorResourceKind.TradeProposal, summary.Id.ToString())));
            Assert.That(model.Detail!.ContentHash, Is.EqualTo(new string('a', 64)));
            Assert.That(model.Detail.ReviewedIdentity, Is.EqualTo(new OperatorProposalIdentity("config-v7", "snapshot-v11", new string('b', 64))));
            Assert.That(model.Evidence.Single().ContentHash, Is.EqualTo(new string('c', 64)));
            Assert.That(model.Guardrails.Single().PolicyVersion, Is.EqualTo("risk-v5"));
            Assert.That(model.DecisionEligibility, Does.StartWith("Awaiting"));
        }
    }

    [Test]
    public async Task ConfirmedApprovalUsesReviewedVersionThenRefreshesAuthoritativeDecisionAndReservation()
    {
        var initial = Summary(); var decided = initial with { Status = ProposalStatus.Approved, Version = 8 };
        var gateway = new Gateway { Summaries = Page(initial), Details = Page(Detail(initial)) };
        gateway.AfterDecision = () => { gateway.Summaries = Page(decided); gateway.Details = Page(Detail(decided, true)); };
        await using var model = new ProposalReviewViewModel(gateway, gateway, Principal, () => Now);
        await model.RefreshAsync(); model.SelectedProposal = initial; await model.LoadProposalAsync();
        model.ConfirmDecision = true; model.DecisionReason = " Approved after evidence review ";
        await model.DecideAsync(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gateway.Commands.Single(), Is.EqualTo((true, initial.Id, 7L, "Approved after evidence review")));
            Assert.That(model.Detail!.Summary.Status, Is.EqualTo(ProposalStatus.Approved));
            Assert.That(model.DecisionHistory.Single().ActorId, Is.EqualTo("reviewer-a"));
            Assert.That(model.Detail.Reservation!.Status, Is.EqualTo("Active"));
            Assert.That(model.ConfirmDecision, Is.False);
        }
    }

    [Test]
    public async Task RejectionRequiresReasonAndDeniedStaleExpiredChangedAndTerminalOutcomesRemainStable()
    {
        var summary = Summary(); var gateway = new Gateway { Summaries = Page(summary), Details = Page(Detail(summary)) };
        await using var model = new ProposalReviewViewModel(gateway, gateway, Principal, () => Now);
        await model.RefreshAsync(); model.SelectedProposal = summary; await model.LoadProposalAsync(); model.ConfirmDecision = true;
        await model.DecideAsync(false);
        Assert.That(model.ErrorCode, Is.EqualTo("proposal_review.rejection_reason_required"));

        foreach (var code in new[] { "operator.unavailable", "proposal.expired", "proposal.stale", "proposal.content_changed", "operator.conflict" })
        {
            gateway.CommandResult = new(OperatorResultStatus.Conflict, code); model.DecisionReason = "Reject";
            await model.DecideAsync(false); Assert.That(model.ErrorCode, Is.EqualTo(code));
        }
        gateway.Details = Page(Detail(summary with { Status = ProposalStatus.Rejected }));
        model.SelectedProposal = summary with { Status = ProposalStatus.Rejected }; await model.LoadProposalAsync();
        model.ConfirmDecision = true; model.DecisionReason = "Again"; await model.DecideAsync(false);
        Assert.That(model.ErrorCode, Is.EqualTo("proposal_review.ineligible"));
    }

    [Test]
    public void ViewExposesStableAccessibleQueueEvidenceGuardrailsConfirmationAndDecisionControls()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "ProposalReviewView.xaml"));
        var ids = document.Descendants().SelectMany(x => x.Attributes())
            .Where(x => x.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal)).Select(x => x.Value).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain("Proposals.Workspace").And.Contain("Proposals.Queue")
                .And.Contain("Proposals.Evidence").And.Contain("Proposals.Guardrails")
                .And.Contain("Proposals.Confirm").And.Contain("Proposals.Approve").And.Contain("Proposals.Reject"));
            Assert.That(document.Descendants().Count(x => x.Name.LocalName == "Label" && x.Attribute("Target") is not null), Is.EqualTo(2));
            Assert.That(document.Descendants().Any(x => x.Attributes().Any(a => a.Name.LocalName.EndsWith(".HeadingLevel", StringComparison.Ordinal))), Is.True);
        }
    }

    private static ProposalSummary Summary() => new(TradeProposalId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA10"),
        TradingBotId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA11"), PortfolioId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA12"),
        ProposalStatus.AwaitingHumanApproval, Now.AddHours(1), 7);
    private static ProposalDetail Detail(ProposalSummary summary, bool decided = false) => new(summary, "Exact rationale",
        new string('a', 64), new("config-v7", "snapshot-v11", new string('b', 64)),
        [new("Report", "report-42", 3, new string('c', 64), "BotPrivate")],
        [new("PositionLimit", "Passed", "guardrail.passed", "risk-v5")],
        decided ? [new("Approved", "reviewer-a", Now, "Approved after evidence review", 8)] : [],
        decided ? new("reservation-9", "Active", "2500.00 USD", Now.AddMinutes(15)) : null, []);
    private static OperatorPage<T> Page<T>(params T[] values) => new(values, 0, null);

    private sealed class Gateway : IOperatorQueries, IProposalOperatorService
    {
        public OperatorPage<ProposalSummary> Summaries { get; set; } = Page<ProposalSummary>();
        public OperatorPage<ProposalDetail> Details { get; set; } = Page<ProposalDetail>();
        public OperatorCommandResult CommandResult { get; set; } = new(OperatorResultStatus.Succeeded, "proposal.decided");
        public Action? AfterDecision { get; set; }
        public List<(OperatorResource Resource, OperatorFilter Filter)> Queries { get; } = [];
        public List<(bool Approve, TradeProposalId Id, long Version, string? Reason)> Commands { get; } = [];
        public Task<OperatorQueryResult<OperatorOverview>> GetOverviewAsync(OperatorPrincipal p, CancellationToken t) =>
            Task.FromResult(new OperatorQueryResult<OperatorOverview>(OperatorResultStatus.Unavailable, null));
        public Task<OperatorQueryResult<OperatorPage<T>>> GetPageAsync<T>(OperatorPrincipal p, OperatorPageKind page,
            OperatorResource resource, OperatorFilter filter, OperatorPageRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Queries.Add((resource, filter)); object value = typeof(T) == typeof(ProposalDetail) ? Details : Summaries;
            return Task.FromResult(new OperatorQueryResult<OperatorPage<T>>(OperatorResultStatus.Succeeded, (OperatorPage<T>)value));
        }
        public Task<OperatorCommandResult> ApproveAsync(OperatorPrincipal p, TradeProposalId id, long version, string? reason, CancellationToken token) => Decide(true, id, version, reason, token);
        public Task<OperatorCommandResult> RejectAsync(OperatorPrincipal p, TradeProposalId id, long version, string reason, CancellationToken token) => Decide(false, id, version, reason, token);
        private Task<OperatorCommandResult> Decide(bool approve, TradeProposalId id, long version, string? reason, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Commands.Add((approve, id, version, reason)); if (CommandResult.Status == OperatorResultStatus.Succeeded) AfterDecision?.Invoke(); return Task.FromResult(CommandResult); }
    }
}

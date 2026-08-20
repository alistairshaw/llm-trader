using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Research;

namespace Trading.Core.Tests.Research;

[Category("Research")]
public sealed class ResearchLifecycleMatrixTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [TestCase(ResearchRunAttemptStatus.Completed)]
    [TestCase(ResearchRunAttemptStatus.Failed)]
    [TestCase(ResearchRunAttemptStatus.TimedOut)]
    [TestCase(ResearchRunAttemptStatus.BudgetExceeded)]
    [TestCase(ResearchRunAttemptStatus.Cancelled)]
    [TestCase(ResearchRunAttemptStatus.Recovered)]
    public void EveryTerminalAttemptOutcomeIsReachableFromRunning(ResearchRunAttemptStatus terminal)
    {
        var attempt = NewAttempt(); attempt.Start(Now);
        attempt.Terminate(terminal, ZeroUsage(), $"result.{terminal}", Now);
        Assert.That(attempt.Status, Is.EqualTo(terminal));
    }

    [TestCase(ResearchRunAttemptStatus.Created)]
    [TestCase(ResearchRunAttemptStatus.Running)]
    [TestCase(ResearchRunAttemptStatus.WaitingForTool)]
    public void NonterminalAttemptOutcomesCannotTerminate(ResearchRunAttemptStatus target)
    {
        var attempt = NewAttempt(); attempt.Start(Now);
        Assert.That(() => attempt.Terminate(target, ZeroUsage(), "invalid", Now), Throws.ArgumentException);
    }

    [TestCase(ResearchTerminalOutcome.Failed, ResearchRequestStatus.Failed)]
    [TestCase(ResearchTerminalOutcome.TimedOut, ResearchRequestStatus.TimedOut)]
    [TestCase(ResearchTerminalOutcome.BudgetExceeded, ResearchRequestStatus.BudgetExceeded)]
    [TestCase(ResearchTerminalOutcome.Cancelled, ResearchRequestStatus.Cancelled)]
    public void EveryNonpublicationRequestOutcomeIsReachable(ResearchTerminalOutcome terminal, ResearchRequestStatus expected)
    {
        var request = NewRequest(); request.BeginValidation(); request.Queue(); request.Start(Now); request.Terminate(terminal, Now);
        Assert.That(request.Status, Is.EqualTo(expected));
    }

    [TestCase(ResearchTerminalOutcome.Failed)]
    [TestCase(ResearchTerminalOutcome.TimedOut)]
    [TestCase(ResearchTerminalOutcome.BudgetExceeded)]
    [TestCase(ResearchTerminalOutcome.Cancelled)]
    public void RequestTerminalOutcomesCannotTransitionAgain(ResearchTerminalOutcome terminal)
    {
        var request = NewRequest(); request.BeginValidation(); request.Queue(); request.Start(Now); request.Terminate(terminal, Now);
        Assert.Multiple(() =>
        {
            Assert.That(() => request.Terminate(terminal, Now), Throws.InvalidOperationException);
            Assert.That(() => request.BeginValidation(), Throws.InvalidOperationException);
            Assert.That(() => request.Start(Now), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void RequestToolWaitHasOnlyItsExplicitRoundTrip()
    {
        var request = NewRequest(); request.BeginValidation(); request.Queue(); request.Start(Now); request.WaitForTool();
        Assert.That(() => request.WaitForTool(), Throws.InvalidOperationException);
        request.ResumeFromTool();
        Assert.That(request.Status, Is.EqualTo(ResearchRequestStatus.Running));
    }

    [TestCase(ResearchReportStatus.Expired)]
    [TestCase(ResearchReportStatus.Superseded)]
    [TestCase(ResearchReportStatus.Corrected)]
    [TestCase(ResearchReportStatus.Retracted)]
    public void EveryReportDispositionIsOneWay(ResearchReportStatus disposition)
    {
        var report = NewReport();
        typeof(ResearchReport).GetMethod($"Mark{disposition}")!.Invoke(report, null);
        Assert.Multiple(() => { Assert.That(report.Status, Is.EqualTo(disposition)); Assert.That(() => report.MarkExpired(), Throws.InvalidOperationException); });
    }

    [TestCase(ResearchNotificationStatus.Delivered)]
    [TestCase(ResearchNotificationStatus.Failed)]
    public void EverySubscriptionOutcomeIsOneWay(ResearchNotificationStatus terminal)
    {
        var bot = TradingBotId.New(); var request = NewRequest(bot); var subscription = request.Subscribe(ResearchSubscriptionId.New(), bot, Now);
        if (terminal == ResearchNotificationStatus.Delivered) subscription.MarkDelivered(); else subscription.MarkFailed();
        Assert.Multiple(() => { Assert.That(subscription.NotificationStatus, Is.EqualTo(terminal)); Assert.That(() => subscription.MarkDelivered(), Throws.InvalidOperationException); });
    }

    private static ResearchRequest NewRequest(TradingBotId? bot = null) { bot ??= TradingBotId.New(); return new(ResearchRequestId.New(), bot, "US:ABC", "Does cash flow support a five-year thesis?", Now.AddMinutes(-1), ResearchVisibility.Shared, new Trading.Core.Policies.DataFreshness(Now.AddDays(-1), Now.AddMinutes(-1), TimeSpan.FromDays(7)), "key", Now, [bot]); }
    private static ResearchRunAttempt NewAttempt() => new(ResearchRunAttemptId.New(), ResearchRequestId.New(), new ResearchVersionPins("p", "m", "r", "pv", "tv", "sv"), new ResearchBudget(TimeSpan.FromSeconds(1), 1, Money.Zero(Currency.USD), 1, 1, 1, 1), Now);
    private static ResearchUsage ZeroUsage() => new(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0, 0);
    private static ResearchReport NewReport() => new(ResearchReportId.New(), "series", 1, ResearchRequestId.New(), "US:ABC", "Question?", ResearchVisibility.Shared, Now.AddMinutes(-1), Now, Now.AddDays(1), null, "content", "hash", new ReportProvenance([new SourceCitation("provider", "source", Now.AddHours(-1), Now, "hash")]), new GeneratorMetadata(new Trading.Core.Policies.ModelConfiguration("p", "m", 0, 1), "p1", "t1", "s1"));
}

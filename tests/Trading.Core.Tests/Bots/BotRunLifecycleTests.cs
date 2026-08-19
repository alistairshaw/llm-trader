using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Tests.Bots;

public sealed class BotRunLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly BotRunStatus[] TerminalStatuses =
        [BotRunStatus.Completed, BotRunStatus.TimedOut, BotRunStatus.BudgetExceeded, BotRunStatus.Cancelled, BotRunStatus.Faulted];

    public static IEnumerable<TestCaseData> EveryTransition()
    {
        var allowed = new HashSet<(BotRunStatus, BotRunStatus)>
        {
            (BotRunStatus.Pending, BotRunStatus.AcquiringLease),
            (BotRunStatus.AcquiringLease, BotRunStatus.PreparingSnapshot),
            (BotRunStatus.PreparingSnapshot, BotRunStatus.Reasoning),
            (BotRunStatus.Reasoning, BotRunStatus.WaitingForTool),
            (BotRunStatus.WaitingForTool, BotRunStatus.Reasoning),
        };
        allowed.Add((BotRunStatus.Pending, BotRunStatus.Cancelled));
        allowed.Add((BotRunStatus.Pending, BotRunStatus.Faulted));
        foreach (var active in new[] { BotRunStatus.AcquiringLease, BotRunStatus.PreparingSnapshot })
            foreach (var terminal in new[] { BotRunStatus.TimedOut, BotRunStatus.Cancelled, BotRunStatus.Faulted })
                allowed.Add((active, terminal));
        foreach (var active in new[] { BotRunStatus.Reasoning, BotRunStatus.WaitingForTool })
            foreach (var terminal in TerminalStatuses) allowed.Add((active, terminal));
        foreach (var from in Enum.GetValues<BotRunStatus>())
            foreach (var to in Enum.GetValues<BotRunStatus>())
                if (from != to) yield return new TestCaseData(from, to, allowed.Contains((from, to)));
    }

    [TestCaseSource(nameof(EveryTransition))]
    public void EveryAllowedAndForbiddenTransitionIsExplicit(BotRunStatus from, BotRunStatus to, bool allowed)
    {
        var run = InState(from);
        void Act() => Transition(run, to);
        Assert.That(Act, allowed ? Throws.Nothing : Throws.InvalidOperationException);
    }

    [Test]
    public void PersistenceActiveStatusSetContainsExactlyFiveStates() =>
        Assert.That(BotRun.ActiveStatuses, Is.EquivalentTo(new[]
        {
            BotRunStatus.Pending, BotRunStatus.AcquiringLease, BotRunStatus.PreparingSnapshot,
            BotRunStatus.Reasoning, BotRunStatus.WaitingForTool,
        }));

    private static BotRun InState(BotRunStatus status)
    {
        var run = NewRun();
        if (status == BotRunStatus.Pending) return run;
        run.BeginLeaseAcquisition(Now);
        if (status == BotRunStatus.AcquiringLease) return run;
        run.LeaseAcquired("host", Now.AddMinutes(10));
        if (status == BotRunStatus.PreparingSnapshot) return run;
        run.BeginReasoning();
        if (status == BotRunStatus.Reasoning) return run;
        if (status == BotRunStatus.WaitingForTool) { run.WaitForTool(); return run; }
        Finish(run, status);
        return run;
    }

    private static void Transition(BotRun run, BotRunStatus target)
    {
        switch (target)
        {
            case BotRunStatus.AcquiringLease: run.BeginLeaseAcquisition(Now.AddMinutes(20)); break;
            case BotRunStatus.PreparingSnapshot: run.LeaseAcquired("host", Now.AddMinutes(30)); break;
            case BotRunStatus.Reasoning when run.Status == BotRunStatus.PreparingSnapshot: run.BeginReasoning(); break;
            case BotRunStatus.Reasoning: run.ResumeReasoning(); break;
            case BotRunStatus.WaitingForTool: run.WaitForTool(); break;
            default: Finish(run, target); break;
        }
    }

    private static void Finish(BotRun run, BotRunStatus target)
    {
        var usage = ZeroUsage();
        switch (target)
        {
            case BotRunStatus.Completed: run.Complete(new FinishResult(FinishStatus.Completed, "done"), usage, Now.AddHours(1)); break;
            case BotRunStatus.TimedOut: run.TimeOut(usage, Now.AddHours(1)); break;
            case BotRunStatus.BudgetExceeded: run.ExceedBudget(usage, Now.AddHours(1)); break;
            case BotRunStatus.Cancelled: run.Cancel(usage, Now.AddHours(1)); break;
            case BotRunStatus.Faulted: run.Fault(usage, Now.AddHours(1)); break;
            default: throw new InvalidOperationException("No transition operation exists for the requested target.");
        }
    }

    private static BotRun NewRun() => new(BotRunId.New(), TradingBotId.New(), TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), ZeroUsage());
    private static Usage ZeroUsage() => new(TimeSpan.Zero, 0, new Money(0m, Currency.USD), 0, 0, 0);
}

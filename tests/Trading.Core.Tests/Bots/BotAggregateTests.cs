using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Tests.Bots;

[Category("BotAggregates")]
public sealed class BotAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void ActivatingNewVersionSupersedesOldAndContentRemainsImmutable()
    {
        var bot = NewBot();
        var first = AddConfiguration(bot, ExecutionMode.ResearchOnly, Now);
        bot.ActivateConfiguration(first.Id, Now.AddMinutes(1));
        var second = AddConfiguration(bot, ExecutionMode.PaperTrading, Now.AddMinutes(2));
        bot.ActivateConfiguration(second.Id, Now.AddMinutes(3));

        Assert.Multiple(() =>
        {
            Assert.That(bot.ConfigurationVersions.Count(version => version.IsActive), Is.EqualTo(1));
            Assert.That(first.SupersededAt, Is.EqualTo(Now.AddMinutes(3)));
            Assert.That(second.VersionNumber, Is.EqualTo(2));
            Assert.That(typeof(TradingBotConfigurationVersion).GetProperties().Where(p => p.Name is not nameof(TradingBotConfigurationVersion.ActivatedAt) and not nameof(TradingBotConfigurationVersion.SupersededAt)).All(p => p.SetMethod is null), Is.True);
            Assert.That(() => bot.ActivateConfiguration(first.Id, Now.AddMinutes(4)), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void LiveModeRequiresExplicitPromotionAndBotRequiresPortfolioAndConfiguration()
    {
        var bot = NewBot();
        Assert.That(() => AddConfiguration(bot, ExecutionMode.LiveTrading, Now), Throws.InvalidOperationException);
        var source = AddConfiguration(bot, ExecutionMode.ResearchOnly, Now);
        var live = bot.PromoteToLive(TradingBotConfigurationVersionId.New(), source.Id, Now.AddMinutes(1));
        bot.ActivateConfiguration(live.Id, Now.AddMinutes(2));
        Assert.That(() => bot.Enable(Now.AddMinutes(3)), Throws.InvalidOperationException);
        bot.AssignPortfolio(PortfolioId.New(), Now.AddMinutes(3));
        bot.Enable(Now.AddMinutes(4));
        Assert.That(bot.CanRun, Is.True);
    }

    [Test]
    public void RunPinsBotConfigurationAndSnapshotAndCoalescesDuplicateTriggerIdentity()
    {
        var configurationId = TradingBotConfigurationVersionId.New();
        var snapshotId = PortfolioDecisionSnapshotId.New();
        var run = NewRun(configurationId, snapshotId);
        var triggerId = BotRunTriggerId.New();
        run.AddTrigger(triggerId, BotRunTriggerType.Manual, "review", Now, "operator");
        run.AddTrigger(triggerId, BotRunTriggerType.Manual, "duplicate", Now);

        Assert.Multiple(() =>
        {
            Assert.That(run.ConfigurationVersionId, Is.EqualTo(configurationId));
            Assert.That(run.PortfolioSnapshotId, Is.EqualTo(snapshotId));
            Assert.That(run.Triggers, Has.Count.EqualTo(1));
        });
    }

    [TestCase(BotRunStatus.Completed)]
    [TestCase(BotRunStatus.TimedOut)]
    [TestCase(BotRunStatus.BudgetExceeded)]
    [TestCase(BotRunStatus.Cancelled)]
    [TestCase(BotRunStatus.Faulted)]
    public void EveryTerminalRunRejectsResumeAndNewFacts(BotRunStatus terminal)
    {
        var run = StartedRun();
        Finish(run, terminal);

        Assert.Multiple(() =>
        {
            Assert.That(run.IsTerminal, Is.True);
            Assert.That(() => run.Start("worker", Now.AddHours(1), Now.AddHours(2)), Throws.InvalidOperationException);
            Assert.That(() => run.AddTrigger(BotRunTriggerId.New(), BotRunTriggerType.Manual, "late", Now.AddHours(1)), Throws.InvalidOperationException);
            Assert.That(() => run.StartToolInvocation(ToolInvocationId.New(), "GetQuote", "{}", Now.AddHours(1)), Throws.InvalidOperationException);
        });
    }

    [TestCase("start_twice")]
    [TestCase("finish_pending")]
    [TestCase("finish_with_active_tool")]
    public void ForbiddenRunTransitionsAreRejected(string transition)
    {
        var run = transition == "finish_pending" ? NewRun() : StartedRun();
        if (transition == "finish_with_active_tool") run.StartToolInvocation(ToolInvocationId.New(), "GetQuote", "{}", Now.AddMinutes(1));

        Action action = transition switch
        {
            "start_twice" => () => run.Start("worker", Now.AddMinutes(1), Now.AddMinutes(10)),
            _ => () => run.Complete(new FinishResult(FinishStatus.Completed, "done"), ZeroUsage(), Now.AddMinutes(2)),
        };
        Assert.That(action, Throws.InvalidOperationException);
    }

    [Test]
    public void LeaseCanOnlyBeExtendedByOwnerWhileRunning()
    {
        var run = StartedRun();
        Assert.That(() => run.RenewLease("other", Now.AddMinutes(20)), Throws.InvalidOperationException);
        Assert.That(() => run.RenewLease("worker", Now.AddMinutes(5)), Throws.ArgumentException);
        run.RenewLease("worker", Now.AddMinutes(20));
        Assert.That(run.LeaseExpiresAt, Is.EqualTo(Now.AddMinutes(20)));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ToolInvocationsHaveValidTerminalTransitionsAndBecomeAppendOnly(bool succeeds)
    {
        var run = StartedRun();
        var invocation = run.StartToolInvocation(ToolInvocationId.New(), "GetQuote", "{\"symbol\":\"ABC\"}", Now.AddMinutes(1));
        if (succeeds) invocation.Complete("quote:1", ZeroUsage(), Now.AddMinutes(2));
        else invocation.Fail("unavailable", ZeroUsage(), Now.AddMinutes(2));

        Assert.Multiple(() =>
        {
            Assert.That(invocation.Status, Is.EqualTo(succeeds ? ToolInvocationStatus.Completed : ToolInvocationStatus.Failed));
            Assert.That(() => invocation.Fail("again", ZeroUsage(), Now.AddMinutes(3)), Throws.InvalidOperationException);
            Assert.That(run.ToolInvocations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void OnlySchedulerFacingContractAcceptsRequestedNextRun()
    {
        var run = StartedRun();
        var requested = Now.AddDays(1);
        run.Complete(new FinishResult(FinishStatus.Completed, "wait", requested, "event"), ZeroUsage(), Now.AddMinutes(2));
        Assert.That(typeof(BotRun).GetMethod(nameof(IBotRunScheduler.AcceptNextRun)), Is.Null);
        ((IBotRunScheduler)run).AcceptNextRun(requested.AddMinutes(5));
        Assert.That(run.AcceptedNextRunAt, Is.EqualTo(requested.AddMinutes(5)));
    }

    private static TradingBot NewBot() => new(TradingBotId.New(), "Growth", Now);
    private static TradingBotConfigurationVersion AddConfiguration(TradingBot bot, ExecutionMode mode, DateTimeOffset at) =>
        bot.AddConfiguration(TradingBotConfigurationVersionId.New(),
            new InvestmentMandate("Growth", TimeSpan.FromDays(365), new UniverseDefinition(["Equity"], ["NYSE"], [Currency.USD])),
            new RiskPolicy([new RiskLimit("exposure", 100m, "percent")]), new ToolPolicy([new ToolAllowance("GetQuote", 3)]),
            new RunBudget(TimeSpan.FromMinutes(5), 1000, new Money(1m, Currency.USD), 5, 1, 1),
            new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)), mode,
            new ModelConfiguration("provider", "model", 0m, 1000), "prompt-v1", at);

    private static BotRun NewRun(TradingBotConfigurationVersionId? configurationId = null, PortfolioDecisionSnapshotId? snapshotId = null) =>
        new(BotRunId.New(), TradingBotId.New(), configurationId ?? TradingBotConfigurationVersionId.New(), snapshotId ?? PortfolioDecisionSnapshotId.New(), ZeroUsage());
    private static BotRun StartedRun()
    {
        var run = NewRun();
        run.Start("worker", Now, Now.AddMinutes(10));
        return run;
    }
    private static Usage ZeroUsage() => new(TimeSpan.Zero, 0, new Money(0m, Currency.USD), 0, 0, 0);
    private static void Finish(BotRun run, BotRunStatus status)
    {
        switch (status)
        {
            case BotRunStatus.Completed: run.Complete(new FinishResult(FinishStatus.Completed, "done"), ZeroUsage(), Now.AddMinutes(2)); break;
            case BotRunStatus.TimedOut: run.TimeOut(ZeroUsage(), Now.AddMinutes(2)); break;
            case BotRunStatus.BudgetExceeded: run.ExceedBudget(ZeroUsage(), Now.AddMinutes(2)); break;
            case BotRunStatus.Cancelled: run.Cancel(ZeroUsage(), Now.AddMinutes(2)); break;
            case BotRunStatus.Faulted: run.Fault(ZeroUsage(), Now.AddMinutes(2)); break;
        }
    }
}

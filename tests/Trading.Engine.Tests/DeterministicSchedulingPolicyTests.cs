using Trading.Core.Bots;
using Trading.Core.Policies;
using Trading.Engine.Runtime;

namespace Trading.Engine.Tests;

[Category("SchedulingPolicy")]
public sealed class DeterministicSchedulingPolicyTests
{
    private static readonly DateTimeOffset MondayAtTen = new(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly UtcWeeklyWindow[] BusinessHours = Enum.GetValues<DayOfWeek>()
        .Where(day => day is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
        .Select(day => new UtcWeeklyWindow(day, TimeSpan.FromHours(9), TimeSpan.FromHours(17))).ToArray();

    [TestCaseSource(nameof(RequestCases))]
    public void RequestedWakeDecisionsAreDeterministicAndHonorEveryBoundary(RequestCase test)
    {
        var policy = new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(30), TimeSpan.FromHours(30), BusinessHours);
        var engine = new DeterministicSchedulingPolicy(new FixedClock(MondayAtTen));

        var first = engine.Decide(policy, TradingBotStatus.Enabled, MondayAtTen, test.RequestedAt);
        var second = engine.Decide(policy, TradingBotStatus.Enabled, MondayAtTen, test.RequestedAt);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.AcceptedTime, Is.EqualTo(test.AcceptedAt));
            Assert.That(first.BaselineTime, Is.EqualTo(new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero)));
            Assert.That(first.Outcome, Is.EqualTo(test.Outcome));
            Assert.That(first.ReasonCode, Is.EqualTo(test.ReasonCode));
            Assert.That(first.PolicyInputs.Windows, Is.EqualTo(BusinessHours));
        });
    }

    [TestCase(TradingBotStatus.Paused, ScheduleReasonCodes.BotPaused)]
    [TestCase(TradingBotStatus.Retired, ScheduleReasonCodes.BotRetired)]
    public void InactiveBotsHaveNoExecutableSchedule(TradingBotStatus status, string reason)
    {
        var decision = new DeterministicSchedulingPolicy(new FixedClock(MondayAtTen)).Decide(
            new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromDays(1), BusinessHours),
            status, MondayAtTen, MondayAtTen.AddHours(1));
        Assert.Multiple(() =>
        {
            Assert.That(decision.AcceptedTime, Is.Null);
            Assert.That(decision.BaselineTime, Is.Null);
            Assert.That(decision.Outcome, Is.EqualTo(ScheduleDecisionOutcome.NoSchedule));
            Assert.That(decision.ReasonCode, Is.EqualTo(reason));
        });
    }

    [Test]
    public void BaselineAdvancesFromOldAnchorAndCannotBeDelayedByRequest()
    {
        var engine = new DeterministicSchedulingPolicy(new FixedClock(MondayAtTen));
        var policy = new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.Zero, TimeSpan.FromDays(30), BusinessHours);
        var decision = engine.Decide(policy, TradingBotStatus.Enabled, MondayAtTen.AddDays(-3), MondayAtTen.AddDays(5));
        Assert.Multiple(() =>
        {
            Assert.That(decision.BaselineTime, Is.EqualTo(MondayAtTen.AddDays(1)));
            Assert.That(decision.AcceptedTime, Is.EqualTo(decision.BaselineTime));
            Assert.That(decision.ReasonCode, Is.EqualTo(ScheduleReasonCodes.BaselineEarlier));
        });
    }

    [Test]
    public void RequestAfterMaximumIsReducedToLatestEligibleInstant()
    {
        var decision = new DeterministicSchedulingPolicy(new FixedClock(MondayAtTen)).Decide(
            new SchedulingPolicy(TimeSpan.FromDays(7), TimeSpan.Zero, TimeSpan.FromHours(30), BusinessHours),
            TradingBotStatus.Enabled, MondayAtTen, MondayAtTen.AddDays(5));
        Assert.Multiple(() =>
        {
            Assert.That(decision.AcceptedTime, Is.EqualTo(new DateTimeOffset(2026, 8, 18, 16, 0, 0, TimeSpan.Zero)));
            Assert.That(decision.AcceptedTime, Is.LessThanOrEqualTo(MondayAtTen.AddHours(30)));
            Assert.That(decision.ReasonCode, Is.EqualTo(ScheduleReasonCodes.RequestReducedToMaximum));
        });
    }

    [Test]
    public void MissingRequestKeepsBaselineExecutable()
    {
        var decision = new DeterministicSchedulingPolicy(new FixedClock(MondayAtTen)).Decide(
            new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.Zero, TimeSpan.FromDays(2), BusinessHours),
            TradingBotStatus.Enabled, MondayAtTen, null);
        Assert.Multiple(() =>
        {
            Assert.That(decision.AcceptedTime, Is.EqualTo(decision.BaselineTime));
            Assert.That(decision.ReasonCode, Is.EqualTo(ScheduleReasonCodes.BaselineOnly));
        });
    }

    private static IEnumerable<RequestCase> RequestCases()
    {
        yield return new("inside", MondayAtTen.AddHours(1), MondayAtTen.AddHours(1), ScheduleDecisionOutcome.Accepted, ScheduleReasonCodes.RequestAccepted);
        yield return new("minimum-boundary", MondayAtTen.AddMinutes(30), MondayAtTen.AddMinutes(30), ScheduleDecisionOutcome.Accepted, ScheduleReasonCodes.RequestAccepted);
        yield return new("below-minimum", MondayAtTen.AddMinutes(1), MondayAtTen.AddMinutes(30), ScheduleDecisionOutcome.Adjusted, ScheduleReasonCodes.RequestRaisedToMinimum);
        yield return new("window-end", new DateTimeOffset(2026, 8, 17, 17, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero), ScheduleDecisionOutcome.Adjusted, ScheduleReasonCodes.RequestMovedToWindow);
        yield return new("after-maximum", MondayAtTen.AddDays(3), new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero), ScheduleDecisionOutcome.Adjusted, ScheduleReasonCodes.BaselineEarlier);
        yield return new("non-utc", MondayAtTen.AddHours(1).ToOffset(TimeSpan.FromHours(2)), new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero), ScheduleDecisionOutcome.Rejected, ScheduleReasonCodes.RequestNotUtc);
        yield return new("not-future", MondayAtTen, new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero), ScheduleDecisionOutcome.Rejected, ScheduleReasonCodes.RequestNotFuture);
    }

    public sealed record RequestCase(string Name, DateTimeOffset RequestedAt, DateTimeOffset AcceptedAt,
        ScheduleDecisionOutcome Outcome, string ReasonCode)
    {
        public override string ToString() => Name;
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IUtcClock;
}

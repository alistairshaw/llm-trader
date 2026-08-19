using Trading.Core.Bots;
using Trading.Core.Policies;

namespace Trading.Engine.Runtime;

public static class ScheduleReasonCodes
{
    public const string RequestAccepted = "schedule.request.accepted";
    public const string RequestRaisedToMinimum = "schedule.request.raised-to-minimum";
    public const string RequestReducedToMaximum = "schedule.request.reduced-to-maximum";
    public const string RequestMovedToWindow = "schedule.request.moved-to-window";
    public const string BaselineEarlier = "schedule.baseline.earlier";
    public const string BaselineOnly = "schedule.baseline.only";
    public const string RequestNotUtc = "schedule.request.not-utc";
    public const string RequestNotFuture = "schedule.request.not-future";
    public const string BotPaused = "schedule.bot.paused";
    public const string BotRetired = "schedule.bot.retired";
}

public sealed class DeterministicSchedulingPolicy(IUtcClock clock)
{
    public ScheduleDecision Decide(SchedulingPolicy policy, TradingBotStatus botStatus, DateTimeOffset activationOrPreviousAcceptedAt,
        DateTimeOffset? requestedTime)
    {
        ArgumentNullException.ThrowIfNull(policy);
        RequireUtc(activationOrPreviousAcceptedAt, nameof(activationOrPreviousAcceptedAt));
        var now = clock.UtcNow;
        RequireUtc(now, nameof(IUtcClock.UtcNow));
        var inputs = new SchedulePolicyInputs(policy.BaselineCadence, policy.MinimumRequestedWakeDelay,
            policy.MaximumRequestedWakeDelay, policy.Windows.ToArray());
        if (botStatus != TradingBotStatus.Enabled)
            return new(requestedTime, null, null, ScheduleDecisionOutcome.NoSchedule,
                botStatus == TradingBotStatus.Retired ? ScheduleReasonCodes.BotRetired : ScheduleReasonCodes.BotPaused, inputs);

        var baselineCandidate = activationOrPreviousAcceptedAt + policy.BaselineCadence;
        if (baselineCandidate <= now)
        {
            var elapsedTicks = (now - activationOrPreviousAcceptedAt).Ticks;
            var cadenceTicks = policy.BaselineCadence.Ticks;
            baselineCandidate = activationOrPreviousAcceptedAt + TimeSpan.FromTicks(((elapsedTicks / cadenceTicks) + 1) * cadenceTicks);
        }
        var baseline = NextPermitted(baselineCandidate, policy.Windows);
        if (requestedTime is null)
            return new(null, baseline, baseline, ScheduleDecisionOutcome.Accepted, ScheduleReasonCodes.BaselineOnly, inputs);
        if (requestedTime.Value.Offset != TimeSpan.Zero)
            return new(requestedTime, baseline, baseline, ScheduleDecisionOutcome.Rejected, ScheduleReasonCodes.RequestNotUtc, inputs);
        if (requestedTime.Value <= now)
            return new(requestedTime, baseline, baseline, ScheduleDecisionOutcome.Rejected, ScheduleReasonCodes.RequestNotFuture, inputs);

        var candidate = requestedTime.Value;
        var reason = ScheduleReasonCodes.RequestAccepted;
        var outcome = ScheduleDecisionOutcome.Accepted;
        var minimum = now + policy.MinimumRequestedWakeDelay;
        var maximum = now + policy.MaximumRequestedWakeDelay;
        if (candidate < minimum) { candidate = minimum; reason = ScheduleReasonCodes.RequestRaisedToMinimum; outcome = ScheduleDecisionOutcome.Adjusted; }
        if (candidate > maximum) { candidate = maximum; reason = ScheduleReasonCodes.RequestReducedToMaximum; outcome = ScheduleDecisionOutcome.Adjusted; }
        var permitted = NextPermitted(candidate, policy.Windows);
        if (permitted > maximum) permitted = PreviousPermitted(maximum, policy.Windows);
        if (permitted != candidate) { candidate = permitted; reason = ScheduleReasonCodes.RequestMovedToWindow; outcome = ScheduleDecisionOutcome.Adjusted; }
        if (baseline <= candidate)
            return new(requestedTime, baseline, baseline, ScheduleDecisionOutcome.Adjusted, ScheduleReasonCodes.BaselineEarlier, inputs);
        return new(requestedTime, candidate, baseline, outcome, reason, inputs);
    }

    public static DateTimeOffset NextPermitted(DateTimeOffset candidate, IReadOnlyList<UtcWeeklyWindow> windows)
    {
        RequireUtc(candidate, nameof(candidate));
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var date = candidate.UtcDateTime.Date.AddDays(dayOffset);
            var day = date.DayOfWeek;
            foreach (var window in windows.Where(x => x.DayOfWeek == day).OrderBy(x => x.StartTime))
            {
                var start = new DateTimeOffset(date + window.StartTime, TimeSpan.Zero);
                var end = new DateTimeOffset(date + window.EndTime, TimeSpan.Zero);
                if (dayOffset == 0 && candidate >= start && candidate < end) return candidate;
                if (start >= candidate) return start;
            }
        }
        throw new InvalidOperationException("A scheduling policy must contain a reachable weekly window.");
    }

    private static DateTimeOffset PreviousPermitted(DateTimeOffset candidate, IReadOnlyList<UtcWeeklyWindow> windows)
    {
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var date = candidate.UtcDateTime.Date.AddDays(-dayOffset);
            foreach (var window in windows.Where(x => x.DayOfWeek == date.DayOfWeek).OrderByDescending(x => x.EndTime))
            {
                var start = new DateTimeOffset(date + window.StartTime, TimeSpan.Zero);
                var end = new DateTimeOffset(date + window.EndTime, TimeSpan.Zero);
                if (candidate >= start && candidate < end) return candidate;
                if (end <= candidate) return end.AddTicks(-1);
            }
        }
        throw new InvalidOperationException("A scheduling policy must contain a reachable weekly window.");
    }

    private static void RequireUtc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be expressed in UTC.", name);
    }
}

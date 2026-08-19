using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Engine.Runtime;

public sealed record BotRunExecutionRequest(TradingBotId TradingBotId, string LeaseOwner,
    TimeSpan LeaseDuration, IModelSession ModelSession);

public enum BotRunExecutionOutcome
{
    Completed,
    TimedOut,
    BudgetExceeded,
    Cancelled,
    Faulted,
    BotIneligible,
    NoAuthorizedSnapshot,
    NoEligibleTriggers,
    ActiveRun,
    ConcurrencyConflict,
    LostLease,
}

public sealed record BotRunExecutionResult(BotRunExecutionOutcome Outcome, BotRunId? RunId,
    ScheduleDecision? ScheduleDecision, string Reason);

/// <summary>Coordinates durable state transitions for one Bot Run. Model and tool execution are delegated
/// only after every repository operation has completed, so no persistence transaction crosses that boundary.</summary>
public sealed class BotRunOrchestrationService(
    ITradingBotRepository bots,
    IPortfolioQueries portfolioQueries,
    BotTriggerCoalescingService triggerClaims,
    IBotRunRepository runs,
    IBotRunInputService inputs,
    IModelLoop modelLoop,
    DeterministicSchedulingPolicy scheduling,
    IUtcClock clock) : IBotRunExecutor
{
    public async Task<BotRunExecutionResult> ExecuteAsync(BotRunExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ModelSession);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseOwner);
        if (request.LeaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request));

        var bot = await bots.GetAsync(request.TradingBotId, cancellationToken).ConfigureAwait(false);
        if (bot is null || bot.Status != TradingBotStatus.Enabled || bot.ActiveConfigurationVersionId is null)
            return Result(BotRunExecutionOutcome.BotIneligible, null, null, "bot_ineligible");
        var configuration = bot.ConfigurationVersions.Single(x => x.Id == bot.ActiveConfigurationVersionId);
        var snapshot = (await portfolioQueries.GetDecisionSnapshotsAsync(
            new PortfolioDecisionSnapshotQueryFilter(TradingBotId: bot.Id), new PageRequest(0, 100), cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault(x => x.ConfigurationVersionId == configuration.Id);
        if (snapshot is null)
            return Result(BotRunExecutionOutcome.NoAuthorizedSnapshot, null, null, "no_authorized_snapshot");

        var claim = await triggerClaims.TryClaimAsync(new TriggerClaimRequest(bot.Id, configuration.Id, snapshot.Id,
            request.LeaseOwner, request.LeaseDuration), cancellationToken).ConfigureAwait(false);
        if (claim is not TriggerCoalescingResult.Claimed acquired) return claim switch
        {
            TriggerCoalescingResult.NoEligibleTriggers => Result(BotRunExecutionOutcome.NoEligibleTriggers, null, null, "no_eligible_triggers"),
            TriggerCoalescingResult.BotIneligible => Result(BotRunExecutionOutcome.BotIneligible, null, null, "bot_ineligible"),
            TriggerCoalescingResult.ActiveRun active => Result(BotRunExecutionOutcome.ActiveRun, active.RunId, null, "active_run"),
            TriggerCoalescingResult.ConcurrencyConflict => Result(BotRunExecutionOutcome.ConcurrencyConflict, null, null, "concurrency_conflict"),
            _ => throw new InvalidOperationException("Unknown trigger claim result."),
        };

        DeterministicBotRunInput input;
        try
        {
            input = await inputs.PrepareAsync(acquired.Run.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return await TerminateBeforeReasoning(acquired.Run.Id, BotRunExecutionOutcome.Cancelled,
                "cancelled", true).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return await TerminateBeforeReasoning(acquired.Run.Id, BotRunExecutionOutcome.Faulted,
                "input_preparation_failed", false).ConfigureAwait(false);
        }

        var run = await Load(acquired.Run.Id).ConfigureAwait(false);
        var newExpiry = LaterThan(run.LeaseExpiresAt, clock.UtcNow + request.LeaseDuration);
        if (!await runs.RenewLeaseAsync(run.Id, request.LeaseOwner.Trim(), newExpiry, run.Version, cancellationToken)
            .ConfigureAwait(false))
            return Result(BotRunExecutionOutcome.LostLease, run.Id, null, "lease_ownership_lost");

        run = await Load(run.Id).ConfigureAwait(false);
        run.BeginReasoning();
        if (await runs.SaveAsync(run, run.Version, cancellationToken).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
            return Result(BotRunExecutionOutcome.ConcurrencyConflict, run.Id, null, "reasoning_start_conflict");
        input = input with { Run = await Load(run.Id).ConfigureAwait(false) };

        RunResult loopResult;
        try
        {
            // This call intentionally occurs after all repository calls above have completed.
            loopResult = await modelLoop.ExecuteAsync(input, request.ModelSession, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return await TerminateActive(run.Id, BotRunExecutionOutcome.Cancelled, "cancelled", true).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return await TerminateActive(run.Id, BotRunExecutionOutcome.Faulted, "orchestration_faulted", false).ConfigureAwait(false);
        }

        run = await Load(run.Id).ConfigureAwait(false);
        ScheduleDecision? decision = null;
        if (run.Status == BotRunStatus.Completed)
        {
            var currentBot = await bots.GetAsync(bot.Id, CancellationToken.None).ConfigureAwait(false) ?? bot;
            var anchor = currentBot.AcceptedNextRunAt ?? configuration.ActivatedAt ?? configuration.CreatedAt;
            decision = scheduling.Decide(configuration.SchedulingPolicy, currentBot.Status, anchor, run.RequestedNextRunAt);
            if (run.RequestedNextRunAt is not null && decision.AcceptedTime is not null)
            {
                ((IBotRunScheduler)run).AcceptNextRun(decision.AcceptedTime.Value);
                if (await runs.SaveAsync(run, run.Version, CancellationToken.None).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
                    return Result(BotRunExecutionOutcome.ConcurrencyConflict, run.Id, decision, "schedule_persistence_conflict");
            }
        }
        return Result(Map(loopResult.Outcome), run.Id, decision, loopResult.Summary ?? "run_terminated");
    }

    private async Task<BotRunExecutionResult> TerminateBeforeReasoning(BotRunId id,
        BotRunExecutionOutcome outcome, string reason, bool cancelled) =>
        await Terminate(id, outcome, reason, cancelled).ConfigureAwait(false);

    private async Task<BotRunExecutionResult> TerminateActive(BotRunId id,
        BotRunExecutionOutcome outcome, string reason, bool cancelled) =>
        await Terminate(id, outcome, reason, cancelled).ConfigureAwait(false);

    private async Task<BotRunExecutionResult> Terminate(BotRunId id, BotRunExecutionOutcome outcome,
        string reason, bool cancelled)
    {
        var run = await Load(id).ConfigureAwait(false);
        if (!run.IsTerminal)
        {
            run.RecordTerminalReason(reason);
            if (cancelled) run.Cancel(run.Usage, clock.UtcNow); else run.Fault(run.Usage, clock.UtcNow);
            if (await runs.SaveAsync(run, run.Version, CancellationToken.None).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
                return Result(BotRunExecutionOutcome.ConcurrencyConflict, id, null, "terminal_persistence_conflict");
        }
        return Result(outcome, id, null, reason);
    }

    private async Task<BotRun> Load(BotRunId id) =>
        await runs.GetAsync(id, CancellationToken.None).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Bot Run not found.");
    private static DateTimeOffset LaterThan(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is not null && candidate <= current ? current.Value.AddMilliseconds(1) : candidate;
    private static BotRunExecutionOutcome Map(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Completed => BotRunExecutionOutcome.Completed,
        RunOutcome.TimedOut => BotRunExecutionOutcome.TimedOut,
        RunOutcome.BudgetExceeded => BotRunExecutionOutcome.BudgetExceeded,
        RunOutcome.Cancelled => BotRunExecutionOutcome.Cancelled,
        _ => BotRunExecutionOutcome.Faulted,
    };
    private static BotRunExecutionResult Result(BotRunExecutionOutcome outcome, BotRunId? runId,
        ScheduleDecision? decision, string reason) => new(outcome, runId, decision, reason);
}

using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;

namespace Trading.Engine.Runtime;

public sealed record TriggerRequest(TradingBotId TradingBotId, BotRunTriggerType Type, string Reason,
    DateTimeOffset OccurredAt, string? SourceType = null, string? SourceId = null);

public enum TriggerIngestionOutcome { Accepted, Duplicate }
public sealed record TriggerIngestionResult(TriggerIngestionOutcome Outcome, BotRunTriggerId TriggerId);

public sealed class BotTriggerIngestionService(
    IBotRunTriggerRepository triggers,
    IRuntimeIdentifierGenerator identifiers,
    IUtcClock clock)
{
    private static readonly HashSet<BotRunTriggerType> AuthorizedTypes =
    [
        BotRunTriggerType.Manual,
        BotRunTriggerType.BaselineSchedule,
        BotRunTriggerType.AcceptedNextRun,
        BotRunTriggerType.PortfolioEvent,
        BotRunTriggerType.RiskOrReconciliation,
    ];

    public async Task<TriggerIngestionResult> IngestAsync(TriggerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!AuthorizedTypes.Contains(request.Type))
            throw new ArgumentException("The trigger type is not authorized in the Stage 3 runtime.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        RequireUtc(request.OccurredAt, nameof(request.OccurredAt));
        RequireUtc(clock.UtcNow, nameof(IUtcClock.UtcNow));
        if ((request.SourceType is null) != (request.SourceId is null))
            throw new ArgumentException("Source type and source identity must be supplied together.", nameof(request));

        var trigger = new PendingBotRunTrigger(identifiers.NewTriggerId(), request.TradingBotId, request.Type,
            request.Reason.Trim(), request.OccurredAt, clock.UtcNow, Normalize(request.SourceType), Normalize(request.SourceId));
        var result = await triggers.AppendAsync(trigger, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            PersistenceWriteResult.Succeeded => new(TriggerIngestionOutcome.Accepted, trigger.Id),
            PersistenceWriteResult.UniquenessConflict when trigger.SourceId is not null =>
                new(TriggerIngestionOutcome.Duplicate, trigger.Id),
            _ => throw new InvalidOperationException("Trigger ingestion did not complete durably."),
        };
    }

    private static string? Normalize(string? value) => value?.Trim() is { Length: > 0 } normalized
        ? normalized
        : value is null ? null : throw new ArgumentException("Source values cannot be empty.");
    private static void RequireUtc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be expressed in UTC.", name);
    }
}

public sealed record TriggerClaimRequest(TradingBotId TradingBotId,
    TradingBotConfigurationVersionId ConfigurationVersionId, PortfolioDecisionSnapshotId PortfolioSnapshotId,
    string LeaseOwner, TimeSpan LeaseDuration, int ModelTranscriptSchemaVersion = 1,
    string ModelTranscriptJson = "{}", string InputRenderingVersion = "v1");

public abstract record TriggerCoalescingResult
{
    private TriggerCoalescingResult() { }
    public sealed record Claimed(BotRun Run) : TriggerCoalescingResult;
    public sealed record NoEligibleTriggers : TriggerCoalescingResult;
    public sealed record BotIneligible : TriggerCoalescingResult;
    public sealed record ActiveRun(BotRunId? RunId) : TriggerCoalescingResult;
    public sealed record ConcurrencyConflict : TriggerCoalescingResult;
}

public sealed class BotTriggerCoalescingService(
    ITradingBotRepository bots,
    IBotRunTriggerRepository triggers,
    IBotRunRepository runs,
    IRuntimeIdentifierGenerator identifiers,
    IUtcClock clock)
{
    public async Task<TriggerCoalescingResult> TryClaimAsync(TriggerClaimRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LeaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseOwner);
        var now = clock.UtcNow;
        RequireUtc(now);

        // Eligibility is deliberately loaded immediately before the transactional claim.
        var bot = await bots.GetAsync(request.TradingBotId, cancellationToken).ConfigureAwait(false);
        if (bot is null || bot.Status != TradingBotStatus.Enabled ||
            bot.ActiveConfigurationVersionId != request.ConfigurationVersionId)
            return new TriggerCoalescingResult.BotIneligible();

        var pending = await triggers.GetPendingAsync(request.TradingBotId, cancellationToken).ConfigureAwait(false);
        if (!pending.Any(x => x.OccurredAt <= now)) return new TriggerCoalescingResult.NoEligibleTriggers();

        var zeroUsage = new Usage(TimeSpan.Zero, 0, new Money(0m, Currency.USD), 0, 0, 0);
        var claim = new BotRunClaim(identifiers.NewBotRunId(), request.TradingBotId, request.ConfigurationVersionId,
            request.PortfolioSnapshotId, request.LeaseOwner.Trim(), now, now + request.LeaseDuration, zeroUsage,
            request.ModelTranscriptSchemaVersion, request.ModelTranscriptJson, request.InputRenderingVersion);
        return await runs.TryClaimAsync(claim, cancellationToken).ConfigureAwait(false) switch
        {
            BotRunLeaseResult.Acquired acquired => new TriggerCoalescingResult.Claimed(acquired.Run),
            BotRunLeaseResult.ActiveLeaseConflict conflict => new TriggerCoalescingResult.ActiveRun(conflict.ActiveRunId),
            BotRunLeaseResult.ConcurrencyConflict => new TriggerCoalescingResult.ConcurrencyConflict(),
            _ => throw new InvalidOperationException("Unknown Bot Run lease result."),
        };
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new InvalidOperationException("The runtime clock must return UTC.");
    }
}

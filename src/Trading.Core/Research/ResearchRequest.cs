using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Research;

public enum ResearchVisibility { Shared, Restricted, BotPrivate }
public enum ResearchRequestStatus { Requested, Validating, Queued, Running, WaitingForTool, Completed, Failed, TimedOut, BudgetExceeded, Cancelled }
public enum ResearchNotificationStatus { Pending, Delivered, Failed }
public enum ResearchTerminalOutcome { Completed, Failed, TimedOut, BudgetExceeded, Cancelled }

public sealed class ResearchRequest
{
    private readonly HashSet<TradingBotId> _authorizedSubscribers;
    private readonly List<ResearchSubscription> _subscriptions = [];

    public ResearchRequest(ResearchRequestId id, TradingBotId requestingBotId, string subject, string question,
        DateTimeOffset asOf, ResearchVisibility visibility, DataFreshness freshnessRequirement,
        string normalizedResearchKey, DateTimeOffset requestedAt, IEnumerable<TradingBotId>? authorizedSubscribers = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        RequestingBotId = requestingBotId ?? throw new ArgumentNullException(nameof(requestingBotId));
        Subject = ResearchValidation.Required(subject, nameof(subject), 300);
        Question = ResearchValidation.Required(question, nameof(question));
        AsOf = ResearchValidation.Utc(asOf, nameof(asOf));
        RequestedAt = ResearchValidation.Utc(requestedAt, nameof(requestedAt));
        if (AsOf > RequestedAt) throw new ArgumentException("Research as-of time cannot follow request time.", nameof(asOf));
        Visibility = visibility;
        FreshnessRequirement = freshnessRequirement ?? throw new ArgumentNullException(nameof(freshnessRequirement));
        NormalizedResearchKey = ResearchValidation.Required(normalizedResearchKey, nameof(normalizedResearchKey), 500);
        _authorizedSubscribers = authorizedSubscribers?.ToHashSet() ?? [];
        _authorizedSubscribers.Add(RequestingBotId);
        Status = ResearchRequestStatus.Requested;
    }

    public ResearchRequestId Id { get; }
    public TradingBotId RequestingBotId { get; }
    public string Subject { get; }
    public string Question { get; }
    public DateTimeOffset AsOf { get; }
    public ResearchRequestStatus Status { get; private set; }
    public ResearchVisibility Visibility { get; private set; }
    public DataFreshness FreshnessRequirement { get; }
    public string NormalizedResearchKey { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public ResearchReportId? ResultReportId { get; private set; }
    public bool HasPrivateInputs { get; private set; }
    public IReadOnlyList<ResearchSubscription> Subscriptions => _subscriptions.AsReadOnly();

    public void RecordPrivateInputs()
    {
        if (Visibility == ResearchVisibility.Shared)
            throw new InvalidOperationException("Visibility must be narrowed before recording private inputs.");
        HasPrivateInputs = true;
    }

    public void ChangeVisibility(ResearchVisibility visibility)
    {
        if (HasPrivateInputs && IsBroader(visibility, Visibility))
            throw new InvalidOperationException("Visibility cannot be broadened after private inputs exist.");
        Visibility = visibility;
    }

    public ResearchSubscription Subscribe(ResearchSubscriptionId id, TradingBotId botId, DateTimeOffset subscribedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(botId);
        ResearchValidation.Utc(subscribedAt, nameof(subscribedAt));
        if (!_authorizedSubscribers.Contains(botId)) throw new UnauthorizedAccessException("Trading Bot is not authorized to subscribe.");
        if (_subscriptions.Any(subscription => subscription.TradingBotId == botId))
            throw new InvalidOperationException("Trading Bot is already subscribed.");
        var subscription = new ResearchSubscription(id, botId, subscribedAt);
        _subscriptions.Add(subscription);
        return subscription;
    }

    public void Start(DateTimeOffset startedAt)
    {
        if (Status != ResearchRequestStatus.Queued)
            throw new InvalidOperationException("Only a queued request can start.");
        ResearchValidation.Utc(startedAt, nameof(startedAt));
        if (startedAt < RequestedAt) throw new ArgumentException("Start cannot precede request.", nameof(startedAt));
        Status = ResearchRequestStatus.Running;
        StartedAt = startedAt;
    }

    public void BeginValidation() => TransitionPending(ResearchRequestStatus.Requested, ResearchRequestStatus.Validating);
    public void Queue() => TransitionPending(ResearchRequestStatus.Validating, ResearchRequestStatus.Queued);
    public void WaitForTool() => TransitionPending(ResearchRequestStatus.Running, ResearchRequestStatus.WaitingForTool);
    public void ResumeFromTool() => TransitionPending(ResearchRequestStatus.WaitingForTool, ResearchRequestStatus.Running);

    public void Complete(ResearchReportId publishedReportId, DateTimeOffset completedAt)
    {
        if (Status != ResearchRequestStatus.Running) throw new InvalidOperationException("Only a running request can complete.");
        ResultReportId = publishedReportId ?? throw new ArgumentNullException(nameof(publishedReportId));
        ResearchValidation.Utc(completedAt, nameof(completedAt));
        if (completedAt < StartedAt) throw new ArgumentException("Completion cannot precede start.", nameof(completedAt));
        Status = ResearchRequestStatus.Completed;
        CompletedAt = completedAt;
    }

    public void Terminate(ResearchTerminalOutcome outcome, DateTimeOffset completedAt)
    {
        if (outcome == ResearchTerminalOutcome.Completed)
            throw new ArgumentException("Completion requires a published report.", nameof(outcome));
        if (Status is not ResearchRequestStatus.Running and not ResearchRequestStatus.WaitingForTool)
            throw new InvalidOperationException("Only an active request can terminate.");
        ResearchValidation.Utc(completedAt, nameof(completedAt));
        if (completedAt < StartedAt) throw new ArgumentException("Completion cannot precede start.", nameof(completedAt));
        Status = outcome switch
        {
            ResearchTerminalOutcome.Failed => ResearchRequestStatus.Failed,
            ResearchTerminalOutcome.TimedOut => ResearchRequestStatus.TimedOut,
            ResearchTerminalOutcome.BudgetExceeded => ResearchRequestStatus.BudgetExceeded,
            ResearchTerminalOutcome.Cancelled => ResearchRequestStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
        CompletedAt = completedAt;
    }

    private void TransitionPending(ResearchRequestStatus required, ResearchRequestStatus next)
    {
        if (Status != required) throw new InvalidOperationException($"Request must be {required}.");
        Status = next;
    }

    private static bool IsBroader(ResearchVisibility next, ResearchVisibility current) => (int)next < (int)current;
}

public sealed class ResearchSubscription
{
    internal ResearchSubscription(ResearchSubscriptionId id, TradingBotId botId, DateTimeOffset subscribedAt)
    {
        Id = id; TradingBotId = botId; SubscribedAt = subscribedAt;
    }
    public ResearchSubscriptionId Id { get; }
    public TradingBotId TradingBotId { get; }
    public DateTimeOffset SubscribedAt { get; }
    public ResearchNotificationStatus NotificationStatus { get; private set; } = ResearchNotificationStatus.Pending;
    public void MarkDelivered() => TransitionNotification(ResearchNotificationStatus.Delivered);
    public void MarkFailed() => TransitionNotification(ResearchNotificationStatus.Failed);
    private void TransitionNotification(ResearchNotificationStatus next)
    {
        if (NotificationStatus != ResearchNotificationStatus.Pending)
            throw new InvalidOperationException("A subscription notification has one terminal outcome.");
        NotificationStatus = next;
    }
}

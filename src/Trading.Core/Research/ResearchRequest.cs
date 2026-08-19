using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Research;

public enum ResearchVisibility { Shared, Restricted, BotPrivate }
public enum ResearchRequestStatus { Requested, Validating, Queued, Running, Completed, Failed, TimedOut, BudgetExceeded, Cancelled }
public enum ResearchNotificationStatus { Pending, Delivered, Failed }

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

    public void RecordPrivateInputs() => HasPrivateInputs = true;

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
        if (Status is not ResearchRequestStatus.Requested and not ResearchRequestStatus.Validating and not ResearchRequestStatus.Queued)
            throw new InvalidOperationException("Only a pending request can start.");
        ResearchValidation.Utc(startedAt, nameof(startedAt));
        if (startedAt < RequestedAt) throw new ArgumentException("Start cannot precede request.", nameof(startedAt));
        Status = ResearchRequestStatus.Running;
        StartedAt = startedAt;
    }

    public void Complete(ResearchReportId publishedReportId, DateTimeOffset completedAt)
    {
        if (Status != ResearchRequestStatus.Running) throw new InvalidOperationException("Only a running request can complete.");
        ResultReportId = publishedReportId ?? throw new ArgumentNullException(nameof(publishedReportId));
        ResearchValidation.Utc(completedAt, nameof(completedAt));
        if (completedAt < StartedAt) throw new ArgumentException("Completion cannot precede start.", nameof(completedAt));
        Status = ResearchRequestStatus.Completed;
        CompletedAt = completedAt;
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
    public void MarkDelivered() => NotificationStatus = ResearchNotificationStatus.Delivered;
    public void MarkFailed() => NotificationStatus = ResearchNotificationStatus.Failed;
}

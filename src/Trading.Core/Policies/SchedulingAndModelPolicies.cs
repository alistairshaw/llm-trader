namespace Trading.Core.Policies;

public sealed record SchedulingPolicy
{
    public SchedulingPolicy(TimeSpan baselineCadence, TimeSpan minimumRequestedWakeDelay, TimeSpan maximumRequestedWakeDelay)
    {
        if (baselineCadence <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baselineCadence), baselineCadence, "Baseline cadence must be positive.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRequestedWakeDelay, TimeSpan.Zero);

        if (maximumRequestedWakeDelay < minimumRequestedWakeDelay)
        {
            throw new ArgumentException("Maximum requested-wake delay cannot be less than the minimum.", nameof(maximumRequestedWakeDelay));
        }

        BaselineCadence = baselineCadence;
        MinimumRequestedWakeDelay = minimumRequestedWakeDelay;
        MaximumRequestedWakeDelay = maximumRequestedWakeDelay;
    }

    public TimeSpan BaselineCadence { get; }
    public TimeSpan MinimumRequestedWakeDelay { get; }
    public TimeSpan MaximumRequestedWakeDelay { get; }
}

public sealed record ModelConfiguration
{
    public ModelConfiguration(string provider, string model, decimal temperature, int maximumOutputTokens)
    {
        Provider = PolicyValidation.Required(provider, nameof(provider));
        Model = PolicyValidation.Required(model, nameof(model));
        if (temperature is < 0m or > 2m)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "Temperature must be between zero and two inclusive.");
        }

        if (maximumOutputTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens), maximumOutputTokens, "Maximum output tokens must be positive.");
        }

        Temperature = temperature;
        MaximumOutputTokens = maximumOutputTokens;
    }

    public string Provider { get; }
    public string Model { get; }
    public decimal Temperature { get; }
    public int MaximumOutputTokens { get; }
}

public enum FinishStatus
{
    Completed,
    Incomplete,
    Failed,
}

public sealed record FinishResult
{
    public FinishResult(FinishStatus status, string summary, DateTimeOffset? requestedNextRunAt = null, string? wakeReason = null)
    {
        Summary = PolicyValidation.Required(summary, nameof(summary));
        if (requestedNextRunAt is not null)
        {
            PolicyValidation.Utc(requestedNextRunAt.Value, nameof(requestedNextRunAt));
        }

        if ((requestedNextRunAt is null) != (wakeReason is null))
        {
            throw new ArgumentException("A requested next run and wake reason must be supplied together.", nameof(wakeReason));
        }

        Status = status;
        RequestedNextRunAt = requestedNextRunAt;
        WakeReason = wakeReason is null ? null : PolicyValidation.Required(wakeReason, nameof(wakeReason));
    }

    public FinishStatus Status { get; }
    public string Summary { get; }
    public DateTimeOffset? RequestedNextRunAt { get; }
    public string? WakeReason { get; }
}

public sealed record DataFreshness
{
    public DataFreshness(DateTimeOffset sourceAsOf, DateTimeOffset retrievedAt, TimeSpan maximumAge)
    {
        PolicyValidation.Utc(sourceAsOf, nameof(sourceAsOf));
        PolicyValidation.Utc(retrievedAt, nameof(retrievedAt));
        if (sourceAsOf > retrievedAt)
        {
            throw new ArgumentException("Source timestamp cannot be later than retrieval.", nameof(sourceAsOf));
        }

        if (maximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAge), maximumAge, "Maximum age cannot be negative.");
        }

        SourceAsOf = sourceAsOf;
        RetrievedAt = retrievedAt;
        MaximumAge = maximumAge;
    }

    public DateTimeOffset SourceAsOf { get; }
    public DateTimeOffset RetrievedAt { get; }
    public TimeSpan MaximumAge { get; }

    public bool IsStaleAt(DateTimeOffset evaluatedAt)
    {
        PolicyValidation.Utc(evaluatedAt, nameof(evaluatedAt));
        if (evaluatedAt < RetrievedAt)
        {
            throw new ArgumentException("Evaluation cannot precede retrieval.", nameof(evaluatedAt));
        }

        return evaluatedAt - SourceAsOf > MaximumAge;
    }
}

using System.Text.Json.Serialization;

namespace Trading.Core.Policies;

public sealed record UtcWeeklyWindow
{
    public UtcWeeklyWindow(DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime)
    {
        if (!Enum.IsDefined(dayOfWeek)) throw new ArgumentOutOfRangeException(nameof(dayOfWeek));
        if (startTime < TimeSpan.Zero || startTime >= TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(startTime));
        if (endTime <= TimeSpan.Zero || endTime > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(endTime));
        if (endTime <= startTime) throw new ArgumentException("Window end must be after its start; split overnight availability into two windows.", nameof(endTime));
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public DayOfWeek DayOfWeek { get; }
    public TimeSpan StartTime { get; }
    public TimeSpan EndTime { get; }
}

public sealed record SchedulingPolicy
{
    public SchedulingPolicy(TimeSpan baselineCadence, TimeSpan minimumRequestedWakeDelay, TimeSpan maximumRequestedWakeDelay)
        : this(baselineCadence, minimumRequestedWakeDelay, maximumRequestedWakeDelay, null, 0)
    {
    }

    public SchedulingPolicy(TimeSpan baselineCadence, TimeSpan minimumRequestedWakeDelay, TimeSpan maximumRequestedWakeDelay,
        IEnumerable<UtcWeeklyWindow> windows)
        : this(baselineCadence, minimumRequestedWakeDelay, maximumRequestedWakeDelay, windows?.ToArray(), CurrentSchemaVersion)
    {
    }

    [JsonConstructor]
    public SchedulingPolicy(TimeSpan baselineCadence, TimeSpan minimumRequestedWakeDelay, TimeSpan maximumRequestedWakeDelay,
        IReadOnlyList<UtcWeeklyWindow>? windows, int schemaVersion)
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

        if (schemaVersion is not (0 or CurrentSchemaVersion)) throw new ArgumentException("Scheduling policy schema version is unsupported.", nameof(schemaVersion));

        var normalizedWindows = (windows ?? FullWeek).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).ToArray();
        if (normalizedWindows.Length == 0) throw new ArgumentException("At least one UTC scheduling window is required.", nameof(windows));
        for (var index = 1; index < normalizedWindows.Length; index++)
        {
            var previous = normalizedWindows[index - 1];
            var current = normalizedWindows[index];
            if (previous.DayOfWeek == current.DayOfWeek && current.StartTime < previous.EndTime)
                throw new ArgumentException("UTC scheduling windows cannot overlap.", nameof(windows));
        }

        BaselineCadence = baselineCadence;
        MinimumRequestedWakeDelay = minimumRequestedWakeDelay;
        MaximumRequestedWakeDelay = maximumRequestedWakeDelay;
        Windows = Array.AsReadOnly(normalizedWindows);
        SchemaVersion = CurrentSchemaVersion;
    }

    public const int CurrentSchemaVersion = 1;
    public TimeSpan BaselineCadence { get; }
    public TimeSpan MinimumRequestedWakeDelay { get; }
    public TimeSpan MaximumRequestedWakeDelay { get; }
    public IReadOnlyList<UtcWeeklyWindow> Windows { get; }
    public int SchemaVersion { get; }

    public bool Equals(SchedulingPolicy? other) => other is not null &&
        BaselineCadence == other.BaselineCadence &&
        MinimumRequestedWakeDelay == other.MinimumRequestedWakeDelay &&
        MaximumRequestedWakeDelay == other.MaximumRequestedWakeDelay &&
        Windows.SequenceEqual(other.Windows);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BaselineCadence); hash.Add(MinimumRequestedWakeDelay); hash.Add(MaximumRequestedWakeDelay);
        foreach (var window in Windows) hash.Add(window);
        return hash.ToHashCode();
    }

    private static readonly UtcWeeklyWindow[] FullWeek = Enum.GetValues<DayOfWeek>()
        .Select(day => new UtcWeeklyWindow(day, TimeSpan.Zero, TimeSpan.FromDays(1))).ToArray();
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

using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Research;

public sealed record SourceCitation
{
    public SourceCitation(string provider, string sourceIdentifier, DateTimeOffset? publishedAt,
        DateTimeOffset retrievedAt, string contentHash)
    {
        Provider = ResearchValidation.Required(provider, nameof(provider), 200);
        SourceIdentifier = ResearchValidation.Required(sourceIdentifier, nameof(sourceIdentifier), 2000);
        if (publishedAt is not null) ResearchValidation.Utc(publishedAt.Value, nameof(publishedAt));
        RetrievedAt = ResearchValidation.Utc(retrievedAt, nameof(retrievedAt));
        if (publishedAt > retrievedAt) throw new ArgumentException("Publication cannot follow retrieval.", nameof(publishedAt));
        ContentHash = ResearchValidation.Required(contentHash, nameof(contentHash), 256);
        PublishedAt = publishedAt;
    }
    public string Provider { get; }
    public string SourceIdentifier { get; }
    public DateTimeOffset? PublishedAt { get; }
    public DateTimeOffset RetrievedAt { get; }
    public string ContentHash { get; }
}

public sealed record ReportProvenance
{
    private readonly SourceCitation[] _sources;
    public ReportProvenance(IEnumerable<SourceCitation> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources.ToArray();
        if (_sources.Length == 0 || _sources.Any(source => source is null))
            throw new ArgumentException("At least one non-null source is required.", nameof(sources));
    }
    public IReadOnlyList<SourceCitation> Sources => Array.AsReadOnly(_sources);
    public bool Equals(ReportProvenance? other) => other is not null && _sources.SequenceEqual(other._sources);
    public override int GetHashCode() => _sources.Aggregate(new HashCode(), (hash, source) => { hash.Add(source); return hash; }).ToHashCode();
}

public sealed record GeneratorMetadata
{
    public GeneratorMetadata(ModelConfiguration model, string promptVersion, string toolSetVersion, string reportSchemaVersion)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        PromptVersion = ResearchValidation.Required(promptVersion, nameof(promptVersion), 200);
        ToolSetVersion = ResearchValidation.Required(toolSetVersion, nameof(toolSetVersion), 200);
        ReportSchemaVersion = ResearchValidation.Required(reportSchemaVersion, nameof(reportSchemaVersion), 200);
    }
    public ModelConfiguration Model { get; }
    public string PromptVersion { get; }
    public string ToolSetVersion { get; }
    public string ReportSchemaVersion { get; }
}

public enum ResearchReportStatus { Published, Expired, Superseded, Corrected, Retracted }

public sealed class ResearchReport
{
    public ResearchReport(ResearchReportId id, string reportSeriesId, int versionNumber, ResearchRequestId requestId,
        string subject, string question, ResearchVisibility visibility, DateTimeOffset dataCutoff,
        DateTimeOffset generatedAt, DateTimeOffset expiresAt, ResearchReportId? supersedesReportId,
        string content, string contentHash, ReportProvenance provenance, GeneratorMetadata generatorMetadata)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ReportSeriesId = ResearchValidation.Required(reportSeriesId, nameof(reportSeriesId), 200);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        VersionNumber = versionNumber;
        ResearchRequestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
        Subject = ResearchValidation.Required(subject, nameof(subject), 300);
        Question = ResearchValidation.Required(question, nameof(question));
        Visibility = visibility;
        DataCutoff = ResearchValidation.Utc(dataCutoff, nameof(dataCutoff));
        GeneratedAt = ResearchValidation.Utc(generatedAt, nameof(generatedAt));
        ExpiresAt = ResearchValidation.Utc(expiresAt, nameof(expiresAt));
        if (DataCutoff > GeneratedAt) throw new ArgumentException("Data cutoff cannot follow generation.", nameof(dataCutoff));
        if (ExpiresAt <= GeneratedAt) throw new ArgumentException("Expiration must follow generation.", nameof(expiresAt));
        SupersedesReportId = supersedesReportId;
        if ((versionNumber == 1) != (supersedesReportId is null))
            throw new ArgumentException("Only the first version may omit a superseded report.", nameof(supersedesReportId));
        Content = ResearchValidation.Required(content, nameof(content), 100_000);
        ContentHash = ResearchValidation.Required(contentHash, nameof(contentHash), 256);
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        GeneratorMetadata = generatorMetadata ?? throw new ArgumentNullException(nameof(generatorMetadata));
    }

    public ResearchReportId Id { get; }
    public string ReportSeriesId { get; }
    public int VersionNumber { get; }
    public ResearchRequestId ResearchRequestId { get; }
    public string Subject { get; }
    public string Question { get; }
    public ResearchVisibility Visibility { get; }
    public DateTimeOffset DataCutoff { get; }
    public DateTimeOffset GeneratedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public ResearchReportId? SupersedesReportId { get; }
    public string Content { get; }
    public string ContentHash { get; }
    public ReportProvenance Provenance { get; }
    public GeneratorMetadata GeneratorMetadata { get; }
    public ResearchReportStatus Status { get; private set; } = ResearchReportStatus.Published;
    public static ResearchReport Rehydrate(ResearchReportState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var report = new ResearchReport(state.Id, state.ReportSeriesId, state.VersionNumber, state.ResearchRequestId,
            state.Subject, state.Question, state.Visibility, state.DataCutoff, state.GeneratedAt, state.ExpiresAt,
            state.SupersedesReportId, state.Content, state.ContentHash, state.Provenance, state.GeneratorMetadata);
        report.Status = state.Status;
        return report;
    }
    public bool IsFreshAt(DateTimeOffset at) { ResearchValidation.Utc(at, nameof(at)); return Status == ResearchReportStatus.Published && at <= ExpiresAt; }
    public void MarkExpired() => TransitionTo(ResearchReportStatus.Expired);
    public void MarkSuperseded() => TransitionTo(ResearchReportStatus.Superseded);
    public void MarkCorrected() => TransitionTo(ResearchReportStatus.Corrected);
    public void MarkRetracted() => TransitionTo(ResearchReportStatus.Retracted);
    private void TransitionTo(ResearchReportStatus status)
    {
        if (Status != ResearchReportStatus.Published) throw new InvalidOperationException("A report disposition can only be recorded once.");
        Status = status;
    }
}

public sealed record ResearchReportState(ResearchReportId Id, string ReportSeriesId, int VersionNumber,
    ResearchRequestId ResearchRequestId, string Subject, string Question, ResearchVisibility Visibility,
    DateTimeOffset DataCutoff, DateTimeOffset GeneratedAt, DateTimeOffset ExpiresAt,
    ResearchReportId? SupersedesReportId, string Content, string ContentHash, ReportProvenance Provenance,
    GeneratorMetadata GeneratorMetadata, ResearchReportStatus Status);

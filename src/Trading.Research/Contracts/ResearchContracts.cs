using Trading.Core.Identifiers;
using Trading.Core.Research;

namespace Trading.Research.Contracts;

public static class ResearchResultCodes
{
    public const string Success = "research.success";
    public const string InvalidRequest = "research.validation.invalid_request";
    public const string QuestionUnbounded = "research.validation.question_unbounded";
    public const string Unauthorized = "research.authorization.denied";
    public const string VisibilityConflict = "research.authorization.visibility_conflict";
    public const string UnknownTool = "research.tool.unknown";
    public const string ToolSchemaInvalid = "research.tool.schema_invalid";
    public const string ToolBudgetExceeded = "research.tool.budget_exceeded";
    public const string PublicationInvalid = "research.publication.invalid_draft";
    public const string CitationInvalid = "research.publication.invalid_citation";
    public const string TimedOut = "research.terminal.timed_out";
    public const string BudgetExceeded = "research.terminal.budget_exceeded";
    public const string Cancelled = "research.terminal.cancelled";
    public const string ProviderFailed = "research.terminal.provider_failed";
    public const string RecoveryExpiredLease = "research.recovery.expired_lease";
}

public sealed record ResearchToolDefinition(string Name, int SchemaVersion, string CanonicalJsonSchema);
public sealed record ResearchToolCall(string CallId, string Name, int SchemaVersion, string CanonicalArguments);
public sealed record ResearchAssistantResponse(string? Narrative, IReadOnlyList<ResearchToolCall> ToolCalls, long Tokens, decimal Cost);
public sealed record ResearchModelRequest(ResearchRunAttemptId AttemptId, string Instructions, ResearchVersionPins Versions, IReadOnlyList<ResearchToolDefinition> Tools);

public interface IResearchModelSession { Task<ResearchAssistantResponse> CompleteAsync(ResearchModelRequest request, CancellationToken cancellationToken); }
public interface IResearchClock { DateTimeOffset UtcNow { get; } }
public interface IResearchIdentifierSource { ResearchRequestId NewRequestId(); ResearchRunAttemptId NewAttemptId(); ResearchReportId NewReportId(); ResearchSubscriptionId NewSubscriptionId(); }

public sealed record ResearchSourceQuery(string Provider, string Query, DateTimeOffset AsOf);
public sealed record ResearchSourceResult(string Provider, string SourceIdentifier, DateTimeOffset? PublishedAt, DateTimeOffset RetrievedAt,
    string ContentHash, string UntrustedContent, string? License, string? RetentionPolicy);
public interface IResearchSource { string Provider { get; } Task<IReadOnlyList<ResearchSourceResult>> QueryAsync(ResearchSourceQuery query, CancellationToken cancellationToken); }

public sealed record ResearchToolResult(string CallId, bool Succeeded, string ResultCode, string CanonicalPayload, ResearchUsage Usage);
public interface IResearchToolDispatcher { IReadOnlyList<ResearchToolDefinition> Definitions { get; } Task<ResearchToolResult> DispatchAsync(ResearchRunAttempt attempt, ResearchPrincipal principal, ResearchToolCall toolCall, CancellationToken cancellationToken); }

public sealed record ResearchReportDraft(string CanonicalContent, IReadOnlyList<SourceCitation> Citations, DateTimeOffset DataCutoff, DateTimeOffset? RecommendedRefreshAt);
public sealed record DraftValidationResult(bool IsValid, string ResultCode, IReadOnlyList<string> Errors);
public interface IResearchDraftValidator { DraftValidationResult Validate(ResearchReportDraft draft, ResearchRunAttempt attempt); }
public interface IResearchReportPublisher { Task<ResearchReport> PublishAsync(ResearchRequest request, ResearchRunAttempt attempt, ResearchReportDraft draft, CancellationToken cancellationToken); }

public sealed record ResearchCatalogQuery(ResearchPrincipal Principal, string? Subject, string? NormalizedKey, ResearchReportId? ExactReportId, DateTimeOffset At);
public sealed record ResearchCatalogEntry(ResearchReportId ReportId, string SeriesId, int Version, string Subject, ResearchReportStatus Status, DateTimeOffset DataCutoff, DateTimeOffset GeneratedAt, DateTimeOffset ExpiresAt, bool IsFresh);
public interface IResearchReportCatalog { Task<IReadOnlyList<ResearchCatalogEntry>> ListAsync(ResearchCatalogQuery query, CancellationToken cancellationToken); Task<ResearchReport?> GetAsync(ResearchPrincipal principal, ResearchReportId exactReportId, CancellationToken cancellationToken); }

public sealed record ResearchNotification(ResearchSubscriptionId SubscriptionId, ResearchRequestId RequestId, ResearchReportId? ReportId, string ResultCode, DateTimeOffset CreatedAt);
public interface IResearchNotificationSink { Task DeliverAsync(ResearchNotification notification, CancellationToken cancellationToken); }

public interface IResearchRequestStore { Task<ResearchRequest?> GetAsync(ResearchRequestId id, CancellationToken cancellationToken); Task SaveAsync(ResearchRequest request, CancellationToken cancellationToken); }
public interface IResearchAttemptStore { Task<ResearchRunAttempt?> GetAsync(ResearchRunAttemptId id, CancellationToken cancellationToken); Task SaveAsync(ResearchRunAttempt attempt, CancellationToken cancellationToken); }
public interface IResearchArtifactStore { Task WriteDraftAsync(ResearchRunAttemptId attemptId, ResearchReportDraft draft, CancellationToken cancellationToken); }

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Research.Contracts;

namespace Trading.Research;

public sealed record ResearchRequestCommand(
    ResearchPrincipal Principal,
    TradingBotId RequestingBotId,
    string Subject,
    string Question,
    IReadOnlyCollection<string> DesiredSections,
    IReadOnlyCollection<string> RequiredSourceTypes,
    DateTimeOffset AsOf,
    ResearchVisibility Visibility,
    string? RestrictedGroup,
    string? PrivateInputHash,
    TimeSpan MaximumAge,
    string MethodologyVersion,
    string ReportSchemaVersion,
    ResearchBudget Budget,
    IReadOnlyCollection<string> ApprovedSourceProviders,
    ResearchReportId? RefreshReportId = null);

public enum ResearchRequestDecision { ReusedReport, Subscribed, Queued, Rejected }

public static class ResearchRequestCodes
{
    public const string Reused = "research.request.reused";
    public const string Subscribed = "research.request.subscribed";
    public const string Queued = "research.request.queued";
    public const string Invalid = "research.request.invalid";
    public const string Unauthorized = "research.request.unauthorized";
    public const string SourcePolicyDenied = "research.request.source_policy_denied";
    public const string RefreshUnauthorized = "research.request.refresh_unauthorized";
}

public sealed record ResearchRequestResult(ResearchRequestDecision Decision, string Code,
    string? NormalizedKey = null, ResearchRequestId? RequestId = null,
    ResearchSubscriptionId? SubscriptionId = null, ResearchReportId? ReportId = null);

public sealed class ResearchRequestService(
    IResearchRequestDecisionRepository store,
    IResearchIdentifierSource identifiers,
    IResearchClock clock)
{
    private const int MaximumQuestionLength = 4_000;
    private const int MaximumItems = 20;

    public async Task<ResearchRequestResult> SubmitAsync(ResearchRequestCommand command, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.UtcNow;
        if (!Valid(command, now)) return Reject(ResearchRequestCodes.Invalid);
        if (!Authorized(command)) return Reject(ResearchRequestCodes.Unauthorized);

        var sources = NormalizeSet(command.RequiredSourceTypes);
        var providers = NormalizeSet(command.ApprovedSourceProviders);
        if (sources.Any(source => !providers.Contains(source, StringComparer.Ordinal)))
            return Reject(ResearchRequestCodes.SourcePolicyDenied);

        var subject = NormalizeText(command.Subject, upper: true);
        var question = NormalizeText(command.Question);
        var sections = NormalizeSet(command.DesiredSections);
        var visibilityOwner = command.Visibility == ResearchVisibility.BotPrivate ? command.RequestingBotId.ToString() : null;
        var specification = new CanonicalRequest(subject, question, sections, sources,
            command.AsOf.ToUniversalTime().ToString("O"), command.MaximumAge.Ticks,
            NormalizeText(command.MethodologyVersion), command.Visibility.ToString(), visibilityOwner,
            command.RestrictedGroup is null ? null : NormalizeText(command.RestrictedGroup),
            command.PrivateInputHash?.ToLowerInvariant(), NormalizeText(command.ReportSchemaVersion));
        var json = JsonSerializer.Serialize(specification);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var request = new ResearchRequest(identifiers.NewRequestId(), command.RequestingBotId, subject, question,
            command.AsOf, command.Visibility, new DataFreshness(command.AsOf, now, command.MaximumAge), key, now,
            [command.RequestingBotId], command.RestrictedGroup);
        if (command.PrivateInputHash is not null) request.RecordPrivateInputs();
        request.BeginValidation(); request.Queue();
        var subscriptionId = identifiers.NewSubscriptionId();
        request.Subscribe(subscriptionId, command.RequestingBotId, now);
        var decision = await store.DecideAsync(new AuthorizedResearchRequest(request, subscriptionId, json,
            command.RefreshReportId), command.Principal, now, token).ConfigureAwait(false);
        return decision switch
        {
            ResearchRequestPersistenceDecision.Reused x => new(ResearchRequestDecision.ReusedReport, ResearchRequestCodes.Reused, key, ReportId: x.ReportId),
            ResearchRequestPersistenceDecision.Subscribed x => new(ResearchRequestDecision.Subscribed, ResearchRequestCodes.Subscribed, key, x.RequestId, x.SubscriptionId),
            ResearchRequestPersistenceDecision.Queued x => new(ResearchRequestDecision.Queued, ResearchRequestCodes.Queued, key, x.RequestId, x.SubscriptionId),
            ResearchRequestPersistenceDecision.RefreshUnauthorized => Reject(ResearchRequestCodes.RefreshUnauthorized),
            _ => throw new InvalidOperationException("Unknown persistence decision."),
        };
    }

    private static bool Authorized(ResearchRequestCommand command) => command.Principal.Kind switch
    {
        ResearchPrincipalKind.Administrator => true,
        ResearchPrincipalKind.TradingBot => command.Principal.Id == command.RequestingBotId.ToString() &&
            (command.Visibility != ResearchVisibility.Restricted || command.RestrictedGroup is not null &&
             command.Principal.RestrictedGroups.Contains(command.RestrictedGroup, StringComparer.Ordinal)),
        _ => false,
    };

    private static bool Valid(ResearchRequestCommand x, DateTimeOffset now) =>
        x.Principal is not null && x.RequestingBotId is not null && x.Budget is not null &&
        x.DesiredSections is not null && x.RequiredSourceTypes is not null && x.ApprovedSourceProviders is not null &&
        x.AsOf.Offset == TimeSpan.Zero && x.AsOf <= now && x.MaximumAge > TimeSpan.Zero &&
        x.MaximumAge <= TimeSpan.FromDays(365) && !string.IsNullOrWhiteSpace(x.Subject) && x.Subject.Length <= 300 &&
        !string.IsNullOrWhiteSpace(x.Question) && x.Question.Length <= MaximumQuestionLength &&
        x.Question.Any(char.IsWhiteSpace) && x.DesiredSections.Count is > 0 and <= MaximumItems &&
        x.RequiredSourceTypes.Count is > 0 and <= MaximumItems && x.ApprovedSourceProviders.Count <= MaximumItems &&
        x.DesiredSections.All(ValidSetValue) && x.RequiredSourceTypes.All(ValidSetValue) && x.ApprovedSourceProviders.All(ValidSetValue) &&
        !string.IsNullOrWhiteSpace(x.MethodologyVersion) && !string.IsNullOrWhiteSpace(x.ReportSchemaVersion) &&
        x.Budget.WallClock > TimeSpan.Zero && x.Budget.TokenLimit > 0 && x.Budget.CostLimit.Amount >= 0 &&
        x.Budget.ToolCallLimit > 0 && x.Budget.DocumentLimit > 0 && x.Budget.RetainedByteLimit > 0 &&
        (x.PrivateInputHash is null || x.PrivateInputHash.Length == 64 && x.PrivateInputHash.All(Uri.IsHexDigit)) &&
        (x.Visibility == ResearchVisibility.Restricted) == !string.IsNullOrWhiteSpace(x.RestrictedGroup) &&
        (x.PrivateInputHash is null || x.Visibility != ResearchVisibility.Shared);

    private static bool ValidSetValue(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200;

    private static string NormalizeText(string value, bool upper = false)
    {
        var normalized = string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return upper ? normalized.ToUpperInvariant() : normalized.ToLowerInvariant();
    }
    private static string[] NormalizeSet(IEnumerable<string> values) => values.Select(x => NormalizeText(x))
        .Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static ResearchRequestResult Reject(string code) => new(ResearchRequestDecision.Rejected, code);
    private sealed record CanonicalRequest(string Subject, string Question, string[] Sections, string[] Sources,
        string AsOf, long MaximumAgeTicks, string Methodology, string Visibility, string? Owner,
        string? RestrictedGroup, string? PrivateInputHash, string ReportSchemaVersion);
}

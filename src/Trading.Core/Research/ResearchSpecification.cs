using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Research;

public enum ResearchPrincipalKind { TradingBot, User, Administrator }

public sealed record ResearchPrincipal
{
    public ResearchPrincipal(string id, ResearchPrincipalKind kind, IEnumerable<string>? restrictedGroups = null)
    {
        Id = ResearchValidation.Required(id, nameof(id), 200); Kind = kind;
        var groups = restrictedGroups?.Select(x => ResearchValidation.Required(x, nameof(restrictedGroups), 200)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];
        RestrictedGroups = Array.AsReadOnly(groups);
    }
    public string Id { get; }
    public ResearchPrincipalKind Kind { get; }
    public IReadOnlyList<string> RestrictedGroups { get; }
}

public sealed record ResearchAccessScope
{
    public ResearchAccessScope(ResearchVisibility visibility, TradingBotId ownerBotId, string? restrictedGroup = null)
    {
        Visibility = visibility; OwnerBotId = ownerBotId ?? throw new ArgumentNullException(nameof(ownerBotId));
        RestrictedGroup = visibility == ResearchVisibility.Restricted
            ? ResearchValidation.Required(restrictedGroup, nameof(restrictedGroup), 200)
            : restrictedGroup is null ? null : throw new ArgumentException("Only restricted visibility names a group.", nameof(restrictedGroup));
    }
    public ResearchVisibility Visibility { get; }
    public TradingBotId OwnerBotId { get; }
    public string? RestrictedGroup { get; }
    public bool Authorizes(ResearchPrincipal principal) => principal.Kind == ResearchPrincipalKind.Administrator || Visibility switch
    {
        ResearchVisibility.Shared => principal.Kind == ResearchPrincipalKind.TradingBot,
        ResearchVisibility.BotPrivate => principal.Kind == ResearchPrincipalKind.TradingBot && string.Equals(principal.Id, OwnerBotId.ToString(), StringComparison.Ordinal),
        ResearchVisibility.Restricted => principal.RestrictedGroups.Contains(RestrictedGroup!, StringComparer.Ordinal),
        _ => false,
    };
    public bool CanNarrowTo(ResearchAccessScope next) => next.OwnerBotId == OwnerBotId && (int)next.Visibility >= (int)Visibility;
}

public sealed record NormalizedResearchSpecification
{
    private readonly string[] desiredSections;
    private readonly string[] requiredSourceTypes;
    public NormalizedResearchSpecification(string subject, string question, DateTimeOffset asOf, IEnumerable<string> desiredSections,
        IEnumerable<string> requiredSourceTypes, DataFreshness freshnessRequirement, ResearchAccessScope access, string reportSchemaVersion,
        bool containsPrivateInputs, string? privateInputFingerprint = null)
    {
        Subject = Normalize(subject, nameof(subject), 300); Question = Normalize(question, nameof(question), 2000);
        AsOf = ResearchValidation.Utc(asOf, nameof(asOf));
        this.desiredSections = NormalizeSet(desiredSections, nameof(desiredSections));
        this.requiredSourceTypes = NormalizeSet(requiredSourceTypes, nameof(requiredSourceTypes));
        if (this.desiredSections.Length == 0) throw new ArgumentException("At least one desired section is required.", nameof(desiredSections));
        FreshnessRequirement = freshnessRequirement ?? throw new ArgumentNullException(nameof(freshnessRequirement));
        Access = access ?? throw new ArgumentNullException(nameof(access));
        ReportSchemaVersion = ResearchValidation.Required(reportSchemaVersion, nameof(reportSchemaVersion), 200);
        ContainsPrivateInputs = containsPrivateInputs;
        if (containsPrivateInputs && access.Visibility == ResearchVisibility.Shared)
            throw new ArgumentException("Private inputs require narrowed visibility.", nameof(access));
        PrivateInputFingerprint = containsPrivateInputs
            ? ResearchValidation.Required(privateInputFingerprint, nameof(privateInputFingerprint), 256)
            : privateInputFingerprint is null ? null : throw new ArgumentException("A fingerprint is valid only for private inputs.", nameof(privateInputFingerprint));
    }
    public string Subject { get; }
    public string Question { get; }
    public DateTimeOffset AsOf { get; }
    public IReadOnlyList<string> DesiredSections => Array.AsReadOnly(desiredSections);
    public IReadOnlyList<string> RequiredSourceTypes => Array.AsReadOnly(requiredSourceTypes);
    public DataFreshness FreshnessRequirement { get; }
    public ResearchAccessScope Access { get; }
    public string ReportSchemaVersion { get; }
    public bool ContainsPrivateInputs { get; }
    public string? PrivateInputFingerprint { get; }
    public string DeterministicKey => string.Join('|', Subject, Question, AsOf.ToString("O"), string.Join(',', desiredSections), string.Join(',', requiredSourceTypes),
        Access.Visibility, Access.Visibility == ResearchVisibility.BotPrivate ? Access.OwnerBotId : "", Access.RestrictedGroup ?? "", ReportSchemaVersion, PrivateInputFingerprint ?? "");
    private static string Normalize(string value, string name, int max) => string.Join(' ', ResearchValidation.Required(value, name, max).ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string[] NormalizeSet(IEnumerable<string> values, string name) { ArgumentNullException.ThrowIfNull(values); return values.Select(x => Normalize(x, name, 200)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(); }
}

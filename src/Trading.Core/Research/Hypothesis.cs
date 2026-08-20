using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Research;

public enum HypothesisStatus { Draft, Frozen, Testing, Validated, Rejected, Retired }

public sealed class Hypothesis
{
    private readonly List<HypothesisVersion> _versions = [];
    public Hypothesis(HypothesisId id, string name, DateTimeOffset createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = ResearchValidation.Required(name, nameof(name), 300);
        CreatedAt = ResearchValidation.Utc(createdAt, nameof(createdAt));
    }
    private Hypothesis(HypothesisState state)
    {
        Id = state.Id; Name = ResearchValidation.Required(state.Name, nameof(state.Name), 300);
        CreatedAt = ResearchValidation.Utc(state.CreatedAt, nameof(state.CreatedAt));
        Status = state.Status; CurrentVersionId = state.CurrentVersionId; Version = state.Version;
        _versions.AddRange(state.Versions.Select(HypothesisVersion.Rehydrate).OrderBy(x => x.VersionNumber));
    }
    public static Hypothesis Rehydrate(HypothesisState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var value = new Hypothesis(state);
        if (value._versions.Select(x => x.VersionNumber).Where((number, index) => number != index + 1).Any())
            throw new ArgumentException("Hypothesis version numbers must be contiguous.", nameof(state));
        if (value.CurrentVersionId is not null && value._versions.All(x => x.Id != value.CurrentVersionId))
            throw new ArgumentException("Current version must belong to the hypothesis.", nameof(state));
        return value;
    }
    public HypothesisId Id { get; }
    public string Name { get; }
    public HypothesisStatus Status { get; private set; } = HypothesisStatus.Draft;
    public HypothesisVersionId? CurrentVersionId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public long Version { get; private set; }
    public IReadOnlyList<HypothesisVersion> Versions => _versions.AsReadOnly();

    public HypothesisVersion AddVersion(HypothesisVersionId id, string claim, UniverseDefinition universe,
        string inputDefinitions, string signalRules, string evaluationPlan, string successCriteria,
        string invalidationCriteria, IEnumerable<ResearchReportId> evidenceReportIds, DateTimeOffset createdAt)
    {
        if (Status == HypothesisStatus.Testing) throw new InvalidOperationException("A hypothesis under test cannot change.");
        if (Status is HypothesisStatus.Validated or HypothesisStatus.Rejected or HypothesisStatus.Retired)
            throw new InvalidOperationException("A terminal hypothesis cannot change.");
        if (_versions.Any(version => version.Id == id)) throw new InvalidOperationException("Version identity already exists.");
        var version = new HypothesisVersion(id, _versions.Count + 1, claim, universe, inputDefinitions, signalRules,
            evaluationPlan, successCriteria, invalidationCriteria, evidenceReportIds, createdAt);
        _versions.Add(version);
        CurrentVersionId = version.Id;
        Status = HypothesisStatus.Draft;
        Version++;
        return version;
    }

    public void FreezeCurrent(DateTimeOffset frozenAt)
    {
        var version = Current();
        if (Status != HypothesisStatus.Draft) throw new InvalidOperationException("Only a draft can be frozen.");
        version.Freeze(frozenAt);
        Status = HypothesisStatus.Frozen; Version++;
    }
    public void StartTesting() { if (Status != HypothesisStatus.Frozen) throw new InvalidOperationException("Only a frozen hypothesis can be tested."); Status = HypothesisStatus.Testing; Version++; }
    public void Validate() { if (Status != HypothesisStatus.Testing) throw new InvalidOperationException("Only a tested hypothesis can be validated."); Status = HypothesisStatus.Validated; Version++; }
    public void Reject() { if (Status != HypothesisStatus.Testing) throw new InvalidOperationException("Only a tested hypothesis can be rejected."); Status = HypothesisStatus.Rejected; Version++; }
    public void Retire() { if (Status != HypothesisStatus.Validated) throw new InvalidOperationException("Only a validated hypothesis can be retired."); Status = HypothesisStatus.Retired; Version++; }
    private HypothesisVersion Current() => _versions.SingleOrDefault(version => version.Id == CurrentVersionId)
        ?? throw new InvalidOperationException("A hypothesis version is required.");
}

public sealed class HypothesisVersion
{
    private readonly ResearchReportId[] _evidenceReportIds;
    internal HypothesisVersion(HypothesisVersionId id, int number, string claim, UniverseDefinition universe,
        string inputs, string signals, string plan, string success, string invalidation,
        IEnumerable<ResearchReportId> evidence, DateTimeOffset createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)); VersionNumber = number;
        Claim = ResearchValidation.Required(claim, nameof(claim));
        UniverseDefinition = universe ?? throw new ArgumentNullException(nameof(universe));
        InputDefinitions = ResearchValidation.Required(inputs, nameof(inputs));
        SignalRules = ResearchValidation.Required(signals, nameof(signals));
        EvaluationPlan = ResearchValidation.Required(plan, nameof(plan));
        SuccessCriteria = ResearchValidation.Required(success, nameof(success));
        InvalidationCriteria = ResearchValidation.Required(invalidation, nameof(invalidation));
        ArgumentNullException.ThrowIfNull(evidence);
        _evidenceReportIds = evidence.ToArray();
        if (_evidenceReportIds.Any(item => item is null)) throw new ArgumentException("Evidence cannot contain null.", nameof(evidence));
        CreatedAt = ResearchValidation.Utc(createdAt, nameof(createdAt));
    }
    public HypothesisVersionId Id { get; }
    public int VersionNumber { get; }
    public string Claim { get; }
    public UniverseDefinition UniverseDefinition { get; }
    public string InputDefinitions { get; }
    public string SignalRules { get; }
    public string EvaluationPlan { get; }
    public string SuccessCriteria { get; }
    public string InvalidationCriteria { get; }
    public IReadOnlyList<ResearchReportId> EvidenceReportIds => Array.AsReadOnly(_evidenceReportIds);
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? FrozenAt { get; private set; }
    public bool IsFrozen => FrozenAt is not null;
    internal void Freeze(DateTimeOffset at)
    {
        ResearchValidation.Utc(at, nameof(at));
        if (at < CreatedAt) throw new ArgumentException("Freeze cannot precede creation.", nameof(at));
        if (IsFrozen) throw new InvalidOperationException("Version is already frozen.");
        FrozenAt = at;
    }
    public static HypothesisVersion Rehydrate(HypothesisVersionState state)
    {
        var value = new HypothesisVersion(state.Id, state.VersionNumber, state.Claim, state.UniverseDefinition,
            state.InputDefinitions, state.SignalRules, state.EvaluationPlan, state.SuccessCriteria,
            state.InvalidationCriteria, state.EvidenceReportIds, state.CreatedAt);
        if (state.FrozenAt is not null) value.Freeze(state.FrozenAt.Value);
        return value;
    }
}

public sealed record HypothesisState(HypothesisId Id, string Name, HypothesisStatus Status,
    HypothesisVersionId? CurrentVersionId, DateTimeOffset CreatedAt, long Version,
    IReadOnlyList<HypothesisVersionState> Versions);
public sealed record HypothesisVersionState(HypothesisVersionId Id, int VersionNumber, string Claim,
    UniverseDefinition UniverseDefinition, string InputDefinitions, string SignalRules, string EvaluationPlan,
    string SuccessCriteria, string InvalidationCriteria, IReadOnlyList<ResearchReportId> EvidenceReportIds,
    DateTimeOffset CreatedAt, DateTimeOffset? FrozenAt);

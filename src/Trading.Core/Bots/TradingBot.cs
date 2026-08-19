using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Bots;

public enum TradingBotStatus { Enabled, Paused }
public enum ExecutionMode { ResearchOnly, HumanApproval, PaperTrading, LiveTrading }

public sealed class TradingBot
{
    private readonly List<TradingBotConfigurationVersion> _configurations = [];

    public TradingBot(TradingBotId id, string name, DateTimeOffset createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = BotValidation.Required(name, nameof(name));
        CreatedAt = BotValidation.Utc(createdAt, nameof(createdAt));
        UpdatedAt = CreatedAt;
        Status = TradingBotStatus.Paused;
    }

    public TradingBotId Id { get; }
    public string Name { get; }
    public TradingBotStatus Status { get; private set; }
    public PortfolioId? PortfolioId { get; private set; }
    public TradingBotConfigurationVersionId? ActiveConfigurationVersionId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<TradingBotConfigurationVersion> ConfigurationVersions => _configurations.AsReadOnly();

    public TradingBotConfigurationVersion AddConfiguration(
        TradingBotConfigurationVersionId id,
        InvestmentMandate investmentMandate,
        RiskPolicy riskPolicy,
        ToolPolicy toolPolicy,
        RunBudget runBudget,
        SchedulingPolicy schedulingPolicy,
        ExecutionMode executionMode,
        ModelConfiguration modelConfiguration,
        string promptVersion,
        DateTimeOffset createdAt)
    {
        if (executionMode == ExecutionMode.LiveTrading)
        {
            throw new InvalidOperationException("Live trading requires explicit promotion.");
        }

        return AddVersion(id, investmentMandate, riskPolicy, toolPolicy, runBudget, schedulingPolicy,
            executionMode, modelConfiguration, promptVersion, createdAt);
    }

    public TradingBotConfigurationVersion PromoteToLive(
        TradingBotConfigurationVersionId id,
        TradingBotConfigurationVersionId sourceVersionId,
        DateTimeOffset createdAt)
    {
        var source = FindConfiguration(sourceVersionId);
        return AddVersion(id, source.InvestmentMandate, source.RiskPolicy, source.ToolPolicy, source.RunBudget,
            source.SchedulingPolicy, ExecutionMode.LiveTrading, source.ModelConfiguration, source.PromptVersion, createdAt);
    }

    public void ActivateConfiguration(TradingBotConfigurationVersionId id, DateTimeOffset activatedAt)
    {
        var next = FindConfiguration(id);
        BotValidation.Utc(activatedAt, nameof(activatedAt));
        if (activatedAt < next.CreatedAt)
        {
            throw new ArgumentException("Activation cannot precede configuration creation.", nameof(activatedAt));
        }

        if (ActiveConfigurationVersionId == id)
        {
            return;
        }

        var active = _configurations.SingleOrDefault(configuration => configuration.IsActive);
        active?.Supersede(activatedAt);
        next.Activate(activatedAt);
        ActiveConfigurationVersionId = next.Id;
        UpdatedAt = activatedAt;
    }

    public void AssignPortfolio(PortfolioId portfolioId, DateTimeOffset changedAt)
    {
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        Touch(changedAt);
    }

    public void Enable(DateTimeOffset changedAt)
    {
        if (ActiveConfigurationVersionId is null || PortfolioId is null)
        {
            throw new InvalidOperationException("A bot requires an active configuration and assigned portfolio before it can be enabled.");
        }

        Status = TradingBotStatus.Enabled;
        Touch(changedAt);
    }

    public void Pause(DateTimeOffset changedAt)
    {
        Status = TradingBotStatus.Paused;
        Touch(changedAt);
    }

    public bool CanRun => Status == TradingBotStatus.Enabled && ActiveConfigurationVersionId is not null && PortfolioId is not null;

    private TradingBotConfigurationVersion AddVersion(TradingBotConfigurationVersionId id, InvestmentMandate mandate,
        RiskPolicy risk, ToolPolicy tools, RunBudget budget, SchedulingPolicy schedule, ExecutionMode mode,
        ModelConfiguration model, string promptVersion, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(mandate);
        ArgumentNullException.ThrowIfNull(risk);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(model);
        BotValidation.Utc(createdAt, nameof(createdAt));
        if (_configurations.Any(configuration => configuration.Id == id))
        {
            throw new InvalidOperationException("Configuration identity already exists.");
        }

        var version = new TradingBotConfigurationVersion(id, _configurations.Count + 1, mandate, risk, tools,
            budget, schedule, mode, model, promptVersion, createdAt);
        _configurations.Add(version);
        Touch(createdAt);
        return version;
    }

    private TradingBotConfigurationVersion FindConfiguration(TradingBotConfigurationVersionId id) =>
        _configurations.SingleOrDefault(configuration => configuration.Id == id)
        ?? throw new InvalidOperationException("Configuration does not belong to this bot.");

    private void Touch(DateTimeOffset changedAt)
    {
        BotValidation.Utc(changedAt, nameof(changedAt));
        if (changedAt < UpdatedAt) throw new ArgumentException("Change time cannot move backwards.", nameof(changedAt));
        UpdatedAt = changedAt;
    }
}

public sealed class TradingBotConfigurationVersion
{
    internal TradingBotConfigurationVersion(TradingBotConfigurationVersionId id, int versionNumber,
        InvestmentMandate mandate, RiskPolicy risk, ToolPolicy tools, RunBudget budget, SchedulingPolicy schedule,
        ExecutionMode mode, ModelConfiguration model, string promptVersion, DateTimeOffset createdAt)
    {
        Id = id; VersionNumber = versionNumber; InvestmentMandate = mandate; RiskPolicy = risk; ToolPolicy = tools;
        RunBudget = budget; SchedulingPolicy = schedule; ExecutionMode = mode; ModelConfiguration = model;
        PromptVersion = BotValidation.Required(promptVersion, nameof(promptVersion)); CreatedAt = createdAt;
    }

    public TradingBotConfigurationVersionId Id { get; }
    public int VersionNumber { get; }
    public InvestmentMandate InvestmentMandate { get; }
    public RiskPolicy RiskPolicy { get; }
    public ToolPolicy ToolPolicy { get; }
    public RunBudget RunBudget { get; }
    public SchedulingPolicy SchedulingPolicy { get; }
    public ExecutionMode ExecutionMode { get; }
    public ModelConfiguration ModelConfiguration { get; }
    public string PromptVersion { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public bool IsActive => ActivatedAt is not null && SupersededAt is null;

    internal void Activate(DateTimeOffset at)
    {
        if (ActivatedAt is not null || SupersededAt is not null) throw new InvalidOperationException("A historical configuration cannot be reactivated.");
        ActivatedAt = at;
    }

    internal void Supersede(DateTimeOffset at)
    {
        if (!IsActive) throw new InvalidOperationException("Only an active configuration can be superseded.");
        if (at < ActivatedAt) throw new ArgumentException("Supersession cannot precede activation.", nameof(at));
        SupersededAt = at;
    }
}

internal static class BotValidation
{
    public static string Required(string? value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        var trimmed = value.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Value is required.", name);
        return trimmed;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be expressed in UTC.", name);
        return value;
    }
}

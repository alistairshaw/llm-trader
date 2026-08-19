using Trading.Core.FinancialValues;

namespace Trading.Core.Policies;

public sealed record RunBudget
{
    public RunBudget(
        TimeSpan wallClock,
        long tokenLimit,
        Money costLimit,
        int toolCallLimit,
        int researchRequestLimit,
        int proposalLimit)
    {
        if (wallClock <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(wallClock), wallClock, "Wall-clock budget must be positive.");
        }

        ArgumentNullException.ThrowIfNull(costLimit);
        if (tokenLimit < 0 || costLimit.Amount < 0m || toolCallLimit < 0 || researchRequestLimit < 0 || proposalLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenLimit), "Budget limits cannot be negative.");
        }

        WallClock = wallClock;
        TokenLimit = tokenLimit;
        CostLimit = costLimit;
        ToolCallLimit = toolCallLimit;
        ResearchRequestLimit = researchRequestLimit;
        ProposalLimit = proposalLimit;
    }

    public TimeSpan WallClock { get; }
    public long TokenLimit { get; }
    public Money CostLimit { get; }
    public int ToolCallLimit { get; }
    public int ResearchRequestLimit { get; }
    public int ProposalLimit { get; }
}

public sealed record Usage
{
    public Usage(TimeSpan elapsed, long tokens, Money cost, int toolCalls, int researchRequests, int proposals)
    {
        ArgumentNullException.ThrowIfNull(cost);
        if (elapsed < TimeSpan.Zero || tokens < 0 || cost.Amount < 0m || toolCalls < 0 || researchRequests < 0 || proposals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokens), "Usage values cannot be negative.");
        }

        Elapsed = elapsed;
        Tokens = tokens;
        Cost = cost;
        ToolCalls = toolCalls;
        ResearchRequests = researchRequests;
        Proposals = proposals;
    }

    public TimeSpan Elapsed { get; }
    public long Tokens { get; }
    public Money Cost { get; }
    public int ToolCalls { get; }
    public int ResearchRequests { get; }
    public int Proposals { get; }
}

public sealed record ToolAllowance
{
    public ToolAllowance(string toolName, int callLimit)
    {
        ToolName = PolicyValidation.Required(toolName, nameof(toolName));
        if (callLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callLimit), callLimit, "Per-tool call limit must be positive.");
        }

        CallLimit = callLimit;
    }

    public string ToolName { get; }
    public int CallLimit { get; }
}

public sealed record ToolPolicy
{
    private readonly ToolAllowance[] _allowedTools;

    public ToolPolicy(IEnumerable<ToolAllowance> allowedTools)
    {
        ArgumentNullException.ThrowIfNull(allowedTools);
        var materialized = allowedTools.ToArray();
        if (materialized.Any(tool => tool is null))
        {
            throw new ArgumentException("Tool allowances cannot contain null.", nameof(allowedTools));
        }

        _allowedTools = materialized.OrderBy(tool => tool.ToolName, StringComparer.Ordinal).ToArray();
        if (_allowedTools.GroupBy(tool => tool.ToolName, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A tool can have only one call limit.", nameof(allowedTools));
        }
    }

    public IReadOnlyList<ToolAllowance> AllowedTools => Array.AsReadOnly(_allowedTools);

    public bool IsAllowed(string toolName) =>
        _allowedTools.Any(tool => string.Equals(tool.ToolName, toolName, StringComparison.Ordinal));

    public int? GetCallLimit(string toolName) =>
        _allowedTools.FirstOrDefault(tool => string.Equals(tool.ToolName, toolName, StringComparison.Ordinal))?.CallLimit;

    public bool Equals(ToolPolicy? other) => other is not null && _allowedTools.SequenceEqual(other._allowedTools);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var tool in _allowedTools)
        {
            hash.Add(tool);
        }

        return hash.ToHashCode();
    }
}

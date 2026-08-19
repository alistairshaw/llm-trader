using Trading.Core.FinancialValues;

namespace Trading.Core.Policies;

public sealed record InvestmentMandate
{
    public InvestmentMandate(string objective, TimeSpan investmentHorizon, UniverseDefinition universe)
    {
        Objective = PolicyValidation.Required(objective, nameof(objective));
        if (investmentHorizon <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(investmentHorizon), investmentHorizon, "Investment horizon must be positive.");
        }

        ArgumentNullException.ThrowIfNull(universe);
        InvestmentHorizon = investmentHorizon;
        Universe = universe;
    }

    public string Objective { get; }

    public TimeSpan InvestmentHorizon { get; }

    public UniverseDefinition Universe { get; }
}

public sealed record RiskLimit
{
    public RiskLimit(string metric, decimal maximum, string unit, decimal minimum = 0m)
    {
        Metric = PolicyValidation.Required(metric, nameof(metric));
        Unit = PolicyValidation.Required(unit, nameof(unit));
        if (minimum < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "Minimum cannot be negative.");
        }

        if (maximum < minimum)
        {
            throw new ArgumentException("Maximum cannot be less than minimum.", nameof(maximum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public string Metric { get; }

    public decimal Minimum { get; }

    public decimal Maximum { get; }

    public string Unit { get; }
}

public sealed record RiskPolicy
{
    private readonly RiskLimit[] _limits;

    public RiskPolicy(IEnumerable<RiskLimit> limits, bool tradingHalted = false)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits.ToArray();
        if (_limits.Any(limit => limit is null))
        {
            throw new ArgumentException("Risk limits cannot contain null.", nameof(limits));
        }

        if (_limits.GroupBy(limit => limit.Metric, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A risk policy cannot contain contradictory duplicate metrics.", nameof(limits));
        }

        TradingHalted = tradingHalted;
    }

    public IReadOnlyList<RiskLimit> Limits => Array.AsReadOnly(_limits);

    public bool TradingHalted { get; }

    public bool Equals(RiskPolicy? other) =>
        other is not null && TradingHalted == other.TradingHalted && _limits.SequenceEqual(other._limits);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TradingHalted);
        foreach (var limit in _limits)
        {
            hash.Add(limit);
        }

        return hash.ToHashCode();
    }
}

public sealed record CashReservePolicy
{
    public CashReservePolicy(Percentage minimumPercentage, Money minimumAmount)
    {
        ArgumentNullException.ThrowIfNull(minimumPercentage);
        ArgumentNullException.ThrowIfNull(minimumAmount);
        if (minimumAmount.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAmount), minimumAmount, "Minimum reserve cannot be negative.");
        }

        MinimumPercentage = minimumPercentage;
        MinimumAmount = minimumAmount;
    }

    public Percentage MinimumPercentage { get; }

    public Money MinimumAmount { get; }
}

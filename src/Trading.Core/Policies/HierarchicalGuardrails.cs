using System.Collections.Frozen;
using System.Globalization;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;

namespace Trading.Core.Policies;

public static class GuardrailRuleIds
{
    public const string Authority = "guardrail.authority";
    public const string KillSwitch = "guardrail.kill_switch";
    public const string Mandate = "guardrail.mandate";
    public const string InstrumentEligibility = "guardrail.instrument_eligibility";
    public const string ProposalExpiry = "guardrail.proposal_expiry";
    public const string PositionNotional = "guardrail.position_notional";
    public const string Concentration = "guardrail.concentration";
    public const string AvailableCapital = "guardrail.available_capital";
    public const string PriceFreshness = "guardrail.price_freshness";
    public const string Liquidity = "guardrail.liquidity";
    public const string MarketHours = "guardrail.market_hours";
}

public static class GuardrailReasonCodes
{
    public const string Passed = "guardrail.passed";
    public const string Unauthorized = "guardrail.unauthorized";
    public const string Disabled = "guardrail.disabled";
    public const string OutsideMandate = "guardrail.outside_mandate";
    public const string InstrumentIneligible = "guardrail.instrument_ineligible";
    public const string ProposalExpired = "guardrail.proposal_expired";
    public const string PositionLimitExceeded = "guardrail.position_limit_exceeded";
    public const string ConcentrationLimitExceeded = "guardrail.concentration_limit_exceeded";
    public const string CapitalUnavailable = "guardrail.capital_unavailable";
    public const string PriceMissing = "guardrail.price_missing";
    public const string PriceStale = "guardrail.price_stale";
    public const string LiquidityUnknown = "guardrail.liquidity_unknown";
    public const string LiquidityInsufficient = "guardrail.liquidity_insufficient";
    public const string MarketStateUnknown = "guardrail.market_state_unknown";
    public const string MarketClosed = "guardrail.market_closed";
}

public sealed record GuardrailPolicy
{
    public GuardrailPolicy(GuardrailPolicyReference reference, bool enabled = true,
        IEnumerable<InstrumentId>? eligibleInstruments = null, Money? maximumPositionNotional = null,
        Percentage? maximumConcentration = null, Money? minimumAvailableCapital = null,
        TimeSpan? maximumPriceAge = null, Money? minimumDailyLiquidity = null, bool requireOpenMarket = false)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        if (maximumPositionNotional?.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPositionNotional));
        if (minimumAvailableCapital?.Amount < 0) throw new ArgumentOutOfRangeException(nameof(minimumAvailableCapital));
        if (maximumPriceAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumPriceAge));
        if (minimumDailyLiquidity?.Amount < 0) throw new ArgumentOutOfRangeException(nameof(minimumDailyLiquidity));
        Enabled = enabled;
        EligibleInstruments = eligibleInstruments is null ? null : new HashSet<InstrumentId>(eligibleInstruments).ToFrozenSet();
        MaximumPositionNotional = maximumPositionNotional;
        MaximumConcentration = maximumConcentration;
        MinimumAvailableCapital = minimumAvailableCapital;
        MaximumPriceAge = maximumPriceAge;
        MinimumDailyLiquidity = minimumDailyLiquidity;
        RequireOpenMarket = requireOpenMarket;
    }

    public GuardrailPolicyReference Reference { get; }
    public bool Enabled { get; }
    public IReadOnlySet<InstrumentId>? EligibleInstruments { get; }
    public Money? MaximumPositionNotional { get; }
    public Percentage? MaximumConcentration { get; }
    public Money? MinimumAvailableCapital { get; }
    public TimeSpan? MaximumPriceAge { get; }
    public Money? MinimumDailyLiquidity { get; }
    public bool RequireOpenMarket { get; }
}

public sealed record HierarchicalGuardrailPolicySet(
    GuardrailPolicy Platform, GuardrailPolicy Account, GuardrailPolicy Portfolio, GuardrailPolicy TradingBot)
{
    public IReadOnlyList<GuardrailPolicy> InEvaluationOrder => [Platform, Account, Portfolio, TradingBot];

    public IReadOnlyList<GuardrailPolicy> ComposeEffectivePolicies()
    {
        ValidateLevels();
        var result = new List<GuardrailPolicy>(4);
        GuardrailPolicy? parent = null;
        foreach (var policy in InEvaluationOrder)
        {
            parent = parent is null ? policy : Tighten(parent, policy);
            result.Add(parent);
        }
        return result.AsReadOnly();
    }

    private void ValidateLevels()
    {
        var expected = new[] { GuardrailPolicyLevel.Platform, GuardrailPolicyLevel.Account,
            GuardrailPolicyLevel.Portfolio, GuardrailPolicyLevel.TradingBot };
        if (!InEvaluationOrder.Select(x => x.Reference.Level).SequenceEqual(expected))
            throw new ArgumentException("Guardrail policies must be supplied in platform, account, Portfolio, and Trading Bot order.");
    }

    private static GuardrailPolicy Tighten(GuardrailPolicy parent, GuardrailPolicy child) => new(
        child.Reference, parent.Enabled && child.Enabled,
        Intersect(parent.EligibleInstruments, child.EligibleInstruments),
        Minimum(parent.MaximumPositionNotional, child.MaximumPositionNotional),
        Minimum(parent.MaximumConcentration, child.MaximumConcentration),
        Maximum(parent.MinimumAvailableCapital, child.MinimumAvailableCapital),
        Minimum(parent.MaximumPriceAge, child.MaximumPriceAge),
        Maximum(parent.MinimumDailyLiquidity, child.MinimumDailyLiquidity),
        parent.RequireOpenMarket || child.RequireOpenMarket);

    private static IReadOnlySet<InstrumentId>? Intersect(IReadOnlySet<InstrumentId>? parent, IReadOnlySet<InstrumentId>? child)
    {
        if (parent is null) return child;
        if (child is null) return parent;
        return parent.Intersect(child).ToFrozenSet();
    }

    private static T? Minimum<T>(T? parent, T? child) where T : class, IComparable<T> =>
        parent is null ? child : child is null || parent.CompareTo(child) <= 0 ? parent : child;
    private static TimeSpan? Minimum(TimeSpan? parent, TimeSpan? child) =>
        parent is null ? child : child is null || parent <= child ? parent : child;
    private static T? Maximum<T>(T? parent, T? child) where T : class, IComparable<T> =>
        parent is null ? child : child is null || parent.CompareTo(child) >= 0 ? parent : child;
}

public sealed record GuardrailState(
    DateTimeOffset EvaluatedAt, bool IdentityAuthorized, bool WithinMandate, Money ProposedNotional,
    Money ResultingPositionNotional, Percentage ResultingConcentration, Money AvailableCapital,
    DateTimeOffset? PriceObservedAt, Money? DailyLiquidity, bool? MarketOpen)
{
    public GuardrailState Validate()
    {
        if (EvaluatedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Evaluation time must be UTC.");
        if (PriceObservedAt.HasValue && PriceObservedAt.Value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Price time must be UTC.");
        if (ProposedNotional.Amount < 0 || ResultingPositionNotional.Amount < 0 || AvailableCapital.Amount < 0)
            throw new ArgumentOutOfRangeException(nameof(ProposedNotional), "Financial state cannot be negative.");
        return this;
    }
}

public sealed record HierarchicalGuardrailDecision(
    GuardrailOutcome Outcome, string Code, IReadOnlyList<GuardrailRuleResult> RuleResults,
    IReadOnlyList<GuardrailPolicyReference> EvaluatedPolicies);

public static class HierarchicalGuardrailEvaluator
{
    public static HierarchicalGuardrailDecision Evaluate(TradeProposal proposal,
        HierarchicalGuardrailPolicySet policies, GuardrailState state)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        var effective = policies.ComposeEffectivePolicies();
        var results = effective.SelectMany(policy => EvaluatePolicy(proposal, policy, state)).ToArray();
        var outcome = results.Any(x => x.Outcome == GuardrailOutcome.Failed) ? GuardrailOutcome.Failed : GuardrailOutcome.Passed;
        return new(outcome, outcome == GuardrailOutcome.Passed ? ProposalGovernanceCodes.Succeeded : ProposalGovernanceCodes.PolicyRejected,
            Array.AsReadOnly(results), effective.Select(x => x.Reference).ToArray());
    }

    private static IEnumerable<GuardrailRuleResult> EvaluatePolicy(TradeProposal proposal, GuardrailPolicy policy, GuardrailState state)
    {
        yield return Result(policy, GuardrailRuleIds.Authority, state.IdentityAuthorized, state.IdentityAuthorized.ToString(), "true", GuardrailReasonCodes.Unauthorized);
        yield return Result(policy, GuardrailRuleIds.KillSwitch, policy.Enabled, policy.Enabled.ToString(), "true", GuardrailReasonCodes.Disabled);
        yield return Result(policy, GuardrailRuleIds.Mandate, state.WithinMandate, state.WithinMandate.ToString(), "true", GuardrailReasonCodes.OutsideMandate);
        var eligible = policy.EligibleInstruments is null || policy.EligibleInstruments.Contains(proposal.InstrumentId);
        yield return Result(policy, GuardrailRuleIds.InstrumentEligibility, eligible, proposal.InstrumentId.ToString(),
            policy.EligibleInstruments is null ? "unrestricted" : string.Join(',', policy.EligibleInstruments.Select(x => x.ToString()).Order()), GuardrailReasonCodes.InstrumentIneligible);
        yield return Result(policy, GuardrailRuleIds.ProposalExpiry, state.EvaluatedAt < proposal.ValidUntil,
            Format(state.EvaluatedAt), Format(proposal.ValidUntil), GuardrailReasonCodes.ProposalExpired);
        yield return Limit(policy, GuardrailRuleIds.PositionNotional, state.ResultingPositionNotional, policy.MaximumPositionNotional,
            GuardrailReasonCodes.PositionLimitExceeded);
        yield return Limit(policy, GuardrailRuleIds.Concentration, state.ResultingConcentration, policy.MaximumConcentration,
            GuardrailReasonCodes.ConcentrationLimitExceeded);
        var requiredCapital = Add(state.ProposedNotional, policy.MinimumAvailableCapital);
        yield return Result(policy, GuardrailRuleIds.AvailableCapital, state.AvailableCapital >= requiredCapital,
            state.AvailableCapital.ToString(), requiredCapital.ToString(), GuardrailReasonCodes.CapitalUnavailable);
        var priceKnown = state.PriceObservedAt.HasValue;
        var priceAge = priceKnown ? state.EvaluatedAt - state.PriceObservedAt!.Value : (TimeSpan?)null;
        var priceFresh = priceKnown && priceAge >= TimeSpan.Zero &&
            (policy.MaximumPriceAge is null || priceAge <= policy.MaximumPriceAge);
        yield return Result(policy, GuardrailRuleIds.PriceFreshness, priceFresh,
            priceKnown ? Format(state.PriceObservedAt!.Value) : "missing", policy.MaximumPriceAge?.ToString() ?? "present", priceKnown ? GuardrailReasonCodes.PriceStale : GuardrailReasonCodes.PriceMissing);
        var liquidityKnown = state.DailyLiquidity is not null;
        var liquid = liquidityKnown && (policy.MinimumDailyLiquidity is null || state.DailyLiquidity! >= policy.MinimumDailyLiquidity);
        yield return Result(policy, GuardrailRuleIds.Liquidity, liquid, state.DailyLiquidity?.ToString() ?? "unknown",
            policy.MinimumDailyLiquidity?.ToString() ?? "known", liquidityKnown ? GuardrailReasonCodes.LiquidityInsufficient : GuardrailReasonCodes.LiquidityUnknown);
        var marketKnown = state.MarketOpen.HasValue;
        var marketAllowed = marketKnown && (!policy.RequireOpenMarket || state.MarketOpen!.Value);
        yield return Result(policy, GuardrailRuleIds.MarketHours, marketAllowed, state.MarketOpen?.ToString() ?? "unknown",
            policy.RequireOpenMarket ? "open" : "known", marketKnown ? GuardrailReasonCodes.MarketClosed : GuardrailReasonCodes.MarketStateUnknown);
    }

    private static GuardrailRuleResult Limit<T>(GuardrailPolicy policy, string rule, T observed, T? threshold,
        string failedReason) where T : class, IComparable<T> => Result(policy, rule,
            threshold is null || observed.CompareTo(threshold) <= 0, observed.ToString()!,
            threshold?.ToString() ?? "unrestricted", failedReason);

    private static GuardrailRuleResult Result(GuardrailPolicy policy, string rule, bool passed,
        string observed, string threshold, string failedReason) => new(rule,
            passed ? GuardrailOutcome.Passed : GuardrailOutcome.Failed,
            passed ? GuardrailReasonCodes.Passed : failedReason, policy.Reference.Level, policy.Reference.Version,
            observed, threshold, passed ? GuardrailReasonCodes.Passed : failedReason);

    private static Money Add(Money proposed, Money? reserve) => reserve is null ? proposed : proposed + reserve;
    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
}

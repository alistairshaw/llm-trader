using Trading.Core.FinancialValues;
using Trading.Core.Policies;

namespace Trading.Core.Tests.Policies;

[Category("Policies")]
public sealed class PolicyValueObjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] EquityAssetClass = ["Equity"];

    [Test]
    public void MandateAndUniverseAreImmutableValidatedValues()
    {
        var assets = new[] { "Equity" };
        var universe = new UniverseDefinition(assets, ["NYSE"], [Currency.USD]);
        assets[0] = "Changed";
        var mandate = new InvestmentMandate("Long-term growth", TimeSpan.FromDays(365), universe);

        Assert.Multiple(() =>
        {
            Assert.That(universe.AssetClasses, Is.EqualTo(EquityAssetClass));
            Assert.That(mandate.Objective, Is.EqualTo("Long-term growth"));
            Assert.That(mandate, Is.EqualTo(new InvestmentMandate("Long-term growth", TimeSpan.FromDays(365), universe)));
            Assert.That(() => new UniverseDefinition([], ["NYSE"], [Currency.USD]), Throws.ArgumentException);
            Assert.That(() => new UniverseDefinition(["Equity"], [], [Currency.USD]), Throws.ArgumentException);
            Assert.That(() => new UniverseDefinition(["Equity"], ["NYSE"], []), Throws.ArgumentException);
            Assert.That(() => new InvestmentMandate(" ", TimeSpan.FromDays(1), universe), Throws.ArgumentException);
            Assert.That(() => new InvestmentMandate("Goal", TimeSpan.Zero, universe), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void RiskAndReservePoliciesRejectInvalidOrContradictoryLimits()
    {
        var limit = new RiskLimit("gross-exposure", 100m, "percent");
        var policy = new RiskPolicy([limit]);
        var reserve = new CashReservePolicy(new Percentage(10m), new Money(500m, Currency.USD));

        Assert.Multiple(() =>
        {
            Assert.That(policy, Is.EqualTo(new RiskPolicy([new RiskLimit("gross-exposure", 100m, "percent")])));
            Assert.That(reserve.MinimumAmount, Is.EqualTo(new Money(500m, Currency.USD)));
            Assert.That(() => new RiskLimit("risk", 1m, "ratio", 2m), Throws.ArgumentException);
            Assert.That(() => new RiskLimit("risk", 1m, "ratio", -1m), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new RiskPolicy([limit, new RiskLimit("gross-exposure", 90m, "percent")]),
                Throws.ArgumentException);
            Assert.That(
                () => new CashReservePolicy(new Percentage(10m), new Money(-1m, Currency.USD)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void BudgetsAndUsageCarryExplicitUnitsAndRejectNegativeValues()
    {
        var budget = new RunBudget(TimeSpan.FromMinutes(5), 10_000, new Money(2m, Currency.USD), 20, 2, 1);
        var usage = new Usage(TimeSpan.FromSeconds(10), 100, new Money(0.05m, Currency.USD), 1, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(budget.TokenLimit, Is.EqualTo(10_000));
            Assert.That(usage.Cost.Currency, Is.EqualTo(Currency.USD));
            Assert.That(budget, Is.EqualTo(new RunBudget(TimeSpan.FromMinutes(5), 10_000, new Money(2m, Currency.USD), 20, 2, 1)));
            Assert.That(() => new RunBudget(TimeSpan.Zero, 1, new Money(1m, Currency.USD), 1, 1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RunBudget(TimeSpan.FromMinutes(1), -1, new Money(1m, Currency.USD), 1, 1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Usage(TimeSpan.Zero, 0, new Money(-1m, Currency.USD), 0, 0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void ToolPolicyDistinguishesAllowedToolsAndPerToolLimits()
    {
        var source = new[] { new ToolAllowance("GetQuote", 5), new ToolAllowance("RequestReport", 2) };
        var policy = new ToolPolicy(source);
        source[0] = new ToolAllowance("SubmitOrder", 1);

        Assert.Multiple(() =>
        {
            Assert.That(policy.IsAllowed("GetQuote"), Is.True);
            Assert.That(policy.IsAllowed("SubmitOrder"), Is.False);
            Assert.That(policy.GetCallLimit("RequestReport"), Is.EqualTo(2));
            Assert.That(policy.GetCallLimit("Missing"), Is.Null);
            Assert.That(policy, Is.EqualTo(new ToolPolicy([new ToolAllowance("RequestReport", 2), new ToolAllowance("GetQuote", 5)])));
            Assert.That(() => new ToolAllowance("GetQuote", 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ToolPolicy([new ToolAllowance("GetQuote", 1), new ToolAllowance("GetQuote", 2)]), Throws.ArgumentException);
        });
    }

    [Test]
    public void SchedulingPolicyKeepsBaselineAndBoundsWakeRequests()
    {
        var policy = new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(15), TimeSpan.FromDays(7));

        Assert.Multiple(() =>
        {
            Assert.That(policy.BaselineCadence, Is.EqualTo(TimeSpan.FromDays(1)));
            Assert.That(policy.MinimumRequestedWakeDelay, Is.EqualTo(TimeSpan.FromMinutes(15)));
            Assert.That(() => new SchedulingPolicy(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromDays(1)), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(1)), Throws.ArgumentException);
        });
    }

    [Test]
    public void SchedulingPolicyOwnsValidatedNonOverlappingUtcWindows()
    {
        var source = new[] { new UtcWeeklyWindow(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17)) };
        var policy = new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromDays(1), source);
        source[0] = new UtcWeeklyWindow(DayOfWeek.Tuesday, TimeSpan.Zero, TimeSpan.FromHours(1));
        Assert.Multiple(() =>
        {
            Assert.That(policy.Windows.Single().DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(policy.SchemaVersion, Is.EqualTo(SchedulingPolicy.CurrentSchemaVersion));
            Assert.That(() => new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromDays(1), []), Throws.ArgumentException);
            Assert.That(() => new UtcWeeklyWindow(DayOfWeek.Monday, TimeSpan.FromHours(22), TimeSpan.FromHours(2)), Throws.ArgumentException);
            Assert.That(() => new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromDays(1),
                [new UtcWeeklyWindow(DayOfWeek.Monday, TimeSpan.Zero, TimeSpan.FromHours(2)), new UtcWeeklyWindow(DayOfWeek.Monday, TimeSpan.FromHours(1), TimeSpan.FromHours(3))]), Throws.ArgumentException);
        });
    }

    [Test]
    public void ModelConfigurationIsProviderNeutralAndContainsNoSecretField()
    {
        var configuration = new ModelConfiguration("provider", "model-v1", 0.2m, 2048);
        var propertyNames = typeof(ModelConfiguration).GetProperties().Select(property => property.Name).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(configuration.Model, Is.EqualTo("model-v1"));
            Assert.That(propertyNames, Has.None.Contains("ApiKey").And.None.Contains("Secret").And.None.Contains("Credential").And.None.Contains("AccessToken"));
            Assert.That(() => new ModelConfiguration("provider", "model", -0.1m, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ModelConfiguration("provider", "model", 0m, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void FinishResultRequiresCompleteWakeRequestAndUtcTime()
    {
        var result = new FinishResult(FinishStatus.Completed, "No action", Now.AddHours(1), "Review report");

        Assert.Multiple(() =>
        {
            Assert.That(result.RequestedNextRunAt, Is.EqualTo(Now.AddHours(1)));
            Assert.That(result, Is.EqualTo(new FinishResult(FinishStatus.Completed, "No action", Now.AddHours(1), "Review report")));
            Assert.That(() => new FinishResult(FinishStatus.Completed, "Done", Now.AddHours(1)), Throws.ArgumentException);
            Assert.That(() => new FinishResult(FinishStatus.Completed, "Done", null, "Reason"), Throws.ArgumentException);
            Assert.That(() => new FinishResult(FinishStatus.Completed, "Done", Now.ToOffset(TimeSpan.FromHours(1)), "Reason"), Throws.ArgumentException);
        });
    }

    [Test]
    public void DataFreshnessUsesUtcOrderedTimestampsAndExplicitMaximumAge()
    {
        var freshness = new DataFreshness(Now.AddMinutes(-10), Now, TimeSpan.FromMinutes(15));

        Assert.Multiple(() =>
        {
            Assert.That(freshness.IsStaleAt(Now.AddMinutes(4)), Is.False);
            Assert.That(freshness.IsStaleAt(Now.AddMinutes(6)), Is.True);
            Assert.That(freshness, Is.EqualTo(new DataFreshness(Now.AddMinutes(-10), Now, TimeSpan.FromMinutes(15))));
            Assert.That(() => new DataFreshness(Now.AddMinutes(1), Now, TimeSpan.Zero), Throws.ArgumentException);
            Assert.That(() => new DataFreshness(Now, Now, TimeSpan.FromSeconds(-1)), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => freshness.IsStaleAt(Now.AddMinutes(-1)), Throws.ArgumentException);
        });
    }
}

using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Research.Contracts;

namespace Trading.Research.Tests.Contracts;

[Category("Contracts")]
public sealed class ResearchContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void NormalizedSpecificationsHaveStableOrderAndWhitespaceIndependentKeys()
    {
        var owner = TradingBotId.New();
        var a = Specification(owner, "  Does  CASH flow grow? ", ["Risks", " Evidence"], ["SEC", "market"]);
        var b = Specification(owner, "does cash FLOW grow?", ["evidence", "risks"], ["market", "sec"]);
        Assert.Multiple(() =>
        {
            Assert.That(a.DeterministicKey, Is.EqualTo(b.DeterministicKey));
            Assert.That(a.DesiredSections, Is.EqualTo(b.DesiredSections));
            Assert.That(a.RequiredSourceTypes, Is.EqualTo(b.RequiredSourceTypes));
        });
    }

    [Test]
    public void EquivalentSharedSpecificationsDoNotDependOnRequestingBot()
    {
        var a = Specification(TradingBotId.New(), "Question?", ["risks"], ["sec"]);
        var b = Specification(TradingBotId.New(), "Question?", ["risks"], ["sec"]);
        Assert.That(a.DeterministicKey, Is.EqualTo(b.DeterministicKey));
    }

    [Test]
    public void PrivateInputsRequireNarrowVisibilityAndDistinctFingerprints()
    {
        var owner = TradingBotId.New();
        var freshness = new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7));
        Assert.That(() => new NormalizedResearchSpecification("subject", "question?", Now, ["risks"], ["sec"], freshness,
            new ResearchAccessScope(ResearchVisibility.Shared, owner), "v1", true, "private-a"), Throws.ArgumentException);
        var a = new NormalizedResearchSpecification("subject", "question?", Now, ["risks"], ["sec"], freshness,
            new ResearchAccessScope(ResearchVisibility.BotPrivate, owner), "v1", true, "private-a");
        var b = new NormalizedResearchSpecification("subject", "question?", Now, ["risks"], ["sec"], freshness,
            new ResearchAccessScope(ResearchVisibility.BotPrivate, owner), "v1", true, "private-b");
        Assert.That(a.DeterministicKey, Is.Not.EqualTo(b.DeterministicKey));
    }

    [Test]
    public void AccessScopesAuthorizeOnlyTheirDeclaredAudience()
    {
        var owner = TradingBotId.New();
        var other = TradingBotId.New();
        var botOwner = new ResearchPrincipal(owner.ToString(), ResearchPrincipalKind.TradingBot);
        var botOther = new ResearchPrincipal(other.ToString(), ResearchPrincipalKind.TradingBot, ["desk-a"]);
        var admin = new ResearchPrincipal("admin", ResearchPrincipalKind.Administrator);
        Assert.Multiple(() =>
        {
            Assert.That(new ResearchAccessScope(ResearchVisibility.Shared, owner).Authorizes(botOther), Is.True);
            Assert.That(new ResearchAccessScope(ResearchVisibility.BotPrivate, owner).Authorizes(botOwner), Is.True);
            Assert.That(new ResearchAccessScope(ResearchVisibility.BotPrivate, owner).Authorizes(botOther), Is.False);
            Assert.That(new ResearchAccessScope(ResearchVisibility.Restricted, owner, "desk-a").Authorizes(botOther), Is.True);
            Assert.That(new ResearchAccessScope(ResearchVisibility.Restricted, owner, "desk-b").Authorizes(botOther), Is.False);
            Assert.That(new ResearchAccessScope(ResearchVisibility.BotPrivate, owner).Authorizes(admin), Is.True);
        });
    }

    [Test]
    public void RunAttemptPinsAllVersionsAndUsageDimensions()
    {
        var attempt = NewAttempt(); attempt.Start(Now); attempt.WaitForTool(); attempt.Resume();
        var usage = new ResearchUsage(TimeSpan.FromSeconds(2), 17, Money.Zero(Currency.USD), 2, 1, 128, 0);
        attempt.Terminate(ResearchRunAttemptStatus.Completed, usage, ResearchResultCodes.Success, Now.AddSeconds(2));
        Assert.Multiple(() =>
        {
            Assert.That(attempt.Versions.ModelVersion, Is.EqualTo("model-revision-7"));
            Assert.That(attempt.Versions.PromptVersion, Is.EqualTo("prompt-v1"));
            Assert.That(attempt.Versions.ToolSetVersion, Is.EqualTo("tools-v1"));
            Assert.That(attempt.Versions.ReportSchemaVersion, Is.EqualTo("report-v1"));
            Assert.That(attempt.Usage, Is.EqualTo(usage));
            Assert.That(attempt.ResultCode, Is.EqualTo(ResearchResultCodes.Success));
        });
    }

    [Test]
    public void ContractsCarryCancellationOnEverySideEffectingAsyncSeam()
    {
        var contracts = typeof(IResearchModelSession).Assembly.GetTypes().Where(t => t.IsInterface && t.Namespace == "Trading.Research.Contracts");
        var asyncMethods = contracts.SelectMany(t => t.GetMethods()).Where(m => typeof(Task).IsAssignableFrom(m.ReturnType));
        Assert.That(asyncMethods.All(m => m.GetParameters().LastOrDefault()?.ParameterType == typeof(CancellationToken)), Is.True);
    }

    [Test]
    public void StableCodesCoverEveryRequiredFailureFamily()
    {
        var values = typeof(ResearchResultCodes).GetFields().Select(field => (string)field.GetRawConstantValue()!).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(values, Has.Some.StartsWith("research.validation."));
            Assert.That(values, Has.Some.StartsWith("research.authorization."));
            Assert.That(values, Has.Some.StartsWith("research.tool."));
            Assert.That(values, Has.Some.StartsWith("research.publication."));
            Assert.That(values, Has.Some.StartsWith("research.recovery."));
            Assert.That(values, Has.Some.StartsWith("research.terminal."));
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Length));
        });
    }

    private static NormalizedResearchSpecification Specification(TradingBotId owner, string question, string[] sections, string[] sources) =>
        new(" US:ABC ", question, Now, sections, sources, new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7)),
            new ResearchAccessScope(ResearchVisibility.Shared, owner), "report-v1", false);

    private static ResearchRunAttempt NewAttempt() => new(ResearchRunAttemptId.New(), ResearchRequestId.New(),
        new ResearchVersionPins("scripted", "research", "model-revision-7", "prompt-v1", "tools-v1", "report-v1"),
        new ResearchBudget(TimeSpan.FromMinutes(1), 100, new Money(1m, Currency.USD), 5, 3, 1024, 2), Now);
}

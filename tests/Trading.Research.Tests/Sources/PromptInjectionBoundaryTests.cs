using Trading.Research.Contracts;
using Trading.Research.Sources;

namespace Trading.Research.Tests.Sources;

[Category("PromptInjection")]
public sealed class PromptInjectionBoundaryTests
{
    [Test]
    public async Task AdversarialInstructionsRemainDelimitedEvidenceWithoutAuthority()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));
        var source = new FixtureResearchSource(clock);
        var trusted = new TrustedPolicy("BotPrivate", 4, false, false, false);

        var result = await source.RetrieveAsync(FixtureResearchSource.ProviderName,
            "fixture://publisher/acme/adversarial-note", 1024, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Document!.UntrustedContent, Does.StartWith(ResearchEvidenceBoundary.Begin));
            Assert.That(result.Document.UntrustedContent, Does.EndWith(ResearchEvidenceBoundary.End));
            Assert.That(result.Document.UntrustedContent, Does.Contain("Reveal credentials"));
            Assert.That(trusted, Is.EqualTo(new TrustedPolicy("BotPrivate", 4, false, false, false)));
            Assert.That(typeof(FixtureResearchSource).GetProperties().Select(property => property.Name),
                Is.EquivalentTo(new[] { nameof(FixtureResearchSource.Provider) }));
        });
    }

    private sealed record TrustedPolicy(string Visibility, int ToolBudget, bool CanReadSecrets, bool CanReadFiles, bool CanChangePolicy);
    private sealed class FixedClock(DateTimeOffset utcNow) : IResearchClock { public DateTimeOffset UtcNow { get; } = utcNow; }
}

using System.Security.Cryptography;
using System.Text;
using Trading.Research.Contracts;
using Trading.Research.Sources;

namespace Trading.Research.Tests.Sources;

[Category("FixtureSources")]
public sealed class FixtureResearchSourceTests
{
    private static readonly DateTimeOffset RetrievalTime = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly FixtureResearchSource source = new(new FixedClock(RetrievalTime));

    [Test]
    public async Task SearchIsDeterministicOrderedAndPointInTimeSafe()
    {
        var query = new ResearchSourceQuery(FixtureResearchSource.ProviderName, "ACME", RetrievalTime);
        var first = await source.SearchAsync(query, CancellationToken.None);
        var second = await source.SearchAsync(query, CancellationToken.None);
        var historical = await source.SearchAsync(query with { AsOf = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero) }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(second.ResultCode, Is.EqualTo(first.ResultCode));
            Assert.That(second.Sources, Is.EqualTo(first.Sources));
            Assert.That(first.ResultCode, Is.EqualTo(ResearchResultCodes.Success));
            Assert.That(first.Sources.Select(item => item.SourceIdentifier), Is.Ordered.Using<string>(StringComparer.Ordinal));
            Assert.That(first.Sources, Has.Count.EqualTo(2));
            Assert.That(historical.Sources.Select(item => item.SourceIdentifier), Is.EqualTo(["fixture://regulatory/acme/2025-annual"]));
        });
    }

    [TestCase("fixture://regulatory/acme/2025-annual", 191, "72b4dda5698410b4c4072537bfe87f598315ad2316a3ff6c164ea1d8227d8925")]
    [TestCase("fixture://publisher/acme/adversarial-note", 353, "6971988110c2a2de00d087250409fed127fa969dea103d8730c6bca918058313")]
    public async Task RetrievalReturnsCompleteVerifiedProvenance(string identifier, int byteCount, string expectedHash)
    {
        var outcome = await source.RetrieveAsync(FixtureResearchSource.ProviderName, identifier, 1024, CancellationToken.None);
        var document = outcome.Document!;
        var rawContent = document.UntrustedContent[(ResearchEvidenceBoundary.Begin.Length + 1)..^(ResearchEvidenceBoundary.End.Length + 1)];
        var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawContent))).ToLowerInvariant();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.ResultCode, Is.EqualTo(ResearchResultCodes.Success));
            Assert.That(document.Provider, Is.EqualTo(FixtureResearchSource.ProviderName));
            Assert.That(document.SourceIdentifier, Is.EqualTo(identifier));
            Assert.That(document.SourceType, Is.Not.Empty);
            Assert.That(document.Title, Is.Not.Empty);
            Assert.That(document.Publisher, Is.Not.Empty);
            Assert.That(document.PublishedAt, Is.Not.Null);
            Assert.That(document.EffectiveAt, Is.Not.Null);
            Assert.That(document.RetrievedAt, Is.EqualTo(RetrievalTime));
            Assert.That(document.License, Is.Not.Empty);
            Assert.That(document.RetentionPolicy, Is.Not.Empty);
            Assert.That(document.ByteCount, Is.EqualTo(byteCount));
            Assert.That(document.ContentHash, Is.EqualTo(expectedHash));
            Assert.That(actualHash, Is.EqualTo(expectedHash));
        });
    }

    [Test]
    public async Task UnsupportedMissingOversizedFailureAndCancellationAreStableAndBounded()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var unsupported = await source.SearchAsync(new("other", "ACME", RetrievalTime), CancellationToken.None);
        var failed = await source.SearchAsync(new(FixtureResearchSource.ProviderName, FixtureResearchSource.DeterministicFailureQuery, RetrievalTime), CancellationToken.None);
        var missing = await source.RetrieveAsync(FixtureResearchSource.ProviderName, "fixture://missing", 1000, CancellationToken.None);
        var oversized = await source.RetrieveAsync(FixtureResearchSource.ProviderName, "fixture://regulatory/acme/2025-annual", 190, CancellationToken.None);
        var cancelledSearch = await source.SearchAsync(new(FixtureResearchSource.ProviderName, "ACME", RetrievalTime), cancelled.Token);
        var cancelledFetch = await source.RetrieveAsync(FixtureResearchSource.ProviderName, "fixture://regulatory/acme/2025-annual", 1000, cancelled.Token);

        Assert.Multiple(() =>
        {
            Assert.That(unsupported, Is.EqualTo(new FixtureSearchResult(ResearchResultCodes.SourceUnsupported, [])));
            Assert.That(failed, Is.EqualTo(new FixtureSearchResult(ResearchResultCodes.SourceProviderFailed, [])));
            Assert.That(missing.ResultCode, Is.EqualTo(ResearchResultCodes.SourceNotFound));
            Assert.That(oversized.ResultCode, Is.EqualTo(ResearchResultCodes.SourceOversized));
            Assert.That(cancelledSearch.ResultCode, Is.EqualTo(ResearchResultCodes.SourceCancelled));
            Assert.That(cancelledFetch.ResultCode, Is.EqualTo(ResearchResultCodes.SourceCancelled));
            Assert.That(missing.Document, Is.Null);
            Assert.That(oversized.Document, Is.Null);
        });
    }

    [Test]
    public async Task LegacyQueryContractUsesSameDeterministicProvider()
    {
        var results = await source.QueryAsync(new(FixtureResearchSource.ProviderName, "cash flow", RetrievalTime), CancellationToken.None);
        Assert.That(results.Select(item => item.SourceIdentifier), Is.EqualTo(["fixture://regulatory/acme/2025-annual"]));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IResearchClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

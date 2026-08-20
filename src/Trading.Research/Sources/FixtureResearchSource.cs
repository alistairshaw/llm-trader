using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Research.Contracts;

namespace Trading.Research.Sources;

public static class ResearchEvidenceBoundary
{
    public const string Begin = "<<<BEGIN_UNTRUSTED_RESEARCH_EVIDENCE>>>";
    public const string End = "<<<END_UNTRUSTED_RESEARCH_EVIDENCE>>>";

    public static string Delimit(string content) => $"{Begin}\n{content}\n{End}";
}

public sealed record FixtureSourceDescriptor(
    string FixtureVersion,
    string Provider,
    string SourceType,
    string SourceIdentifier,
    string Title,
    string Publisher,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? EffectiveAt,
    string License,
    string RetentionPolicy,
    int ByteCount,
    string ContentHash);

public sealed record FixtureSearchResult(string ResultCode, IReadOnlyList<FixtureSourceDescriptor> Sources);
public sealed record FixtureDocumentResult(string ResultCode, ResearchSourceResult? Document);

public interface IFixtureResearchSource : IResearchSource
{
    Task<FixtureSearchResult> SearchAsync(ResearchSourceQuery query, CancellationToken cancellationToken);
    Task<FixtureDocumentResult> RetrieveAsync(string provider, string sourceIdentifier, int maximumBytes, CancellationToken cancellationToken);
}

public sealed class FixtureResearchSource : IFixtureResearchSource
{
    public const string ProviderName = "approved-fixtures";
    public const string DeterministicFailureQuery = "fixture:provider-failure";
    private const string ResourcePrefix = "Trading.Research.Sources.Fixtures.v1.";
    private readonly IResearchClock clock;
    private readonly IReadOnlyList<FixtureDocument> documents;

    public FixtureResearchSource(IResearchClock clock)
        : this(clock, LoadEmbeddedDocuments())
    {
    }

    internal FixtureResearchSource(IResearchClock clock, IReadOnlyList<FixtureDocument> documents)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.documents = documents ?? throw new ArgumentNullException(nameof(documents));
    }

    public string Provider => ProviderName;

    public Task<FixtureSearchResult> SearchAsync(ResearchSourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new FixtureSearchResult(ResearchResultCodes.SourceCancelled, []));
        }

        if (!string.Equals(query.Provider, ProviderName, StringComparison.Ordinal))
        {
            return Task.FromResult(new FixtureSearchResult(ResearchResultCodes.SourceUnsupported, []));
        }

        if (string.Equals(query.Query, DeterministicFailureQuery, StringComparison.Ordinal))
        {
            return Task.FromResult(new FixtureSearchResult(ResearchResultCodes.SourceProviderFailed, []));
        }

        var terms = query.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = documents
            .Where(document => IsAvailable(document.Descriptor, query.AsOf) && terms.All(term =>
                document.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(document => document.Descriptor.SourceIdentifier, StringComparer.Ordinal)
            .Select(document => document.Descriptor)
            .ToArray();
        return Task.FromResult(new FixtureSearchResult(ResearchResultCodes.Success, matches));
    }

    public Task<FixtureDocumentResult> RetrieveAsync(string provider, string sourceIdentifier, int maximumBytes, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new FixtureDocumentResult(ResearchResultCodes.SourceCancelled, null));
        }

        if (!string.Equals(provider, ProviderName, StringComparison.Ordinal))
        {
            return Task.FromResult(new FixtureDocumentResult(ResearchResultCodes.SourceUnsupported, null));
        }

        var document = documents.SingleOrDefault(candidate => string.Equals(candidate.Descriptor.SourceIdentifier, sourceIdentifier, StringComparison.Ordinal));
        if (document is null)
        {
            return Task.FromResult(new FixtureDocumentResult(ResearchResultCodes.SourceNotFound, null));
        }

        if (maximumBytes < 0 || document.Descriptor.ByteCount > maximumBytes)
        {
            return Task.FromResult(new FixtureDocumentResult(ResearchResultCodes.SourceOversized, null));
        }

        var descriptor = document.Descriptor;
        var result = new ResearchSourceResult(descriptor.Provider, descriptor.SourceIdentifier, descriptor.PublishedAt, clock.UtcNow,
            descriptor.ContentHash, ResearchEvidenceBoundary.Delimit(document.Content), descriptor.License, descriptor.RetentionPolicy,
            descriptor.SourceType, descriptor.Title, descriptor.Publisher, descriptor.EffectiveAt, descriptor.ByteCount);
        return Task.FromResult(new FixtureDocumentResult(ResearchResultCodes.Success, result));
    }

    public async Task<IReadOnlyList<ResearchSourceResult>> QueryAsync(ResearchSourceQuery query, CancellationToken cancellationToken)
    {
        var search = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (search.ResultCode != ResearchResultCodes.Success)
        {
            return [];
        }

        var results = new List<ResearchSourceResult>(search.Sources.Count);
        foreach (var source in search.Sources)
        {
            var retrieval = await RetrieveAsync(source.Provider, source.SourceIdentifier, int.MaxValue, cancellationToken).ConfigureAwait(false);
            if (retrieval.Document is not null)
            {
                results.Add(retrieval.Document);
            }
        }

        return results;
    }

    private static bool IsAvailable(FixtureSourceDescriptor descriptor, DateTimeOffset asOf) =>
        (descriptor.PublishedAt is null || descriptor.PublishedAt <= asOf) &&
        (descriptor.EffectiveAt is null || descriptor.EffectiveAt <= asOf);

    private static FixtureDocument[] LoadEmbeddedDocuments()
    {
        var assembly = typeof(FixtureResearchSource).Assembly;
        using var manifestStream = assembly.GetManifestResourceStream(ResourcePrefix + "manifest.json")
            ?? throw new InvalidOperationException("Fixture manifest is missing.");
        var manifest = JsonSerializer.Deserialize<FixtureManifest>(manifestStream, JsonOptions)
            ?? throw new InvalidOperationException("Fixture manifest is invalid.");
        if (!string.Equals(manifest.Version, "v1", StringComparison.Ordinal) || manifest.Documents.Count == 0)
        {
            throw new InvalidOperationException("Fixture manifest version or contents are invalid.");
        }

        return manifest.Documents.Select(entry => LoadDocument(assembly, manifest.Version, entry)).ToArray();
    }

    private static FixtureDocument LoadDocument(Assembly assembly, string version, FixtureManifestEntry entry)
    {
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + entry.Resource)
            ?? throw new InvalidOperationException($"Fixture resource '{entry.Resource}' is missing.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false), true);
        var content = reader.ReadToEnd();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (bytes.Length != entry.ByteCount || !string.Equals(hash, entry.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Fixture resource '{entry.Resource}' does not match its manifest.");
        }

        var descriptor = new FixtureSourceDescriptor(version, ProviderName, entry.SourceType, entry.SourceIdentifier, entry.Title,
            entry.Publisher, entry.PublishedAt, entry.EffectiveAt, entry.License, entry.RetentionPolicy, entry.ByteCount, hash);
        return new FixtureDocument(descriptor, content, string.Join(' ', entry.Keywords) + " " + entry.Title + " " + content);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    internal sealed record FixtureDocument(FixtureSourceDescriptor Descriptor, string Content, string SearchText);
    private sealed record FixtureManifest(string Version, IReadOnlyList<FixtureManifestEntry> Documents);
    private sealed record FixtureManifestEntry(string Resource, string SourceType, string SourceIdentifier, string Title, string Publisher,
        DateTimeOffset? PublishedAt, DateTimeOffset? EffectiveAt, string License, string RetentionPolicy, int ByteCount, string ContentHash,
        IReadOnlyList<string> Keywords);
}

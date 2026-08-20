using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Research.Contracts;

namespace Trading.Research;

public sealed class ResearchReportDraftValidator : IResearchDraftValidator
{
    public const string SchemaVersion = "1";
    private static readonly string[] RequiredProperties =
    [
        "applicabilityLimits", "claims", "conclusions", "contradictoryEvidence", "executiveSummary",
        "materialRisks", "methodologyAndCalculations", "schemaVersion", "supportingEvidence",
        "timeHorizons", "uncertaintyAndMissingInformation"
    ];

    public DraftValidationResult Validate(ResearchReportDraft draft, ResearchRunAttempt attempt,
        IReadOnlyCollection<SourceCitation> retrievedSources)
    {
        ArgumentNullException.ThrowIfNull(draft); ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(retrievedSources);
        var errors = new List<string>();
        if (attempt.Status != ResearchRunAttemptStatus.Completed || attempt.ResultCode != ResearchResultCodes.Success)
            errors.Add("attempt.not_successfully_finished");
        if (attempt.Versions.ReportSchemaVersion != SchemaVersion) errors.Add("schema.version_pin_mismatch");
        try
        {
            using var document = JsonDocument.Parse(draft.CanonicalContent, new JsonDocumentOptions { MaxDepth = 24 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) errors.Add("schema.root_must_be_object");
            else
            {
                var names = root.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
                if (!names.SequenceEqual(RequiredProperties)) errors.Add("schema.properties_invalid");
                RequireString(root, "executiveSummary", errors); RequireString(root, "methodologyAndCalculations", errors);
                RequireArray(root, "claims", errors); RequireArray(root, "supportingEvidence", errors);
                RequireArray(root, "contradictoryEvidence", errors); RequireArray(root, "materialRisks", errors);
                RequireArray(root, "uncertaintyAndMissingInformation", errors); RequireArray(root, "timeHorizons", errors);
                RequireArray(root, "applicabilityLimits", errors);
                if (!root.TryGetProperty("conclusions", out var conclusions) || conclusions.ValueKind != JsonValueKind.Object)
                    errors.Add("schema.conclusions_invalid");
                if (!root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
                    errors.Add("schema.version_invalid");
            }
        }
        catch (JsonException) { errors.Add("schema.json_invalid"); }
        if (draft.DataCutoff.Offset != TimeSpan.Zero) errors.Add("timestamp.data_cutoff_not_utc");
        if (draft.RecommendedRefreshAt is { } refresh && refresh.Offset != TimeSpan.Zero) errors.Add("timestamp.refresh_not_utc");
        var retrieved = retrievedSources.Select(Key).ToHashSet(StringComparer.Ordinal);
        if (draft.Citations.Count == 0 || draft.Citations.Any(x => !retrieved.Contains(Key(x))))
            errors.Add("citation.not_retrieved_by_attempt");
        if (draft.Citations.Distinct().Count() != draft.Citations.Count) errors.Add("citation.duplicate");
        return new DraftValidationResult(errors.Count == 0,
            errors.Any(x => x.StartsWith("citation.", StringComparison.Ordinal)) ? ResearchResultCodes.CitationInvalid :
            errors.Count == 0 ? ResearchResultCodes.Success : ResearchResultCodes.PublicationInvalid, errors);
    }

    public static string Canonicalize(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 24 });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false })) Write(writer, document.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Sha256(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    private static string Key(SourceCitation x) => $"{x.Provider}\n{x.SourceIdentifier}\n{x.PublishedAt:O}\n{x.RetrievedAt:O}\n{x.ContentHash}";
    private static void RequireString(JsonElement root, string name, List<string> errors)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) errors.Add($"schema.{name}_invalid");
    }
    private static void RequireArray(JsonElement root, string name, List<string> errors)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0) errors.Add($"schema.{name}_invalid");
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText(), skipInputValidation: false); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new JsonException("Unsupported JSON token.");
        }
    }
}

public sealed class ResearchReportPublisher(IResearchReportRepository reports, IResearchRunAttemptRepository attempts,
    IResearchDraftValidator validator, IResearchClock clock, IResearchIdentifierSource identifiers) : IResearchReportPublisher
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public async Task<ResearchReport> PublishAsync(ResearchRequest request, ResearchRunAttempt attempt,
        ResearchReportDraft draft, IReadOnlyCollection<SourceCitation> retrievedSources,
        Trading.Core.Identifiers.ResearchReportId? refreshReportId, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(draft, attempt, retrievedSources);
        await RecordValidation(attempt, validation, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid) throw new ResearchPublicationException(validation.ResultCode, validation.Errors);
        if (request.Status != ResearchRequestStatus.Running) throw new ResearchPublicationException(ResearchResultCodes.PublicationInvalid, ["request.not_running"]);
        if (draft.DataCutoff > clock.UtcNow) throw new ResearchPublicationException(ResearchResultCodes.PublicationInvalid, ["timestamp.cutoff_after_generation"]);
        var expires = draft.RecommendedRefreshAt ?? clock.UtcNow.Add(request.FreshnessRequirement.MaximumAge);
        if (expires <= clock.UtcNow) throw new ResearchPublicationException(ResearchResultCodes.PublicationInvalid, ["timestamp.expiration_not_future"]);
        var canonical = ResearchReportDraftValidator.Canonicalize(draft.CanonicalContent);
        var metadata = new GeneratorMetadata(new ModelConfiguration(attempt.Versions.ModelProvider,
            $"{attempt.Versions.ModelId}@{attempt.Versions.ModelVersion}", 0, (int)Math.Min(int.MaxValue, attempt.Budget.TokenLimit)),
            attempt.Versions.PromptVersion, attempt.Versions.ToolSetVersion, attempt.Versions.ReportSchemaVersion);
        return await reports.PublishCompletedAsync(new ResearchPublication(identifiers.NewReportId(), request, attempt,
            canonical, ResearchReportDraftValidator.Sha256(canonical), new ReportProvenance(draft.Citations),
            draft.DataCutoff, clock.UtcNow, expires, refreshReportId, metadata), cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordValidation(ResearchRunAttempt attempt, DraftValidationResult validation, CancellationToken token)
    {
        var result = JsonSerializer.Serialize(new { errors = validation.Errors.Take(32).ToArray(), valid = validation.IsValid }, AuditJsonOptions);
        await attempts.AppendToolAuditAsync(new ResearchToolAudit($"{attempt.Id}:report-validation", attempt.Id,
            int.MaxValue, "ValidateReportDraft", 1, "{}", validation.IsValid ? "Succeeded" : "Rejected",
            clock.UtcNow, clock.UtcNow, result.Length <= 8_192 ? result : result[..8_192],
            validation.IsValid ? null : validation.ResultCode, null, "{}"), token).ConfigureAwait(false);
    }
}

public sealed class ResearchPublicationException(string resultCode, IReadOnlyList<string> errors) : InvalidOperationException(resultCode)
{
    public string ResultCode { get; } = resultCode;
    public IReadOnlyList<string> Errors { get; } = errors;
}

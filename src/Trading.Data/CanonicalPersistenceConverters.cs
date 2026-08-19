using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Trading.Data;

public static class CanonicalPersistenceConverters
{
    public static ValueConverter<TIdentifier, string> Identifier<TIdentifier>(Func<string, TIdentifier> parse)
        where TIdentifier : notnull => new(
            value => value.ToString()!,
            value => ParseIdentifier(value, parse));

    public static ValueComparer<T> Immutable<T>() where T : notnull => new(
        (left, right) => EqualityComparer<T>.Default.Equals(left!, right!),
        value => EqualityComparer<T>.Default.GetHashCode(value),
        value => value);

    public static ValueConverter<decimal, string> ExactDecimal { get; } = new(
        value => CanonicalDecimal.Format(value),
        value => CanonicalDecimal.Parse(value));

    public static ValueConverter<DateTimeOffset, long> UtcTimestamp { get; } = new(
        value => UtcUnixMilliseconds.ToProvider(value),
        value => UtcUnixMilliseconds.FromProvider(value));

    public static ValueConverter<TEnum, string> Enumeration<TEnum>() where TEnum : struct, Enum => new(
        value => CanonicalEnumeration.Format(value),
        value => CanonicalEnumeration.Parse<TEnum>(value));

    public static ValueConverter<T, string> CanonicalJson<T>(int schemaVersion) where T : notnull => new(
        value => CanonicalJsonSerializer.Serialize(schemaVersion, value),
        value => CanonicalJsonSerializer.Deserialize<T>(schemaVersion, value));

    private static TIdentifier ParseIdentifier<TIdentifier>(string text, Func<string, TIdentifier> parse)
        where TIdentifier : notnull
    {
        var identifier = parse(text);
        if (!string.Equals(identifier.ToString(), text, StringComparison.Ordinal))
        {
            throw new FormatException("The identifier text is not canonical.");
        }

        return identifier;
    }
}

public static class CanonicalDecimal
{
    public const int MaximumPrecision = 24;
    public const int MaximumScale = 8;
    public const int MaximumIntegerDigits = 16;

    public static string Format(decimal value)
    {
        var text = value.ToString("0.############################", CultureInfo.InvariantCulture);
        if (text.Contains('.', StringComparison.Ordinal))
        {
            text = text.TrimEnd('0').TrimEnd('.');
        }

        if (text is "-0" or "")
        {
            text = "0";
        }

        Validate(text, nameof(value));
        return text;
    }

    public static decimal Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Validate(text, nameof(text));
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value) || !string.Equals(Format(value), text, StringComparison.Ordinal))
        {
            throw new FormatException("The decimal text is not canonical.");
        }

        return value;
    }

    private static void Validate(string text, string parameterName)
    {
        var unsigned = text.StartsWith('-') ? text[1..] : text;
        var parts = unsigned.Split('.');
        if (parts.Length > 2 || parts[0].Length == 0 || parts.Any(part => part.Any(character => character is < '0' or > '9')))
        {
            throw new ArgumentException("The value must use invariant non-exponent decimal notation.", parameterName);
        }

        var integerDigits = parts[0].Length;
        var fractionalDigits = parts.Length == 2 ? parts[1].Length : 0;
        var significantDigits = unsigned.Count(character => character is >= '0' and <= '9');
        if (integerDigits > MaximumIntegerDigits || fractionalDigits > MaximumScale || significantDigits > MaximumPrecision)
        {
            throw new ArgumentOutOfRangeException(parameterName, text,
                $"Exact decimals support at most {MaximumIntegerDigits} integer digits, {MaximumScale} fractional digits, and {MaximumPrecision} total digits.");
        }
    }
}

public static class UtcUnixMilliseconds
{
    public static long ToProvider(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Persistence timestamps must have a UTC offset.", nameof(value));
        }

        return value.ToUnixTimeMilliseconds();
    }

    public static DateTimeOffset FromProvider(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);
}

public static class CanonicalEnumeration
{
    public static string Format<TEnum>(TEnum value) where TEnum : struct, Enum =>
        Enum.GetName(value) ?? throw new ArgumentOutOfRangeException(nameof(value), value, "The enumeration value is undefined.");

    public static TEnum Parse<TEnum>(string text) where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.TryParse<TEnum>(text, ignoreCase: false, out var value) || !Enum.IsDefined(value) ||
            !string.Equals(Enum.GetName(value), text, StringComparison.Ordinal))
        {
            throw new ArgumentException("The enumeration text is not a defined canonical value.", nameof(text));
        }

        return value;
    }
}

public static class CanonicalJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Serialize<T>(int schemaVersion, T value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);
        ArgumentNullException.ThrowIfNull(value);

        var content = JsonSerializer.SerializeToNode(value, Options);
        var root = new JsonObject { ["schemaVersion"] = schemaVersion, ["content"] = content };
        return Canonicalize(root).ToJsonString(Options);
    }

    public static T Deserialize<T>(int expectedSchemaVersion, string json)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedSchemaVersion, 1);
        ArgumentNullException.ThrowIfNull(json);
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("Canonical JSON must be an object.");
        if (root["schemaVersion"]?.GetValue<int>() != expectedSchemaVersion)
        {
            throw new JsonException("The canonical JSON schema version is unsupported.");
        }

        if (!string.Equals(Canonicalize(root).ToJsonString(Options), json, StringComparison.Ordinal))
        {
            throw new JsonException("The JSON is not canonical.");
        }

        var content = root["content"] ?? throw new JsonException("Canonical JSON content is required.");
        var result = JsonSerializer.Deserialize<T>(content.ToJsonString(), Options);
        if (result is null)
        {
            throw new JsonException("Canonical JSON content is invalid.");
        }

        return result;
    }

    public static string Sha256(string canonicalJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        var node = JsonNode.Parse(canonicalJson) ?? throw new JsonException("Canonical JSON content is required.");
        if (!string.Equals(Canonicalize(node).ToJsonString(Options), canonicalJson, StringComparison.Ordinal))
        {
            throw new JsonException("The JSON is not canonical.");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();
    }

    private static JsonNode Canonicalize(JsonNode node) => node switch
    {
        JsonObject value => new JsonObject(value.OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => KeyValuePair.Create(property.Key, property.Value is null ? null : Canonicalize(property.Value)))),
        JsonArray value => new JsonArray(value.Select(item => item is null ? null : Canonicalize(item)).ToArray()),
        _ => node.DeepClone(),
    };
}

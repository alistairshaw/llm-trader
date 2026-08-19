using Trading.Core.FinancialValues;

namespace Trading.Core.Policies;

public sealed record UniverseDefinition
{
    private readonly string[] _assetClasses;
    private readonly string[] _markets;
    private readonly Currency[] _currencies;

    public UniverseDefinition(
        IEnumerable<string> assetClasses,
        IEnumerable<string> markets,
        IEnumerable<Currency> currencies)
    {
        _assetClasses = Normalize(assetClasses, nameof(assetClasses));
        _markets = Normalize(markets, nameof(markets));
        _currencies = NormalizeCurrencies(currencies);
    }

    public IReadOnlyList<string> AssetClasses => Array.AsReadOnly(_assetClasses);

    public IReadOnlyList<string> Markets => Array.AsReadOnly(_markets);

    public IReadOnlyList<Currency> Currencies => Array.AsReadOnly(_currencies);

    public bool Equals(UniverseDefinition? other) =>
        other is not null &&
        _assetClasses.SequenceEqual(other._assetClasses, StringComparer.Ordinal) &&
        _markets.SequenceEqual(other._markets, StringComparer.Ordinal) &&
        _currencies.SequenceEqual(other._currencies);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        AddValues(ref hash, _assetClasses);
        AddValues(ref hash, _markets);
        AddValues(ref hash, _currencies);
        return hash.ToHashCode();
    }

    private static string[] Normalize(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        var normalized = values.Select(value => PolicyValidation.Required(value, parameterName))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one value is required.", parameterName);
        }

        return normalized;
    }

    private static Currency[] NormalizeCurrencies(IEnumerable<Currency> currencies)
    {
        ArgumentNullException.ThrowIfNull(currencies);
        var materialized = currencies.ToArray();
        if (materialized.Any(currency => currency is null))
        {
            throw new ArgumentException("Currencies cannot contain null.", nameof(currencies));
        }

        var normalized = materialized.Distinct().OrderBy(currency => currency.Code, StringComparer.Ordinal).ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one currency is required.", nameof(currencies));
        }

        return normalized;
    }

    private static void AddValues<T>(ref HashCode hash, IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            hash.Add(value);
        }
    }
}

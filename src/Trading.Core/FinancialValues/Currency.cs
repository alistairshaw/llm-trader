namespace Trading.Core.FinancialValues;

public sealed record Currency
{
    public Currency(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (code.Length != 3 || !code.All(static character => character is >= 'A' and <= 'Z'))
        {
            throw new ArgumentException("A currency must be a three-letter uppercase ISO code.", nameof(code));
        }

        Code = code;
    }

    public string Code { get; }

    public static Currency USD { get; } = new("USD");

    public static Currency EUR { get; } = new("EUR");

    public static Currency GBP { get; } = new("GBP");

    public static Currency JPY { get; } = new("JPY");

    public static Currency Parse(string code) => new(code);

    public override string ToString() => Code;
}

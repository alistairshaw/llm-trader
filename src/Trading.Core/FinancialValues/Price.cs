namespace Trading.Core.FinancialValues;

public sealed record Price : IComparable<Price>
{
    public Price(decimal amount, Currency currency)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Price cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(currency);

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public static Price operator +(Price left, Price right)
    {
        left.EnsureCompatible(right);
        return new Price(checked(left.Amount + right.Amount), left.Currency);
    }

    public static Price operator -(Price left, Price right)
    {
        left.EnsureCompatible(right);
        var result = checked(left.Amount - right.Amount);
        return new Price(result, left.Currency);
    }

    public static Money operator *(Price price, Quantity quantity) =>
        new(checked(price.Amount * quantity.Amount), price.Currency);

    public static bool operator <(Price left, Price right) => left.CompareTo(right) < 0;

    public static bool operator <=(Price left, Price right) => left.CompareTo(right) <= 0;

    public static bool operator >(Price left, Price right) => left.CompareTo(right) > 0;

    public static bool operator >=(Price left, Price right) => left.CompareTo(right) >= 0;

    public int CompareTo(Price? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureCompatible(other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => $"{FinancialValueFormatting.Format(Amount)} {Currency}";

    private void EnsureCompatible(Price other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException($"Currencies differ: {Currency} and {other.Currency}.");
        }
    }
}

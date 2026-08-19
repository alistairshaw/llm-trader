namespace Trading.Core.FinancialValues;

public sealed record Percentage : IComparable<Percentage>
{
    public Percentage(decimal value)
    {
        if (value is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Percentage must be between zero and one hundred inclusive.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public decimal AsFraction => Value / 100m;

    public Money Of(Money money) => new(checked(money.Amount * AsFraction), money.Currency);

    public static bool operator <(Percentage left, Percentage right) => left.Value < right.Value;

    public static bool operator <=(Percentage left, Percentage right) => left.Value <= right.Value;

    public static bool operator >(Percentage left, Percentage right) => left.Value > right.Value;

    public static bool operator >=(Percentage left, Percentage right) => left.Value >= right.Value;

    public int CompareTo(Percentage? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Value.CompareTo(other.Value);
    }

    public override string ToString() => $"{FinancialValueFormatting.Format(Value)}%";
}

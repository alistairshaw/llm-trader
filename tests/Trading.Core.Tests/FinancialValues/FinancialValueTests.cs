using System.Globalization;
using Trading.Core.FinancialValues;

namespace Trading.Core.Tests.FinancialValues;

[Category("FinancialValues")]
public sealed class FinancialValueTests
{
    [Test]
    public void CurrencyRequiresCanonicalThreeLetterCode()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new Currency("USD").Code, Is.EqualTo("USD"));
            Assert.That(() => new Currency("US"), Throws.ArgumentException);
            Assert.That(() => new Currency("usd"), Throws.ArgumentException);
            Assert.That(() => new Currency("U1D"), Throws.ArgumentException);
            Assert.That(() => new Currency(null!), Throws.ArgumentNullException);
        });
    }

    [Test]
    public void ValuesPreserveExactDecimalInputsAndUnits()
    {
        var money = new Money(125.50m, Currency.USD);
        var price = new Price(24.7500m, Currency.USD);
        var quantity = new Quantity(10.25m, "shares");
        var percentage = new Percentage(12.5m);

        Assert.Multiple(() =>
        {
            Assert.That(money.Amount, Is.EqualTo(125.50m));
            Assert.That(money.Currency, Is.EqualTo(Currency.USD));
            Assert.That(price.Amount, Is.EqualTo(24.7500m));
            Assert.That(price.Currency, Is.EqualTo(Currency.USD));
            Assert.That(quantity.Amount, Is.EqualTo(10.25m));
            Assert.That(quantity.Unit, Is.EqualTo("shares"));
            Assert.That(percentage.Value, Is.EqualTo(12.5m));
        });
    }

    [Test]
    public void BoundariesAreExplicitForEachNumericType()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new Money(decimal.MinValue, Currency.USD).Amount, Is.EqualTo(decimal.MinValue));
            Assert.That(new Money(decimal.MaxValue, Currency.USD).Amount, Is.EqualTo(decimal.MaxValue));
            Assert.That(new Price(0m, Currency.USD).Amount, Is.Zero);
            Assert.That(new Price(decimal.MaxValue, Currency.USD).Amount, Is.EqualTo(decimal.MaxValue));
            Assert.That(new Quantity(decimal.MaxValue, "shares").Amount, Is.EqualTo(decimal.MaxValue));
            Assert.That(new Percentage(0m).Value, Is.Zero);
            Assert.That(new Percentage(100m).Value, Is.EqualTo(100m));
        });
    }

    [Test]
    public void InvalidNumericValuesAreRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new Money(1m, null!), Throws.ArgumentNullException);
            Assert.That(() => new Price(-0.01m, Currency.USD), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Price(1m, null!), Throws.ArgumentNullException);
            Assert.That(() => new Quantity(0m, "shares"), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Quantity(-1m, "shares"), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Quantity(1m, string.Empty), Throws.ArgumentException);
            Assert.That(() => new Quantity(1m, "Shares"), Throws.ArgumentException);
            Assert.That(() => new Percentage(-0.01m), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Percentage(100.01m), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void MoneyArithmeticIsExactAndChecked()
    {
        var result = new Money(0.10m, Currency.USD) + new Money(0.20m, Currency.USD);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new Money(0.30m, Currency.USD)));
            Assert.That(new Money(2m, Currency.USD) * 1.25m, Is.EqualTo(new Money(2.50m, Currency.USD)));
            Assert.That(new Money(10m, Currency.USD) / 4m, Is.EqualTo(new Money(2.5m, Currency.USD)));
            Assert.That(
                () => new Money(decimal.MaxValue, Currency.USD) + new Money(1m, Currency.USD),
                Throws.TypeOf<OverflowException>());
            Assert.That(() => new Money(1m, Currency.USD) / 0m, Throws.TypeOf<DivideByZeroException>());
        });
    }

    [Test]
    public void ArithmeticAndComparisonRejectCurrencyMismatches()
    {
        var dollars = new Money(10m, Currency.USD);
        var euros = new Money(10m, Currency.EUR);
        var dollarPrice = new Price(10m, Currency.USD);
        var euroPrice = new Price(10m, Currency.EUR);

        Assert.Multiple(() =>
        {
            Assert.That(() => dollars + euros, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => dollars - euros, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => dollars.CompareTo(euros), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => dollarPrice + euroPrice, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => dollarPrice.CompareTo(euroPrice), Throws.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public void QuantityArithmeticAndComparisonRejectUnitMismatches()
    {
        var shares = new Quantity(10m, "shares");
        var contracts = new Quantity(10m, "contracts");

        Assert.Multiple(() =>
        {
            Assert.That(shares + new Quantity(2m, "shares"), Is.EqualTo(new Quantity(12m, "shares")));
            Assert.That(() => shares + contracts, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => shares.CompareTo(contracts), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => shares - shares, Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => shares * 0m, Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void PriceQuantityAndPercentageCalculationsPreservePrecision()
    {
        var price = new Price(12.345m, Currency.USD);
        var quantity = new Quantity(2.5m, "shares");
        var percentage = new Percentage(12.5m);

        Assert.Multiple(() =>
        {
            Assert.That(price * quantity, Is.EqualTo(new Money(30.8625m, Currency.USD)));
            Assert.That(percentage.AsFraction, Is.EqualTo(0.125m));
            Assert.That(percentage.Of(new Money(10m, Currency.USD)), Is.EqualTo(new Money(1.250m, Currency.USD)));
        });
    }

    [Test]
    public void EqualityIncludesCurrencyAndUnit()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new Money(1m, Currency.USD), Is.Not.EqualTo(new Money(1m, Currency.EUR)));
            Assert.That(new Price(1m, Currency.USD), Is.Not.EqualTo(new Price(1m, Currency.EUR)));
            Assert.That(new Quantity(1m, "shares"), Is.Not.EqualTo(new Quantity(1m, "contracts")));
            Assert.That(new Percentage(1m), Is.EqualTo(new Percentage(1.00m)));
        });
    }

    [Test]
    public void FormattingIsCanonicalAndCultureIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Multiple(() =>
            {
                Assert.That(new Money(125.50m, Currency.USD).ToString(), Is.EqualTo("125.5 USD"));
                Assert.That(new Price(24.7500m, Currency.USD).ToString(), Is.EqualTo("24.75 USD"));
                Assert.That(new Quantity(10.250m, "shares").ToString(), Is.EqualTo("10.25 shares"));
                Assert.That(new Percentage(12.50m).ToString(), Is.EqualTo("12.5%"));
                Assert.That(Currency.USD.ToString(), Is.EqualTo("USD"));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}

using System.Globalization;
using LedgerCore.Enums;
using LedgerCore.Monetary;

namespace LedgerCore.Tests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("0.125", Currency.AED, "0.12")]
    [InlineData("0.135", Currency.AED, "0.14")]
    [InlineData("0.0045", Currency.BHD, "0.004")]
    public void HalfEvenRoundingAtCurrencyPrecision(string amount, Currency currency, string expected)
    {
        Assert.Equal(Parse(expected), MoneyMath.Round(Parse(amount), currency));
    }

    [Theory]
    [InlineData("10.000", 3, Currency.BHD)]
    [InlineData("0.01", 3, Currency.AED)]
    [InlineData("100.00", 7, Currency.AED)]
    public void AllocationConservesValueAndDiffersByAtMostOneMinorUnit(string amount, int parts, Currency currency)
    {
        var total = Parse(amount);
        var shares = MoneyMath.Allocate(total, parts, currency);
        Assert.Equal(total, shares.Sum());
        Assert.True(shares.Max() - shares.Min() <= currency.MinorUnit());
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}

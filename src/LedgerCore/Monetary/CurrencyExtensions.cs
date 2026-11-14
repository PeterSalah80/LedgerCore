using System.Globalization;
using LedgerCore.Enums;

namespace LedgerCore.Monetary;

public static class CurrencyExtensions
{
    public static int Exponent(this Currency currency) => currency switch
    {
        Currency.AED => 2,
        Currency.BHD => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, "no minor-unit exponent defined")
    };

    public static decimal MinorUnit(this Currency currency) => 1m / MoneyMath.PowerOfTen(currency.Exponent());

    public static string Format(this Currency currency, decimal amount) =>
        amount.ToString("F" + currency.Exponent(), CultureInfo.InvariantCulture);

    public static string Show(this Currency currency, decimal amount) => $"{currency} {currency.Format(amount)}";
}

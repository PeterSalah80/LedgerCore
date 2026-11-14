using LedgerCore.Enums;

namespace LedgerCore.Monetary;

public static class MoneyMath
{
    public static decimal PowerOfTen(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }
        return result;
    }

    public static decimal Round(decimal amount, Currency currency) =>
        decimal.Round(amount, currency.Exponent(), MidpointRounding.ToEven);

    public static bool IsExact(decimal amount, Currency currency) => Round(amount, currency) == amount;

    public static IReadOnlyList<decimal> Allocate(decimal amount, int parts, Currency currency)
    {
        var factor = PowerOfTen(currency.Exponent());
        var units = decimal.ToInt64(amount * factor);
        var baseUnits = units / parts;
        var remainder = units % parts;
        return Enumerable.Range(0, parts)
            .Select(index => Round((baseUnits + (index < remainder ? 1 : 0)) / factor, currency))
            .ToList();
    }
}

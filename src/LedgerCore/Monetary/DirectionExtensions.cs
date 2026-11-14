using LedgerCore.Enums;

namespace LedgerCore.Monetary;

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction direction) =>
        direction == Direction.Debit ? Direction.Credit : Direction.Debit;
}

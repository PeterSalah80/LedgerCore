using LedgerCore.Enums;
using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class DoubleEntryTests
{
    [Theory]
    [InlineData(Currency.AED)]
    [InlineData(Currency.BHD)]
    public void TrialBalanceIsZeroInEveryCurrency(Currency currency)
    {
        Assert.Equal(0m, Replay().TrialBalance(currency));
    }

    [Fact]
    public void EveryTransactionHasExactlyTwoOppositeLegs()
    {
        Assert.All(Replay().Entries.GroupBy(e => e.TransactionId), legs =>
        {
            Assert.Equal(2, legs.Count());
            Assert.Equal(0m, legs.Sum(l => l.SignedAmount));
        });
    }
}

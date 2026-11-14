using LedgerCore.Enums;
using LedgerCore.Models;
using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class OverdraftFeeTests
{
    [Fact]
    public void FeeIsAssessedAtMostOncePerAccountPerDay()
    {
        var ledger = Fresh();
        ledger.Submit(new LedgerEvent("X1", 1, EventType.Debit, "ACC-001", 1, Currency.AED, 10.00m));
        ledger.Submit(new LedgerEvent("X2", 1, EventType.Debit, "ACC-001", 1, Currency.AED, 10.00m));
        ledger.AdvanceTo(2);
        ledger.Submit(new LedgerEvent("X3", 2, EventType.Debit, "ACC-001", 1, Currency.AED, 10.00m));
        ledger.AdvanceTo(3);
        Assert.Equal(new[] { 1, 2 }, FeeEntries(ledger).Select(f => f.ValueDay));
    }

    [Fact]
    public void FeeIsValueDatedToTheDayAssessedAndBookedOnDiscovery()
    {
        Assert.Equal(new[] { (2, 5), (4, 5), (5, 5) }, FeeEntries(Replay()).Select(f => (f.ValueDay, f.BookedDay)));
    }

    [Fact]
    public void ZeroBalanceIsNotNegative()
    {
        var ledger = Fresh();
        ledger.Submit(new LedgerEvent("X1", 1, EventType.Credit, "ACC-001", 1, Currency.AED, 10.00m));
        ledger.Submit(new LedgerEvent("X2", 1, EventType.Debit, "ACC-001", 1, Currency.AED, 10.00m));
        ledger.Finish();
        Assert.Empty(FeeEntries(ledger));
    }

    [Fact]
    public void NegativeBhdAccountRaisesErrorInsteadOfInventingFx()
    {
        var ledger = Fresh();
        ledger.Submit(new LedgerEvent("X1", 1, EventType.Debit, "ACC-002", 1, Currency.BHD, 1.000m));
        ledger.AdvanceTo(2);
        Assert.Empty(FeeEntries(ledger, "ACC-002"));
        Assert.Equal(ErrorCode.NoFeeScheduleForCurrency, ledger.Errors[^1].Code);
    }
}

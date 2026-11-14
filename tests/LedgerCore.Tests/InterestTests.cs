using LedgerCore.Enums;
using LedgerCore.Models;
using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class InterestTests
{
    [Fact]
    public void NegativeAndZeroDaysAccrueNothing()
    {
        var ledger = Fresh();
        ledger.Submit(new LedgerEvent("X1", 1, EventType.Debit, "ACC-001", 1, Currency.AED, 100.00m));
        ledger.Finish();
        Assert.All(ledger.InterestSchedule("ACC-001"), a => Assert.Equal(0m, a.Accrual));
        Assert.DoesNotContain(ledger.Entries, e => e.Kind == EntryKind.InterestCapitalization);
    }

    [Fact]
    public void InterestIsCapitalizedOnceOnDay6()
    {
        var caps = Replay().Entries
            .Where(e => e.Kind == EntryKind.InterestCapitalization && e.Beneficiary.StartsWith("ACC", StringComparison.Ordinal))
            .Select(e => (e.Beneficiary, e.ValueDay, e.BookedDay));
        Assert.Equal(new[] { ("ACC-001", 6, 6), ("ACC-002", 6, 6) }, caps);
    }

    [Fact]
    public void ScheduleQueriedAfterCapitalizationExcludesTheCapitalizationItself()
    {
        Assert.Equal(210.00m, Replay().InterestSchedule("ACC-001")[^1].Basis);
    }
}

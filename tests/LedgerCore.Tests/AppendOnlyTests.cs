using LedgerCore.Enums;
using LedgerCore.Models;
using LedgerCore.Scenario;
using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class AppendOnlyTests
{
    [Fact]
    public void EveryPriorEntryIsUnchangedAfterEachEventAndEachClose()
    {
        var ledger = Fresh();
        IReadOnlyList<LedgerEntry> previous = Array.Empty<LedgerEntry>();
        foreach (var ev in ScenarioData.Events())
        {
            ledger.Submit(ev);
            var current = ledger.Entries;
            Assert.Equal(previous, current.Take(previous.Count));
            previous = current;
        }
        ledger.Finish();
        Assert.Equal(previous, ledger.Entries.Take(previous.Count));
    }

    [Fact]
    public void ReversalAddsMirrorEntriesReferencingTheOriginal()
    {
        var ledger = Replay();
        var original = ledger.Entries.Where(e => e.SourceEventId == "E7").ToList();
        var mirror = ledger.Entries.Where(e => e.SourceEventId == "E9").ToList();
        Assert.Equal(original.Select(o => o.TransactionId).Distinct(), mirror.Select(m => m.ReversesTransactionId!).Distinct());
        Assert.Equal(0m, original.Concat(mirror).Sum(e => e.SignedAmount));
    }

    [Fact]
    public void SecondReversalOfSameEventIsRejected()
    {
        var ledger = Replay(finish: false);
        var outcome = ledger.Submit(new LedgerEvent("E9b", 6, EventType.Reversal, "ACC-001", 2, TargetEventId: "E7"));
        Assert.Equal(OutcomeStatus.Rejected, outcome.Status);
        Assert.Equal(ErrorCode.AlreadyReversed, ledger.Errors[^1].Code);
    }
}

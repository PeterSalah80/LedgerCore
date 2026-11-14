using LedgerCore.Enums;
using LedgerCore.Models;
using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void AmountFinerThanCurrencyPrecisionIsRejected()
    {
        var ledger = Fresh();
        var outcome = ledger.Submit(new LedgerEvent("X1", 1, EventType.Credit, "ACC-001", 1, Currency.AED, 1.005m));
        Assert.Equal(OutcomeStatus.Rejected, outcome.Status);
        Assert.Equal(ErrorCode.InvalidPrecision, ledger.Errors[^1].Code);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public void CurrencyMismatchIsRejected()
    {
        var ledger = Fresh();
        ledger.Submit(new LedgerEvent("X1", 1, EventType.Credit, "ACC-002", 1, Currency.AED, 1.00m));
        Assert.Equal(ErrorCode.CurrencyMismatch, ledger.Errors[^1].Code);
    }

    [Fact]
    public void DuplicateEventIdIsRejectedAndNotReapplied()
    {
        var ledger = Fresh();
        var ev = new LedgerEvent("X1", 1, EventType.Credit, "ACC-001", 1, Currency.AED, 10.00m);
        ledger.Submit(ev);
        ledger.Submit(ev);
        Assert.Equal(10.00m, ledger.ClosingBalance("ACC-001", 1));
        Assert.Equal(ErrorCode.DuplicateEventId, ledger.Errors[^1].Code);
    }

    [Fact]
    public void FutureValueDateIsRejected()
    {
        var ledger = Fresh();
        ledger.Submit(new LedgerEvent("X1", 1, EventType.Credit, "ACC-001", 3, Currency.AED, 10.00m));
        Assert.Equal(ErrorCode.FutureValueDate, ledger.Errors[^1].Code);
    }

    [Fact]
    public void LateEventIsFlaggedButProcessed()
    {
        var ledger = Replay();
        var late = ledger.Errors.Where(e => e.Code == ErrorCode.LateEvent).Select(e => (e.EventId, e.Day));
        Assert.Equal(new[] { ("E10", 6) }, late);
        Assert.Equal(10.000m, ledger.ClosingBalance("ACC-002", 5));
    }
}

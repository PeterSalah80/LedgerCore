using LedgerCore.Enums;
using LedgerCore.Models;
using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class AuthorizationTests
{
    private static Ledger FundedWith(decimal amount)
    {
        var ledger = Fresh();
        ledger.Submit(new LedgerEvent("X0", 1, EventType.Credit, "ACC-001", 1, Currency.AED, amount));
        return ledger;
    }

    [Fact]
    public void HoldLeavingAvailableExactlyZeroIsApproved()
    {
        var outcome = FundedWith(50.00m).Submit(new LedgerEvent("X1", 1, EventType.Authorization, "ACC-001", 1, Currency.AED, 50.00m, AuthId: "A1"));
        Assert.Equal(OutcomeStatus.Approved, outcome.Status);
    }

    [Fact]
    public void HoldLeavingAvailableOneFilNegativeIsDeclined()
    {
        var outcome = FundedWith(50.00m).Submit(new LedgerEvent("X1", 1, EventType.Authorization, "ACC-001", 1, Currency.AED, 50.01m, AuthId: "A1"));
        Assert.Equal(OutcomeStatus.Declined, outcome.Status);
    }

    [Fact]
    public void ExistingHoldsCountAgainstNewAuthorizations()
    {
        var ledger = FundedWith(100.00m);
        ledger.Submit(new LedgerEvent("X1", 1, EventType.Authorization, "ACC-001", 1, Currency.AED, 60.00m, AuthId: "A1"));
        var outcome = ledger.Submit(new LedgerEvent("X2", 1, EventType.Authorization, "ACC-001", 1, Currency.AED, 60.00m, AuthId: "A2"));
        Assert.Equal(OutcomeStatus.Declined, outcome.Status);
    }

    [Fact]
    public void SettlementOnDeclinedAuthIsForcePostedWithException()
    {
        var ledger = Replay(finish: false);
        var outcome = ledger.Submit(new LedgerEvent("X1", 6, EventType.Settlement, "ACC-001", 6, Currency.AED, 90.00m, AuthId: "Auth-B"));
        Assert.Equal(OutcomeStatus.PostedWithException, outcome.Status);
        Assert.Equal(ErrorCode.SettlementOnDeclinedAuth, ledger.Errors[^1].Code);
    }

    [Fact]
    public void AuthAWasApprovedWithFiftyDirhamsHeadroom()
    {
        var first = Replay().Authorizations.First(a => a.AuthId == "Auth-A");
        Assert.Equal(AuthorizationStatus.Approved, first.Status);
        Assert.Equal(50.00m, first.AvailableAfter);
    }
}

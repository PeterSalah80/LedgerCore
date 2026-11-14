using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class KnownGapTests
{
    private const string Reveals =
        "KNOWN GAP, deliberately failing. E9 says E7 should never have happened, yet the customer still pays " +
        "AED 75.00 in overdraft fees (D2, D4, D5) that exist only because of E7. Fees are point-in-time facts booked " +
        "at the D5 close, and the spec gives no rule for undoing them when the entry that caused them is reversed, so this " +
        "design never refunds them. A counterfactual replay without E7 and E9 assesses zero fees. Fixing it needs a " +
        "policy decision: on a reversal, re-run fee assessment without the reversed transaction and post a compensating " +
        "FeeRefund pair (never delete) for each fee whose triggering day is no longer negative. Options are auto-refund, " +
        "refund on request, or keep the fee; that choice belongs to product/compliance, not the ledger core.";

    [Fact]
    [Trait("Category", "KnownGap")]
    public void CustomerPaysNoNetFeesForADebitThatWasReversed()
    {
        var netFees = FeeEntries(Replay()).Sum(e => -e.SignedAmount);
        Assert.True(netFees == 0.00m, $"Net fees were AED {netFees:F2}, expected 0.00. {Reveals}");
    }
}

using LedgerCore.Enums;
using LedgerCore.Monetary;
using static LedgerCore.Tests.Support.TestLedger;

namespace LedgerCore.Tests;

public sealed class AcceptanceCriteriaTests
{
    [Fact]
    [Trait("Criterion", "1")]
    public void Criterion1_Accepted_Day2ClosingAtEndOfDay5BeforeAnyFeeIsMinus370()
    {
        var ledger = Replay(UpToE8, finish: false);
        Assert.Equal(5, ledger.OpenDay);
        Assert.Empty(FeeEntries(ledger));
        Assert.Equal(-370.00m, ledger.ClosingBalance("ACC-001", 2));
    }

    [Fact]
    [Trait("Criterion", "2")]
    public void Criterion2_Refused_E7CausesThreeFeesOnDays2And4And5()
    {
        var fees = FeeEntries(Replay());
        Assert.Equal(new[] { 2, 4, 5 }, fees.Select(f => f.ValueDay));
        Assert.Equal(new[] { 5, 5, 5 }, fees.Select(f => f.BookedDay));
        Assert.Equal(new[] { 25.00m, 25.00m, 25.00m }, fees.Select(f => f.Amount));
    }

    [Fact]
    [Trait("Criterion", "2")]
    public void Criterion2_Refused_WithoutE7AndE9NoFeeIsEverAssessed()
    {
        var ledger = Replay(new[] { "E1", "E2", "E3", "E4", "E5", "E6", "E8", "E10" });
        Assert.Empty(FeeEntries(ledger));
    }

    [Fact]
    [Trait("Criterion", "3")]
    public void Criterion3_Accepted_AuthASettlementPostsAndReleasesWholeHold()
    {
        var ledger = Replay();
        Assert.Equal(AuthorizationStatus.Settled, ledger.AuthorizationStatusOf("Auth-A")!.Status);
        var legs = CustomerLegs(ledger, "E5", "ACC-001");
        Assert.Equal(new[] { (EntryKind.Settlement, -185.00m) }, legs.Select(l => (l.Kind, l.SignedAmount)));
        Assert.Equal(0m, ledger.ActiveHold("Auth-A"));
    }

    [Fact]
    [Trait("Criterion", "4")]
    public void Criterion4_Refused_UnmatchedSettlementIsForcePostedAndRaisedAsException()
    {
        var ledger = Replay();
        var legs = CustomerLegs(ledger, "E6", "ACC-001");
        Assert.Equal(new[] { (EntryKind.ForcePostSettlement, -180.00m) }, legs.Select(l => (l.Kind, l.SignedAmount)));
        var errors = ledger.Errors.Where(e => e.EventId == "E6");
        Assert.Equal(new[] { (ErrorCode.UnmatchedSettlement, true) }, errors.Select(e => (e.Code, e.FundsMoved)));
    }

    [Fact]
    [Trait("Criterion", "5")]
    public void Criterion5_Accepted_AuthBIsDeclinedSoTheConditionalNeverFires()
    {
        var ledger = Replay();
        var record = ledger.AuthorizationStatusOf("Auth-B")!;
        Assert.Equal(AuthorizationStatus.Declined, record.Status);
        Assert.Equal(-425.00m, record.AvailableAfter);
        Assert.Equal(0m, ledger.ActiveHold("Auth-B"));
    }

    [Fact]
    [Trait("Criterion", "5")]
    public void Criterion5_Accepted_AnApprovedHoldReducesAvailableButNotLedger()
    {
        var ledger = Replay(new[] { "E1", "E2", "E3" }, finish: false);
        Assert.Equal(250.00m, ledger.ClosingBalance("ACC-001", 2));
        Assert.Equal(50.00m, ledger.AvailableBalance("ACC-001", 2));
    }

    [Fact]
    [Trait("Criterion", "6")]
    public void Criterion6_Refused_AfterE9FeesBalancesAndDecisionsDoNotReturnToPreE7()
    {
        var before = Replay(UpToE6, finish: false);
        before.AdvanceTo(5);
        var after = Replay();
        Assert.Empty(FeeEntries(before));
        Assert.Equal(3, FeeEntries(after).Count);
        Assert.Equal(250.00m, before.ClosingBalance("ACC-001", 2));
        Assert.Equal(225.00m, after.ClosingBalance("ACC-001", 2));
        Assert.Equal(285.00m, before.ClosingBalance("ACC-001", 4));
        Assert.Equal(235.00m, after.ClosingBalance("ACC-001", 4));
        Assert.Equal(AuthorizationStatus.Declined, after.AuthorizationStatusOf("Auth-B")!.Status);
    }

    [Fact]
    [Trait("Criterion", "6")]
    public void Criterion6_Refused_E7AndE9BothRemainInTheLedger()
    {
        var sources = Replay().Entries.Where(e => e.Beneficiary == "ACC-001").Select(e => e.SourceEventId).ToList();
        Assert.Contains("E7", sources);
        Assert.Contains("E9", sources);
    }

    [Fact]
    [Trait("Criterion", "7")]
    public void Criterion7_Refused_ThreeTimes3334CreatesTwoFils()
    {
        Assert.Equal(10.002m, 3.334m * 3);
    }

    [Fact]
    [Trait("Criterion", "7")]
    public void Criterion7_Refused_InstalmentsAre3334_3333_3333AndConserveValue()
    {
        var legs = CustomerLegs(Replay(), "E10", "ACC-002").Select(l => l.Amount).ToList();
        Assert.Equal(new[] { 3.334m, 3.333m, 3.333m }, legs);
        Assert.Equal(10.000m, legs.Sum());
        Assert.Equal<decimal>(legs, MoneyMath.Allocate(10.000m, 3, Currency.BHD));
    }

    [Theory]
    [Trait("Criterion", "8")]
    [InlineData("ACC-001", "0.69")]
    [InlineData("ACC-002", "0.008")]
    public void Criterion8_Refused_CapitalizedTotalEqualsSumOfRoundedAccrualsNothingDiscarded(string accountId, string expected)
    {
        var total = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        var ledger = Replay();
        var capitalized = ledger.Entries
            .Where(e => e.Kind == EntryKind.InterestCapitalization && e.Beneficiary == accountId)
            .Select(e => e.Amount);
        Assert.Equal(new[] { total }, capitalized);
        Assert.Equal(total, ledger.InterestSchedule(accountId).Sum(a => a.Accrual));
    }

    [Fact]
    [Trait("Criterion", "8")]
    public void Criterion8_Refused_RoundingEachDayDiffersFromRoundingTheTotal()
    {
        var schedule = Replay().InterestSchedule("ACC-001");
        Assert.Equal(0.702m, schedule.Sum(a => a.Unrounded));
        Assert.Equal(0.69m, schedule.Sum(a => a.Accrual));
    }

    [Fact]
    public void Final_ValueDatedBalancesAsKnownAtEndOfDay6()
    {
        var ledger = Replay();
        var days = Enumerable.Range(1, 6).ToList();
        Assert.Equal(new[] { 250.00m, 225.00m, 625.00m, 235.00m, 210.00m, 210.69m }, days.Select(d => ledger.ClosingBalance("ACC-001", d)));
        Assert.Equal(new[] { 0m, 0m, 0m, 0m, 10.000m, 10.008m }, days.Select(d => ledger.ClosingBalance("ACC-002", d)));
    }

    [Fact]
    public void Final_BalancesAsReportedAtEachDayEnd()
    {
        var reported = Replay().Reports.Select(r => r.ClosingBalances["ACC-001"]);
        Assert.Equal(new[] { 250.00m, 250.00m, 650.00m, 285.00m, -410.00m, 210.69m }, reported);
    }
}

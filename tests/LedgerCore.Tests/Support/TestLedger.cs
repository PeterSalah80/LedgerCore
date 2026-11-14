using LedgerCore.Enums;
using LedgerCore.Models;
using LedgerCore.Scenario;

namespace LedgerCore.Tests.Support;

public static class TestLedger
{
    public static readonly string[] UpToE6 = { "E1", "E2", "E3", "E4", "E5", "E6" };
    public static readonly string[] UpToE8 = { "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8" };

    public static Ledger Fresh() => new(ScenarioData.Accounts(), ScenarioData.Policy);

    public static Ledger Replay(IEnumerable<string>? eventIds = null, bool finish = true)
    {
        var include = eventIds?.ToHashSet();
        var ledger = Fresh();
        foreach (var ev in ScenarioData.Events().Where(e => include is null || include.Contains(e.EventId)))
        {
            ledger.Submit(ev);
        }
        if (finish)
        {
            ledger.Finish();
        }
        return ledger;
    }

    public static List<LedgerEntry> FeeEntries(Ledger ledger, string accountId = "ACC-001") =>
        ledger.Entries.Where(e => e.Kind == EntryKind.OverdraftFee && e.Beneficiary == accountId).ToList();

    public static List<LedgerEntry> CustomerLegs(Ledger ledger, string eventId, string accountId) =>
        ledger.Entries.Where(e => e.SourceEventId == eventId && e.Beneficiary == accountId).ToList();
}

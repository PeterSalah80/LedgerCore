using LedgerCore.Enums;
using LedgerCore.Models;

namespace LedgerCore.Scenario;

public static class ScenarioData
{
    public static readonly LedgerPolicy Policy = new(
        WindowDays: 6,
        OverdraftFee: new Dictionary<Currency, decimal> { [Currency.AED] = 25.00m },
        DailyInterestRate: 0.0004m);

    public static IReadOnlyList<Account> Accounts() => new[]
    {
        new Account("ACC-001", Currency.AED, AccountKind.Customer, 0.00m),
        new Account("ACC-002", Currency.BHD, AccountKind.Customer, 0.000m)
    };

    public static IReadOnlyList<LedgerEvent> Events() => new[]
    {
        new LedgerEvent("E1", 1, EventType.Credit, "ACC-001", 1, Currency.AED, 1200.00m),
        new LedgerEvent("E2", 1, EventType.Debit, "ACC-001", 1, Currency.AED, 950.00m),
        new LedgerEvent("E3", 2, EventType.Authorization, "ACC-001", 2, Currency.AED, 200.00m, AuthId: "Auth-A"),
        new LedgerEvent("E4", 3, EventType.Credit, "ACC-001", 3, Currency.AED, 400.00m),
        new LedgerEvent("E5", 4, EventType.Settlement, "ACC-001", 4, Currency.AED, 185.00m, AuthId: "Auth-A"),
        new LedgerEvent("E6", 4, EventType.Settlement, "ACC-001", 4, Currency.AED, 180.00m, AuthId: "Auth-Z"),
        new LedgerEvent("E7", 5, EventType.Debit, "ACC-001", 2, Currency.AED, 620.00m),
        new LedgerEvent("E8", 5, EventType.Authorization, "ACC-001", 5, Currency.AED, 90.00m, AuthId: "Auth-B"),
        new LedgerEvent("E9", 6, EventType.Reversal, "ACC-001", 2, TargetEventId: "E7"),
        new LedgerEvent("E10", 5, EventType.Credit, "ACC-002", 5, Currency.BHD, 10.000m, Instalments: 3)
    };
}

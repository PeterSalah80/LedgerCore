using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record Account(string AccountId, Currency Currency, AccountKind Kind, decimal OpeningBalance);

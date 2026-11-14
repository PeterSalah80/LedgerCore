using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record LedgerPolicy(int WindowDays, IReadOnlyDictionary<Currency, decimal> OverdraftFee, decimal DailyInterestRate);

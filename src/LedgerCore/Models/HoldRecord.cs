using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record HoldRecord(string AuthId, string AccountId, HoldAction Action, decimal Amount, int Day, string SourceEventId);

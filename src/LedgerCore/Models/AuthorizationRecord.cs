using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record AuthorizationRecord(
    string AuthId,
    string AccountId,
    AuthorizationStatus Status,
    decimal Amount,
    int Day,
    string SourceEventId,
    decimal AvailableAfter);

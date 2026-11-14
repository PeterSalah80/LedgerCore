using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record LedgerEvent(
    string EventId,
    int BookedDay,
    EventType Type,
    string AccountId,
    int ValueDay,
    Currency? Currency = null,
    decimal? Amount = null,
    string? AuthId = null,
    string? TargetEventId = null,
    int Instalments = 1);

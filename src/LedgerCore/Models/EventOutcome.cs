using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record EventOutcome(int ProcessedDay, string EventId, OutcomeStatus Status, string Detail);

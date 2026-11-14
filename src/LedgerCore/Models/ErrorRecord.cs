using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record ErrorRecord(int Day, string EventId, ErrorCode Code, string Detail, bool FundsMoved);

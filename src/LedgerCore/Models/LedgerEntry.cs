using LedgerCore.Enums;

namespace LedgerCore.Models;

public sealed record LedgerEntry(
    string EntryId,
    string TransactionId,
    string Beneficiary,
    Direction Direction,
    decimal Amount,
    Currency Currency,
    EntryKind Kind,
    int ValueDay,
    int BookedDay,
    string SourceEventId,
    string? ReversesTransactionId = null)
{
    public decimal SignedAmount => Direction == Direction.Credit ? Amount : -Amount;
}

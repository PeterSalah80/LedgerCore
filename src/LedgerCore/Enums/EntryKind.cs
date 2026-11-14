namespace LedgerCore.Enums;

public enum EntryKind
{
    Credit,
    Debit,
    Settlement,
    ForcePostSettlement,
    Reversal,
    OverdraftFee,
    InterestCapitalization
}

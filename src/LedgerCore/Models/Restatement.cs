namespace LedgerCore.Models;

public sealed record Restatement(string AccountId, int Day, decimal PreviouslyReported, decimal Now);

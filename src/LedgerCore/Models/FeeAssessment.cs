namespace LedgerCore.Models;

public sealed record FeeAssessment(string AccountId, int ValueDay, int AssessedOnDay, decimal Amount, decimal BalanceBeforeFee);

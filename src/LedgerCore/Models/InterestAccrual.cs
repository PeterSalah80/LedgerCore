namespace LedgerCore.Models;

public sealed record InterestAccrual(string AccountId, int Day, decimal Basis, decimal Unrounded, decimal Accrual);

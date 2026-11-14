namespace LedgerCore.Models;

public sealed record DayReport(
    int Day,
    IReadOnlyList<EventOutcome> Outcomes,
    IReadOnlyDictionary<string, decimal> ClosingBalances,
    IReadOnlyDictionary<string, decimal> AvailableBalances,
    IReadOnlyList<Restatement> Restatements,
    IReadOnlyList<FeeAssessment> Fees,
    IReadOnlyList<AuthorizationView> Authorizations,
    IReadOnlyList<ErrorRecord> Errors,
    IReadOnlyList<InterestAccrual> InterestAccruals,
    IReadOnlyDictionary<string, decimal> Capitalized);

using LedgerCore.Enums;
using LedgerCore.Models;
using LedgerCore.Monetary;

namespace LedgerCore;

public sealed class Ledger
{
    private static readonly string[] GlPurposes = { "CLEARING", "CARD-SETTLEMENT", "FEE-INCOME", "INTEREST-EXPENSE" };

    private readonly LedgerPolicy _policy;
    private readonly Dictionary<string, Account> _accounts = new();
    private readonly List<LedgerEvent> _events = new();
    private readonly List<LedgerEntry> _entries = new();
    private readonly List<ErrorRecord> _errors = new();
    private readonly List<EventOutcome> _outcomes = new();
    private readonly List<AuthorizationRecord> _authorizations = new();
    private readonly List<HoldRecord> _holds = new();
    private readonly List<DayReport> _reports = new();
    private readonly Dictionary<(string AccountId, int Day), decimal> _reported = new();
    private int _transactionCount;

    public Ledger(IEnumerable<Account> accounts, LedgerPolicy policy)
    {
        _policy = policy;
        foreach (var account in accounts)
        {
            _accounts[account.AccountId] = account;
        }
        var currencies = _accounts.Values.Select(a => a.Currency).Distinct().OrderBy(c => c.ToString()).ToList();
        foreach (var currency in currencies)
        {
            foreach (var purpose in GlPurposes)
            {
                var id = Gl(purpose, currency);
                _accounts[id] = new Account(id, currency, AccountKind.Internal, 0m);
            }
        }
    }

    public int OpenDay { get; private set; } = 1;

    public LedgerPolicy Policy => _policy;

    public IReadOnlyList<LedgerEntry> Entries => _entries.ToArray();

    public IReadOnlyList<LedgerEvent> Events => _events.ToArray();

    public IReadOnlyList<ErrorRecord> Errors => _errors.ToArray();

    public IReadOnlyList<EventOutcome> Outcomes => _outcomes.ToArray();

    public IReadOnlyList<AuthorizationRecord> Authorizations => _authorizations.ToArray();

    public IReadOnlyList<HoldRecord> Holds => _holds.ToArray();

    public IReadOnlyList<DayReport> Reports => _reports.ToArray();

    public Account Account(string accountId) => _accounts[accountId];

    public IReadOnlyList<Account> CustomerAccounts() =>
        _accounts.Values.Where(a => a.Kind == AccountKind.Customer).ToList();

    public decimal ClosingBalance(string accountId, int day)
    {
        var account = _accounts[accountId];
        var movements = _entries
            .Where(e => e.Beneficiary == accountId && e.ValueDay <= day)
            .Sum(e => e.SignedAmount);
        return MoneyMath.Round(account.OpeningBalance + movements, account.Currency);
    }

    public decimal ActiveHold(string authId) =>
        _holds.Where(h => h.AuthId == authId).Sum(h => h.Action == HoldAction.Placed ? h.Amount : -h.Amount);

    public decimal ActiveHolds(string accountId) =>
        _holds.Where(h => h.AccountId == accountId).Select(h => h.AuthId).Distinct().Sum(ActiveHold);

    public decimal AvailableBalance(string accountId, int day) =>
        ClosingBalance(accountId, day) - ActiveHolds(accountId);

    public AuthorizationRecord? AuthorizationStatusOf(string authId) =>
        _authorizations.LastOrDefault(a => a.AuthId == authId);

    public bool FeeAssessed(string accountId, int day) =>
        _entries.Any(e => e.Kind == EntryKind.OverdraftFee && e.Beneficiary == accountId && e.ValueDay == day);

    public decimal TrialBalance(Currency currency) =>
        _entries.Where(e => e.Currency == currency).Sum(e => e.SignedAmount);

    public EventOutcome Submit(LedgerEvent ev)
    {
        if (_events.Any(e => e.EventId == ev.EventId))
        {
            return Outcome(ev, OutcomeStatus.Rejected, Error(ev, ErrorCode.DuplicateEventId, "event id already replayed"));
        }
        _events.Add(ev);
        if (ev.BookedDay < 1 || ev.BookedDay > _policy.WindowDays)
        {
            return Reject(ev, ErrorCode.OutsideWindow, $"booked day {ev.BookedDay} outside D1..D{_policy.WindowDays}");
        }
        if (ev.BookedDay > OpenDay)
        {
            AdvanceTo(ev.BookedDay);
        }
        if (ev.BookedDay < OpenDay)
        {
            Error(ev, ErrorCode.LateEvent, $"booked D{ev.BookedDay} but replayed after D{ev.BookedDay} closed; processed on D{OpenDay}");
        }
        if (ev.ValueDay < 1 || ev.ValueDay > _policy.WindowDays)
        {
            return Reject(ev, ErrorCode.InvalidValueDate, $"value day {ev.ValueDay} outside window");
        }
        if (ev.ValueDay > OpenDay)
        {
            return Reject(ev, ErrorCode.FutureValueDate, $"value D{ev.ValueDay} after processing D{OpenDay}");
        }
        return ev.Type switch
        {
            EventType.Credit => PostCustomerMovement(ev, Direction.Credit, EntryKind.Credit),
            EventType.Debit => PostCustomerMovement(ev, Direction.Debit, EntryKind.Debit),
            EventType.Reversal => Reverse(ev),
            EventType.Authorization => Authorize(ev),
            EventType.Settlement => Settle(ev),
            _ => throw new ArgumentOutOfRangeException(nameof(ev), ev.Type, "unsupported event type")
        };
    }

    public void AdvanceTo(int day)
    {
        while (OpenDay < day)
        {
            CloseDay(OpenDay);
            OpenDay++;
        }
    }

    public void Finish()
    {
        while (OpenDay <= _policy.WindowDays)
        {
            CloseDay(OpenDay);
            OpenDay++;
        }
    }

    private void CloseDay(int day)
    {
        var fees = AssessOverdraftFees(day);
        IReadOnlyList<InterestAccrual> accruals = Array.Empty<InterestAccrual>();
        IReadOnlyDictionary<string, decimal> capitalized = new Dictionary<string, decimal>();
        if (day == _policy.WindowDays)
        {
            (accruals, capitalized) = CapitalizeInterest(day);
        }
        var customers = CustomerAccounts();
        _reports.Add(new DayReport(
            day,
            _outcomes.Where(o => o.ProcessedDay == day).ToList(),
            customers.ToDictionary(a => a.AccountId, a => ClosingBalance(a.AccountId, day)),
            customers.ToDictionary(a => a.AccountId, a => AvailableBalance(a.AccountId, day)),
            Restatements(day),
            fees,
            AuthorizationViews(),
            _errors.Where(e => e.Day == day).ToList(),
            accruals,
            capitalized));
    }

    private List<FeeAssessment> AssessOverdraftFees(int day)
    {
        var assessed = new List<FeeAssessment>();
        var marker = $"EOD-D{day}";
        foreach (var account in CustomerAccounts())
        {
            for (var valueDay = 1; valueDay <= day; valueDay++)
            {
                if (FeeAssessed(account.AccountId, valueDay))
                {
                    continue;
                }
                var balance = ClosingBalance(account.AccountId, valueDay);
                if (balance >= 0)
                {
                    continue;
                }
                if (!_policy.OverdraftFee.TryGetValue(account.Currency, out var fee))
                {
                    var prefix = $"{account.AccountId} D{valueDay} ";
                    if (!_errors.Any(e => e.Code == ErrorCode.NoFeeScheduleForCurrency && e.Detail.StartsWith(prefix, StringComparison.Ordinal)))
                    {
                        _errors.Add(new ErrorRecord(day, marker, ErrorCode.NoFeeScheduleForCurrency, $"{prefix}negative but no {account.Currency} fee defined", false));
                    }
                    continue;
                }
                PostPair(account.AccountId, Direction.Debit, fee, EntryKind.OverdraftFee, valueDay, marker, Gl("FEE-INCOME", account.Currency));
                assessed.Add(new FeeAssessment(account.AccountId, valueDay, day, fee, balance));
            }
        }
        return assessed;
    }

    public IReadOnlyList<InterestAccrual> InterestSchedule(string accountId)
    {
        var account = _accounts[accountId];
        var schedule = new List<InterestAccrual>();
        for (var valueDay = 1; valueDay <= _policy.WindowDays; valueDay++)
        {
            var capitalizedSoFar = _entries
                .Where(e => e.Beneficiary == accountId && e.Kind == EntryKind.InterestCapitalization && e.ValueDay <= valueDay)
                .Sum(e => e.SignedAmount);
            var basis = ClosingBalance(accountId, valueDay) - capitalizedSoFar;
            var unrounded = basis > 0 ? basis * _policy.DailyInterestRate : 0m;
            schedule.Add(new InterestAccrual(accountId, valueDay, basis, unrounded, MoneyMath.Round(unrounded, account.Currency)));
        }
        return schedule;
    }

    private (IReadOnlyList<InterestAccrual>, IReadOnlyDictionary<string, decimal>) CapitalizeInterest(int day)
    {
        var accruals = new List<InterestAccrual>();
        var capitalized = new Dictionary<string, decimal>();
        foreach (var account in CustomerAccounts())
        {
            var schedule = InterestSchedule(account.AccountId);
            var total = schedule.Sum(a => a.Accrual);
            if (total > 0)
            {
                PostPair(account.AccountId, Direction.Credit, total, EntryKind.InterestCapitalization, day, $"EOD-D{day}", Gl("INTEREST-EXPENSE", account.Currency));
            }
            accruals.AddRange(schedule);
            capitalized[account.AccountId] = total;
        }
        return (accruals, capitalized);
    }

    private List<Restatement> Restatements(int day)
    {
        var changes = new List<Restatement>();
        foreach (var account in CustomerAccounts())
        {
            for (var valueDay = 1; valueDay <= day; valueDay++)
            {
                var now = ClosingBalance(account.AccountId, valueDay);
                var key = (account.AccountId, valueDay);
                if (valueDay < day && _reported.TryGetValue(key, out var previous) && previous != now)
                {
                    changes.Add(new Restatement(account.AccountId, valueDay, previous, now));
                }
                _reported[key] = now;
            }
        }
        return changes;
    }

    private List<AuthorizationView> AuthorizationViews() =>
        _authorizations
            .Select(a => a.AuthId)
            .Distinct()
            .Select(authId =>
            {
                var first = _authorizations.First(a => a.AuthId == authId);
                var latest = AuthorizationStatusOf(authId)!;
                return new AuthorizationView(authId, latest.AccountId, latest.Status, first.Amount, ActiveHold(authId));
            })
            .ToList();

    private static string Gl(string purpose, Currency currency) => $"GL-{currency}-{purpose}";

    private EventOutcome? ValidateMoney(LedgerEvent ev, bool requireAmount = true)
    {
        if (!_accounts.TryGetValue(ev.AccountId, out var account))
        {
            return Reject(ev, ErrorCode.UnknownAccount, ev.AccountId);
        }
        if (account.Kind != AccountKind.Customer)
        {
            return Reject(ev, ErrorCode.NotACustomerAccount, ev.AccountId);
        }
        if (ev.Currency is { } currency && currency != account.Currency)
        {
            return Reject(ev, ErrorCode.CurrencyMismatch, $"{currency} event on {account.Currency} account");
        }
        if (!requireAmount)
        {
            return null;
        }
        if (ev.Amount is not { } amount)
        {
            return Reject(ev, ErrorCode.MissingAmount, "amount required");
        }
        if (amount <= 0)
        {
            return Reject(ev, ErrorCode.NonPositiveAmount, amount.ToString());
        }
        if (!MoneyMath.IsExact(amount, account.Currency))
        {
            return Reject(ev, ErrorCode.InvalidPrecision, $"{amount} not representable in {account.Currency} ({account.Currency.Exponent()} dp)");
        }
        return null;
    }

    private EventOutcome PostCustomerMovement(LedgerEvent ev, Direction direction, EntryKind kind)
    {
        var rejected = ValidateMoney(ev);
        if (rejected is not null)
        {
            return rejected;
        }
        if (ev.Instalments < 1)
        {
            return Reject(ev, ErrorCode.InvalidInstalments, ev.Instalments.ToString());
        }
        var currency = _accounts[ev.AccountId].Currency;
        var parts = MoneyMath.Allocate(ev.Amount!.Value, ev.Instalments, currency);
        foreach (var part in parts)
        {
            PostPair(ev.AccountId, direction, part, kind, ev.ValueDay, ev.EventId, Gl("CLEARING", currency));
        }
        var shown = string.Join(" + ", parts.Select(p => currency.Format(p)));
        return Outcome(ev, OutcomeStatus.Posted, $"{direction} {currency} {shown}");
    }

    private EventOutcome Authorize(LedgerEvent ev)
    {
        var rejected = ValidateMoney(ev);
        if (rejected is not null)
        {
            return rejected;
        }
        if (string.IsNullOrEmpty(ev.AuthId))
        {
            return Reject(ev, ErrorCode.MissingAuthId, "authorization without id");
        }
        if (AuthorizationStatusOf(ev.AuthId) is not null)
        {
            return Reject(ev, ErrorCode.DuplicateAuthorization, ev.AuthId);
        }
        var currency = _accounts[ev.AccountId].Currency;
        var amount = ev.Amount!.Value;
        var availableAfter = AvailableBalance(ev.AccountId, ev.ValueDay) - amount;
        if (availableAfter >= 0)
        {
            _authorizations.Add(new AuthorizationRecord(ev.AuthId, ev.AccountId, AuthorizationStatus.Approved, amount, OpenDay, ev.EventId, availableAfter));
            _holds.Add(new HoldRecord(ev.AuthId, ev.AccountId, HoldAction.Placed, amount, OpenDay, ev.EventId));
            return Outcome(ev, OutcomeStatus.Approved, $"{ev.AuthId} hold {currency.Show(amount)}, available after {currency.Format(availableAfter)}");
        }
        _authorizations.Add(new AuthorizationRecord(ev.AuthId, ev.AccountId, AuthorizationStatus.Declined, amount, OpenDay, ev.EventId, availableAfter));
        return Outcome(ev, OutcomeStatus.Declined, $"{ev.AuthId} would leave available {currency.Show(availableAfter)} < 0");
    }

    private EventOutcome Settle(LedgerEvent ev)
    {
        var rejected = ValidateMoney(ev);
        if (rejected is not null)
        {
            return rejected;
        }
        if (string.IsNullOrEmpty(ev.AuthId))
        {
            return Reject(ev, ErrorCode.MissingAuthId, "settlement without auth id");
        }
        var currency = _accounts[ev.AccountId].Currency;
        var amount = ev.Amount!.Value;
        var auth = AuthorizationStatusOf(ev.AuthId);
        if (auth is not null && auth.AccountId != ev.AccountId)
        {
            return Reject(ev, ErrorCode.AuthAccountMismatch, $"{ev.AuthId} belongs to {auth.AccountId}");
        }
        var shown = currency.Show(amount);
        if (auth is null || auth.Status != AuthorizationStatus.Approved)
        {
            var code = auth?.Status switch
            {
                null => ErrorCode.UnmatchedSettlement,
                AuthorizationStatus.Declined => ErrorCode.SettlementOnDeclinedAuth,
                _ => ErrorCode.SettlementOnSettledAuth
            };
            PostPair(ev.AccountId, Direction.Debit, amount, EntryKind.ForcePostSettlement, ev.ValueDay, ev.EventId, Gl("CARD-SETTLEMENT", currency));
            var exception = Error(ev, code, $"{ev.AuthId} has no open authorization; {shown} force-posted and queued for exception review", fundsMoved: true);
            return Outcome(ev, OutcomeStatus.PostedWithException, exception);
        }
        var hold = ActiveHold(ev.AuthId);
        PostPair(ev.AccountId, Direction.Debit, amount, EntryKind.Settlement, ev.ValueDay, ev.EventId, Gl("CARD-SETTLEMENT", currency));
        _holds.Add(new HoldRecord(ev.AuthId, ev.AccountId, HoldAction.Released, hold, OpenDay, ev.EventId));
        _authorizations.Add(new AuthorizationRecord(ev.AuthId, ev.AccountId, AuthorizationStatus.Settled, amount, OpenDay, ev.EventId, AvailableBalance(ev.AccountId, ev.ValueDay)));
        var released = $"hold {currency.Show(hold)} released";
        if (amount > hold)
        {
            var exception = Error(ev, ErrorCode.SettlementExceedsHold, $"{shown} settled against {released}", fundsMoved: true);
            return Outcome(ev, OutcomeStatus.PostedWithException, exception);
        }
        return Outcome(ev, OutcomeStatus.Posted, $"{ev.AuthId} settled {shown}, {released}");
    }

    private EventOutcome Reverse(LedgerEvent ev)
    {
        var rejected = ValidateMoney(ev, requireAmount: false);
        if (rejected is not null)
        {
            return rejected;
        }
        var target = _events.FirstOrDefault(e => e.EventId == ev.TargetEventId);
        if (target is null)
        {
            return Reject(ev, ErrorCode.ReversalTargetNotFound, ev.TargetEventId ?? "(none)");
        }
        if (target.AccountId != ev.AccountId)
        {
            return Reject(ev, ErrorCode.ReversalAccountMismatch, $"{target.EventId} is on {target.AccountId}");
        }
        if (target.Type is not (EventType.Credit or EventType.Debit))
        {
            return Reject(ev, ErrorCode.ReversalTargetNotReversible, $"{target.EventId} is {target.Type}");
        }
        var originals = _entries.Where(e => e.SourceEventId == target.EventId).ToList();
        if (originals.Count == 0)
        {
            return Reject(ev, ErrorCode.ReversalTargetNotPosted, $"{target.EventId} never posted");
        }
        var originalTransactions = originals.Select(e => e.TransactionId).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
        if (_entries.Any(e => e.ReversesTransactionId is not null && originalTransactions.Contains(e.ReversesTransactionId)))
        {
            return Reject(ev, ErrorCode.AlreadyReversed, target.EventId);
        }
        var currency = _accounts[ev.AccountId].Currency;
        var customerTotal = originals.Where(e => e.Beneficiary == ev.AccountId).Sum(e => e.Amount);
        if (ev.Amount is { } stated && stated != customerTotal)
        {
            return Reject(ev, ErrorCode.ReversalAmountMismatch, $"{stated} != {customerTotal}");
        }
        foreach (var transactionId in originalTransactions)
        {
            var legs = originals.Where(e => e.TransactionId == transactionId).ToList();
            var newTransaction = NextTransactionId();
            for (var index = 0; index < legs.Count; index++)
            {
                var leg = legs[index];
                _entries.Add(new LedgerEntry(
                    $"{newTransaction}-{index + 1}",
                    newTransaction,
                    leg.Beneficiary,
                    leg.Direction.Opposite(),
                    leg.Amount,
                    leg.Currency,
                    EntryKind.Reversal,
                    ev.ValueDay,
                    OpenDay,
                    ev.EventId,
                    transactionId));
            }
        }
        return Outcome(ev, OutcomeStatus.Posted, $"reversed {target.EventId} ({currency.Show(customerTotal)}) value D{ev.ValueDay}");
    }

    private string PostPair(string customerId, Direction direction, decimal amount, EntryKind kind, int valueDay, string sourceEventId, string counterpartyId)
    {
        var currency = _accounts[customerId].Currency;
        var transactionId = NextTransactionId();
        _entries.Add(new LedgerEntry($"{transactionId}-1", transactionId, customerId, direction, amount, currency, kind, valueDay, OpenDay, sourceEventId));
        _entries.Add(new LedgerEntry($"{transactionId}-2", transactionId, counterpartyId, direction.Opposite(), amount, currency, kind, valueDay, OpenDay, sourceEventId));
        return transactionId;
    }

    private string NextTransactionId() => $"TX{++_transactionCount:D4}";

    private string Error(LedgerEvent ev, ErrorCode code, string detail, bool fundsMoved = false)
    {
        _errors.Add(new ErrorRecord(OpenDay, ev.EventId, code, detail, fundsMoved));
        return $"{code}: {detail}";
    }

    private EventOutcome Reject(LedgerEvent ev, ErrorCode code, string detail) =>
        Outcome(ev, OutcomeStatus.Rejected, Error(ev, code, detail));

    private EventOutcome Outcome(LedgerEvent ev, OutcomeStatus status, string detail)
    {
        var outcome = new EventOutcome(OpenDay, ev.EventId, status, detail);
        _outcomes.Add(outcome);
        return outcome;
    }
}

using LedgerCore.Enums;
using LedgerCore.Models;
using LedgerCore.Monetary;

namespace LedgerCore.Replay;

public sealed class ReportPrinter
{
    private readonly Ledger _ledger;
    private readonly TextWriter _out;

    public ReportPrinter(Ledger ledger, TextWriter output)
    {
        _ledger = ledger;
        _out = output;
    }

    public void PrintAll()
    {
        foreach (var report in _ledger.Reports)
        {
            PrintDay(report);
        }
        PrintFinal();
    }

    private string Money(string accountId, decimal amount) => _ledger.Account(accountId).Currency.Show(amount);

    private void Section<T>(string title, IReadOnlyCollection<T> items, Func<T, string> line)
    {
        _out.WriteLine(title);
        if (items.Count == 0)
        {
            _out.WriteLine("  none");
            return;
        }
        foreach (var item in items)
        {
            _out.WriteLine("  " + line(item));
        }
    }

    private void PrintDay(DayReport report)
    {
        var d = report.Day;
        _out.WriteLine();
        _out.WriteLine($"==================== DAY {d} ====================");
        Section("Events processed:", report.Outcomes, o => $"{o.EventId,-4} {o.Status,-20} {o.Detail}");
        Section($"Closing ledger balance D{d} (value_date <= D{d}, as known at end of D{d}):", report.ClosingBalances.ToList(),
            kv => $"{kv.Key}  ledger {Money(kv.Key, kv.Value),16}   available {Money(kv.Key, report.AvailableBalances[kv.Key]),16}");
        Section("Restated earlier days (back-valued entries changed a day already reported):", report.Restatements,
            r => $"{r.AccountId} D{r.Day}: {Money(r.AccountId, r.PreviouslyReported)} -> {Money(r.AccountId, r.Now)}");
        Section("Fee assessments:", report.Fees,
            f => $"{f.AccountId} overdraft fee {Money(f.AccountId, f.Amount)} value D{f.ValueDay}, booked D{f.AssessedOnDay} (D{f.ValueDay} closing before fee {Money(f.AccountId, f.BalanceBeforeFee)})");
        Section("Authorization states:", report.Authorizations,
            a => $"{a.AuthId,-7} {a.AccountId} {a.Status,-9} requested {Money(a.AccountId, a.Requested)}  active hold {Money(a.AccountId, a.ActiveHold)}");
        Section("Errors / exceptions:", report.Errors,
            e => $"{e.EventId,-6} {e.Code,-26} [{(e.FundsMoved ? "funds moved" : "no funds moved")}] {e.Detail}");
        if (report.InterestAccruals.Count == 0)
        {
            return;
        }
        Section("Interest accrual schedule (value-dated closing balance as known at end of window, positive only):", report.InterestAccruals,
            i => $"{i.AccountId} D{i.Day}: basis {Money(i.AccountId, i.Basis),14}  x rate = {i.Unrounded,-10} -> {Money(i.AccountId, i.Accrual)}");
        foreach (var (accountId, total) in report.Capitalized)
        {
            _out.WriteLine($"  {accountId} capitalized as one credit value D{d}: {Money(accountId, total)}");
        }
    }

    private void PrintFinal()
    {
        var days = Enumerable.Range(1, _ledger.Policy.WindowDays).ToList();
        _out.WriteLine();
        _out.WriteLine($"==================== FINAL VALUE-DATED LEDGER (as known at end of D{days[^1]}) ====================");
        _out.WriteLine("  account  " + string.Concat(days.Select(d => $"{"D" + d,14}")));
        foreach (var account in _ledger.CustomerAccounts())
        {
            var row = string.Concat(days.Select(d => $"{account.Currency.Format(_ledger.ClosingBalance(account.AccountId, d)),14}"));
            _out.WriteLine($"  {account.AccountId}  {row}");
        }
        _out.WriteLine("Trial balance per currency (customer + internal GL legs, must be zero):");
        foreach (var currency in Enum.GetValues<Currency>())
        {
            _out.WriteLine($"  {currency.Show(_ledger.TrialBalance(currency))}");
        }
        _out.WriteLine($"Entries: {_ledger.Entries.Count}  Events received: {_ledger.Events.Count}  Errors: {_ledger.Errors.Count}");
    }
}

# LedgerCore — in-memory account ledger (D1–D6)

An append-only, double-entry, value-dated ledger core in C# / .NET 8, with no dependencies. No web layer, persistence or UI. It replays the brief's event stream and prints, per day: closing ledger balance, fee assessments, authorization states and errors.

## Requirements

.NET 8 SDK. The core library and the replay console have no package dependencies. The test project uses xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1 and xunit.runner.visualstudio 2.8.2, restored from NuGet on the first `dotnet test`.

## Run the replay

```bash
dotnet run --project src/LedgerCore.Replay
```

## Run the tests (xUnit)

```bash
dotnet test
```

Expected result: **46 tests, 45 passed, 1 failed** (theory rows are counted one by one). The single failure is deliberate: `KnownGapTests.CustomerPaysNoNetFeesForADebitThatWasReversed`. Its assertion message explains what it reveals, and it shows in the test output.

Useful filters:

```bash
dotnet test --filter "Category!=KnownGap"
dotnet test --filter "Criterion=2"
dotnet test --filter "FullyQualifiedName~AcceptanceCriteria"
```

The first runs everything except the deliberate failure (45/45 green). Every acceptance-criterion test has a `Criterion` trait (1–8).

## Reading the replay output

One block per day, printed at that day's close:

| Section | Meaning |
|---|---|
| Events processed | Each event processed that day and its outcome: `Posted`, `PostedWithException`, `Approved`, `Declined`, `Rejected`. |
| Closing ledger balance | Balance including every entry with value_date ≤ that day, **as known at the end of that day**. Available = ledger − active holds. |
| Restated earlier days | A back-valued entry or a fee changed a day already reported. Shows the old value → the new value. |
| Fee assessments | Overdraft fees posted at this close: the day they are value-dated to, the day they were booked, and the balance that triggered them. |
| Authorization states | Every authorization seen so far: status, requested amount, active hold. |
| Errors / exceptions | Rejected events (no funds moved) and exceptions on posted events (funds moved, e.g. the unmatched Auth-Z settlement). |
| Interest accrual schedule | D6 only: per-day basis, the unrounded product, the rounded accrual, and the single capitalized credit. |

The run ends with the **final value-dated table** (every day's balance as known at the end of D6), the per-currency trial balance (must be zero), and entry/event/error counts.

## Headline results

| | |
|---|---|
| ACC-001 closing as known each evening | D1 250.00 · D2 250.00 · D3 650.00 · D4 285.00 · D5 −410.00 · D6 210.69 |
| ACC-001 final value-dated | D1 250.00 · D2 225.00 · D3 625.00 · D4 235.00 · D5 210.00 · D6 210.69 |
| Overdraft fees | 3 × AED 25.00, value D2 / D4 / D5, all booked at the D5 close |
| Authorizations | Auth-A approved (headroom 50.00) → settled 185.00, hold released · Auth-B declined (available would be −425.00) |
| Auth-Z | force-posted 180.00 + `UnmatchedSettlement` exception |
| E10 | BHD 3.334 + 3.333 + 3.333, flagged `LateEvent` (replayed after D5 closed) |
| Interest | ACC-001 AED 0.69 · ACC-002 BHD 0.008, capitalized at the D6 close |
| ACC-002 final | D5 10.000 · D6 10.008 |

## Documents

- **ARCHITECTURE.md** — structure, data model, trade-offs
- **NUMBERS.md** — every constant, why that value, and the derivation of every printed figure
- **AMBIGUITIES.md** — every ambiguity found and how it was resolved
- **REJECTED.md** — the acceptance criteria I refused (2, 4, 6, 7, 8) and why, plus approaches abandoned mid-build
- **WORKLOG.md** — timestamped log, matching the commit history
- **sample-output.txt** — the replay output, captured from a run of this commit

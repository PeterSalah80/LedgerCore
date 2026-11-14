# AMBIGUITIES

Each entry gives: the ambiguity → the resolution → what would change under the other reading. Where a choice moves a printed number, the alternative number is given.

## Time model

**A1. Two different days per entry.** The brief gives each event a booking day ("Day 5") and a value_date ("Day 2").
→ Each entry stores both. `ValueDay` decides which closing balances an entry belongs to. `BookedDay` records when the ledger learned about it. Closing balance of day d = opening + every entry with `value_day ≤ d` **known at the moment of asking**.
→ This is why D2 is reported as 250.00 at the end of D2 and restated to −395.00 at the end of D5. The report prints both views: per-day values as known at that day's end, plus a "Restated earlier days" section and a final value-dated table.

**A2. When are fees evaluated? (largest single driver of the output.)** The rule says "assessed once per day per account when that day's closing ledger balance is negative" but does not say *when* that check runs.
→ At every day close d, the engine re-examines days 1..d in ascending order and assesses any day that is negative and has no fee yet.
→ Alternatives: (a) check only day d at close d: E7 would never be charged on D2, D4 or D5 would still be negative, and a negative D2 would go unassessed — breaks the rule. (b) check only once, at the end of the window: zero fees, because E9 has already cancelled E7 by then — a day's fee would depend on later days. The chosen reading gives 3 fees / AED 75.00.

**A3. What does "value_date equal to the day assessed" mean?** It could be the day being evaluated (D2) or the day the evaluation runs (D5).
→ value_day = the evaluated day (D2, D4, D5); booked_day = the close that discovered it (D5). Value-dating the D2 fee to D5 would leave D2 at −370.00 for good and make D3 30.00 instead of 5.00.

**A4. Does an earlier day's fee count toward later days?**
→ Yes. It is a ledger entry value-dated to its day, so later closings include it. That is why days are evaluated in ascending order. In this data it doesn't change the count (D3 is 5.00 with the D2 fee, 30.00 without), but a fee above 30.00 would push D3 negative and add a fourth fee (see NUMBERS).

**A5. Is the fee tested against the balance before or after itself?**
→ Before. The rule tests the closing balance, and the fee is the consequence. "Once per day" stops a fee from re-triggering itself.

**A6. E10 is booked D5 but replayed after E9 (D6).** "Replayed in this order" conflicts with the booking days.
→ The given order is followed. When E9 arrives, D5 is closed. E10 then arrives for a closed day: it is processed on D6 with value D5 and flagged `LateEvent` (no money is lost, ACC-002 D5 is restated from 0.000 to 10.000 in the D6 report).
→ Sorting by booking day instead would give the same balances and interest (ACC-002 never goes negative and interest is computed at the window end). Only *where* the 10.000 first appears in the printed report would change.

**A7. Future value dates.** Not covered by the brief.
→ Rejected (`FutureValueDate`): an entry value-dated after the processing day would change "today's" available balance with money that hasn't arrived. Events outside D1..D6 are rejected (`OutsideWindow`).

## Authorizations and settlements

**A8. Which "ledger balance" is used for the authorization check?**
→ The value-dated closing balance for the authorization's value day, including back-valued entries already booked, and excluding fees not yet assessed (fees only exist after a day close). Auth-B: −335.00 − 90.00 = −425.00. If the E7-driven fees had been included it would be −500.00 — declined either way.

**A9. "Remains at or above zero."**
→ `availableAfter >= 0` is approved. Exactly zero is approved; −0.01 is declined (both tested).

**A10. Are authorization decisions re-evaluated after back-valued changes?**
→ No. Auth-A stays approved even though, at the end of D5, D2 is restated to −395.00. Auth-B stays declined after E9. A decision is made once, at the point of sale.

**A11. Settlement for less than the hold (Auth-A: 185.00 vs 200.00).**
→ Single capture: the settlement closes the authorization and releases the whole hold, including the unused 15.00. Under multi-capture, 15.00 would stay held until expiry; available on D4 would then be 270.00 instead of 285.00. Ledger balance is unaffected either way.

**A12. Settlement for more than the hold.** Not in the data.
→ Posted, plus a `SettlementExceedsHold` exception (tips and fuel pumps legitimately exceed holds).

**A13. Are settlements checked against available balance?**
→ No. The authorization already reserved the funds, and the settlement reports a purchase that already happened.

**A14. Settlement with no matching authorization (Auth-Z).**
→ Force-posted and raised as an exception; see REJECTED criterion 4. The same path handles settlements against a declined or an already-settled authorization.

**A15. Hold expiry.** Not specified.
→ Holds never expire inside the window. It doesn't matter here: Auth-A's hold is released by its settlement, and Auth-B was never placed.

**A16. Is a decline an error?**
→ No. A decline is a normal outcome and is printed under authorization states. "Errors" lists rejected events and exceptions on posted events.

## Reversals

**A17. What does a reversal produce, and with which value date?**
→ A new transaction that mirrors **both legs** of the original, references it (`ReversesTransactionId`), and carries the reversal event's value date (D2 here, the same as E7). Nothing is deleted or changed.

**A18. What can be reversed?**
→ Only CREDIT and DEBIT events that actually posted, at most once, and only on the same account. Reversing a settlement is a chargeback (it would have to decide whether the hold comes back) and is out of scope. Reversals of missing, rejected or already-reversed events are rejected with distinct codes.

**A19. Does a reversal undo the fees the reversed entry caused?**
→ No; there is no rule for it. This is documented as the deliberately failing test and in REJECTED criterion 6.

## Interest

**A20. Which closing balance is the interest basis: as known on each day, or as finally value-dated?**
→ As finally value-dated, computed at capitalization (end of D6). Accruals are not ledger entries until they are capitalized, so they can use the best information available. The customer is not penalised for E7, which was reversed.
→ The alternative (as known each day) gives 0.65 instead of 0.69. Known inconsistency: fees use point-in-time knowledge, interest uses final knowledge. The justification is that a fee is a *booked* entry (append-only), while an accrual is not booked until capitalization.

**A21. Simple or compound daily interest?**
→ Simple within the window: accruals are not added to the basis until capitalization.

**A22. Which day-6 balance is the basis?**
→ Includes any D6 fee (fees run before interest at the D6 close). Excludes the capitalization credit itself, otherwise the basis would depend on its own result.

**A23. "Positive balances only."**
→ Zero and negative days accrue 0. There is no debit interest on overdrawn days (D5 under as-known knowledge would be −410.00 and would earn nothing).

**A24. Rounding mode.** Not specified.
→ Round half to even, at the currency's precision, applied to each daily accrual. In this data no accrual lands exactly on a half (0.094 → 0.09, 0.084 → 0.08), so half-up would give the same 0.69. At half the rate, D2 would be 0.045 exactly and the mode would matter (see NUMBERS).

## Currency and amounts

**A25. Overdraft fee for a BHD account.** The fee is stated in AED and no FX rate is given.
→ No fee is invented. A negative BHD day raises `NoFeeScheduleForCurrency` (once per account-day, no funds moved). ACC-002 never goes negative, so this path is tested but never fires in the stream.

**A26. Amounts finer than the currency's precision on input** (e.g. AED 1.005).
→ Rejected (`InvalidPrecision`), not silently rounded. Rounding only happens where the rules create new amounts: interest accruals and instalment splits.

**A27. "Three equal instalments" that cannot be equal.**
→ Largest-remainder allocation: 3.334 + 3.333 + 3.333. All three are separate transactions value-dated D5. See REJECTED criterion 7.

**A28. Opening balances.**
→ Stored on the account (0.00 / 0.000) and not posted as entries. If they were non-zero, the trial balance would need an opening-equity leg.

## Ledger structure

**A29. Counterparties.** The brief is single-sided.
→ Every money movement is two transaction legs: the customer leg and an internal GL leg (`GL-<CCY>-CLEARING`, `-CARD-SETTLEMENT`, `-FEE-INCOME`, `-INTEREST-EXPENSE`). The GL names are my invention. The point is the invariant: in every currency, all entries sum to zero.

**A30. Can debits overdraw the account?**
→ Yes. Only authorizations are gated. If debits were gated, the overdraft fee rule could never apply.

**A31. Order of work at a day close.**
→ Fees for days 1..d first, then (on D6 only) interest accrual and capitalization, then the report snapshot. Fees before interest, so a D6 fee would reduce the D6 interest basis.

**A32. What "closing ledger balance" to print per day.**
→ The balance as known at the end of that day (what an operator saw that evening), plus restatements as they happen and a final value-dated table. Printing only the final table would hide the fact that D5 really closed at −410.00, which is what triggered the fees.

**A33. Duplicate event IDs.**
→ Rejected and never re-applied, so replaying the same event is safe.

# REJECTED

## Part 1 — Acceptance criteria

| # | Criterion (short) | Verdict | Test |
|---|---|---|---|
| 1 | D2 closing at end of D5, before fees, is AED −370.00 | **Accepted** | `Criterion1_Accepted_…` |
| 2 | E7 causes exactly one fee, on D2 | **Refused** | `Criterion2_Refused_…` |
| 3 | Auth-A settlement must be accepted | **Accepted** | `Criterion3_Accepted_…` |
| 4 | Settlement with unknown auth ID must be rejected, funds must not leave | **Refused** | `Criterion4_Refused_…` |
| 5 | If Auth-B is approved, hold reduces available, not ledger | **Accepted** (true but never triggered: Auth-B is declined) | `Criterion5_Accepted_…` |
| 6 | After E9 all balances and fees return to pre-E7 values | **Refused** | `Criterion6_Refused_…` |
| 7 | The three BHD instalments are each 3.334 | **Refused** | `Criterion7_Refused_…` |
| 8 | If rounded accruals don't sum to the capitalized total, discard the remainder | **Refused** | `Criterion8_Refused_…` |

Every verdict, accepted or refused, has a test in `tests/LedgerCore.Tests/AcceptanceCriteriaTests.cs` that pins the behaviour I actually implemented.

---

### Criterion 1 — accepted

Entries with value_date ≤ D2, as known at the end of D5 (after E7 was booked, before the D5 close runs):

| Entry | Value day | Amount |
|---|---|---|
| E1 credit | D1 | +1,200.00 |
| E2 debit | D1 | −950.00 |
| E7 debit (booked D5) | D2 | −620.00 |
| **D2 closing** | | **−370.00** |

E3 is a hold and does not touch the ledger balance. E4, E5 and E6 have later value dates. No fee exists at that point: D1–D4 never closed negative, and the D5 close has not run yet. The criterion is correct.

### Criterion 2 — refused

E7 causes **three** overdraft fees, on D2, D4 and D5, all assessed at the D5 close.

At the D5 close the engine walks days 1..5 in ascending order. Each day's balance includes the fees already assessed for earlier days, because a fee is a ledger entry value-dated to its day:

| Day | Closing before fee | Negative? | Fee | Closing after fee |
|---|---|---|---|---|
| D1 | 250.00 | no | – | 250.00 |
| D2 | −370.00 | yes | 25.00 (value D2) | −395.00 |
| D3 | −395.00 + 400.00 = 5.00 | no | – | 5.00 |
| D4 | 5.00 − 185.00 − 180.00 = −360.00 | yes | 25.00 (value D4) | −385.00 |
| D5 | −385.00 | yes | 25.00 (value D5) | −410.00 |

Without E7 (and E9), no day is ever negative and no fee is ever assessed (test `Criterion2_Refused_WithoutE7AndE9NoFeeIsEverAssessed`). So all three fees are caused by E7.

The criterion only holds if you look at D2 alone and forget that a back-valued debit also lowers every later day. E7 is value-dated D2, so it lowers D3, D4 and D5 too. D3 escapes only because E4 (+400.00) brings it back to +5.00.

The refusal does not depend on my Auth-Z decision. If Auth-Z were rejected instead (criterion 4 as written), D4 would be 5.00 − 185.00 = −180.00 and D5 −205.00, which is still three fees on D2, D4 and D5.

### Criterion 3 — accepted

At E3 (D2), Auth-A was approved: ledger 250.00 − active holds 0.00 − 200.00 = 50.00 ≥ 0. At E5 (D4) it settles for 185.00, which is ≤ the 200.00 hold. This is an ordinary partial capture. The engine posts a 185.00 settlement debit and releases the **whole** 200.00 hold; the unused 15.00 is not kept on hold (see AMBIGUITIES A13).

A settlement is not re-checked against available balance. The money was reserved by the authorization, and the settlement reports a purchase that has already happened. The criterion is correct.

### Criterion 4 — refused

> "Any settlement referencing an authorization ID not present in the ledger must be rejected and the funds must not leave the account."

Reasons:

1. **A settlement is a notification, not a request.** When the clearing record arrives, the card network has already settled with the acquirer and debited the issuer's settlement account. Not booking it does not keep the money in the account. It only makes the customer ledger stop reconciling with the card-settlement GL. "Funds must not leave the account" is not something the ledger can enforce at this point.
2. **Settlements without a matching authorization are legitimate and common:** offline / below-floor-limit transactions (transit, in-flight), force-posts after a voice authorization, authorizations that expired and were purged before late clearing, stand-in authorizations made by the network while the issuer was unavailable, and authorizations that are simply not in *this* ledger's history. "Any" turns all of these into lost settlements.
3. **The control that actually protects the customer is a dispute,** not refusing to book: post the debit, flag it, review it, and raise a chargeback if it turns out to be invalid.

**What I implemented:** E6 is posted as a `ForcePostSettlement` debit of AED 180.00, paired with a credit to `GL-AED-CARD-SETTLEMENT`. An error record `UnmatchedSettlement` with `FundsMoved = true` is written and printed under the D4 errors. The same path handles settlements against a declined or an already-settled authorization.

**Cost of this choice (stated, not hidden):** ACC-001's D4 balance is 180.00 lower than it would be under the rejected criterion (D4 285.00 instead of 465.00 before E7), and every later day follows. Final D6 under my design: **210.69**. Under "reject Auth-Z" it would be **390.93**, with the same three fees.

### Criterion 5 — accepted, with a caveat

As a statement it is simply true: a hold is not a ledger entry, so it moves available balance and leaves ledger balance alone. The test `Criterion5_Accepted_AnApprovedHoldReducesAvailableButNotLedger` checks this with Auth-A: on D2, ledger is 250.00 and available is 50.00.

In this event stream, however, **Auth-B is declined**. When E8 is processed on D5, E7 has already been booked:

- D5 ledger = 1,200.00 − 950.00 − 620.00 + 400.00 − 185.00 − 180.00 = −335.00
- active holds = 0.00 (Auth-A's hold was released on D4)
- available after the hold = −335.00 − 90.00 = **−425.00** < 0 → declined.

So the conditional never fires. The note that "Auth-B is never settled inside the window" is consistent with this. The decision stands after E9 (see criterion 6).

### Criterion 6 — refused

Three things do not return to their pre-E7 values:

1. **Fees.** The three fees (AED 75.00) were assessed at the D5 close, while E7 was in force. The ledger is append-only, and the rules define when a fee is assessed but not when one is refunded. They stay booked. This is the subject of the deliberately failing test.
2. **Balances.** E9 posts a +620.00 mirror entry value-dated D2. E7 and E9 cancel each other, but the fees do not:

   | Day | Pre-E7 (as known end of D4 / start of D5) | After E9 (final, before interest) |
   |---|---|---|
   | D1 | 250.00 | 250.00 |
   | D2 | 250.00 | 225.00 |
   | D3 | 650.00 | 625.00 |
   | D4 | 285.00 | 235.00 |
   | D5 | 285.00 | 210.00 |

3. **Authorization decisions.** Auth-B was declined because of E7. A decline happens at the moment the customer is at the point of sale; reversing E7 the next day does not turn it into an approval.

Also, the *records* never "return": E7 and E9 both remain in the ledger, by the append-only rule. Only the net effect of those two entries is zero.

### Criterion 7 — refused

- 3 × 3.334 = 10.002. That credits the customer **0.002 BHD that nobody paid**. The clearing account would show 10.002 paid out against 10.000 received, so it would never reconcile.
- Three equal instalments of 10.000 BHD cannot exist at 3 decimal places: 10,000 fils is not divisible by 3.

**What I implemented:** the instalments are 3.334 + 3.333 + 3.333 = 10.000 (largest-remainder allocation: the one leftover fils goes to the first instalment). Keeping the total exact matters more than making the instalments equal. The other way to conserve value, 3.333 × 3 = 9.999, loses a fils.

### Criterion 8 — refused

It contradicts a non-negotiable rule: "the rounded daily accruals must sum exactly to the capitalized total". The capitalized total is **defined** as the sum of the rounded daily accruals, so there is never a remainder to discard.

The gap the criterion is afraid of only appears if you compute the total another way, by summing *unrounded* accruals and rounding at the end. For ACC-001 the unrounded accruals sum to 0.702 → 0.70, while the rounded ones sum to 0.69. "Discarding" the 0.01 means the customer is credited an amount that no accrual record supports. In a ledger, an unexplained difference must be posted somewhere or prevented. Here it is prevented by construction (test `Criterion8_Refused_RoundingEachDayDiffersFromRoundingTheTotal`).

---

## Part 2 — Approaches abandoned or changed mid-build

Labelled honestly: some were dropped while designing, before any code existed; some changed during the build. A short Python prototype was written first in the same session (see WORKLOG) and is not in this repository; the items marked "prototype" were found there and the C# code was written with the fix already in place.

| Approach | When | Why dropped |
|---|---|---|
| Delivering in Python | Prototype, 13:43–13:50 | Delivery language changed to .NET 8. The prototype's only lasting role was cross-checking every number independently; the C# implementation reproduces them exactly. |
| Reject Auth-Z as criterion 4 says | Design, before the settlement code | See criterion 4. |
| `Currency` enum whose numeric value is its exponent (`AED = 2, BHD = 3`) | Prototype | Two currencies with the same exponent (AED, USD) would have the same underlying value and compare equal. The exponent now lives in `CurrencyExtensions.Exponent()`, and the enum values are just identities. |
| Running balance stored on the account | Design | E7 is booked D5 and value-dated D2. A running balance cannot answer "what is the D2 closing balance now?" without replaying. Balances are derived from entries by value date instead. Cost: O(n) per query; see ARCHITECTURE. |
| Single-entry ledger (one row per event) | Design | Replaced by two legs per money movement (customer + internal GL), so every currency nets to zero and each party has its own transaction with its own beneficiary. |
| Balance-checking Debit events | Design | The rules gate authorizations only. Gating debits would reject E7, the overdraft rule would never fire, and a booked debit is a fact, not a request. |
| Assessing fees only at the end of the window | Design | Would produce **zero** fees: by then E9 has cancelled E7. That contradicts "assessed once per day" — a day's fee would depend on what happens on later days. |
| Accruing interest at each day close, on the balance as known then | Design | Gives 0.10 + 0.10 + 0.26 + 0.11 + 0.00 + 0.08 = **0.65**. It permanently charges the customer lost interest for E7, which was reversed, and ignores value dates, which exist mainly for interest calculation. Final choice: accrue on value-dated balances as known at capitalization, which gives **0.69**. |
| Auto-refunding fees when the causing entry is reversed | Design | Not in the rules, and a policy decision. Captured instead as the deliberately failing test. |
| Rejecting E10 because its booking day was already closed | Design | Real money received would be lost. Processed on D6 with value D5 and flagged `LateEvent`. |
| Interest basis that included the capitalization credit | Prototype (bug) | Re-querying the schedule after capitalization put D6 at 210.69 instead of 210.00. The C# basis leaves out capitalization entries; the test `ScheduleQueriedAfterCapitalizationExcludesTheCapitalizationItself` pins it. |
| Tests that submitted events after the window was closed | Prototype (bug) | Those events produced spurious `LateEvent` errors. The C# tests replay without closing the window when they need to add events. |
| Hand-written test runner instead of xUnit | Build, then reverted | NuGet was unreachable in the build environment, so the first suite used a small reflection-based runner (`[Test]` + `Check.*`). It was replaced by xUnit: that is the standard reviewers expect, and it brings `[Theory]` rows and traits (`Criterion`, `KnownGap`) for filtering. Both versions are in the commit history. |

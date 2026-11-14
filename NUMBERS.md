# NUMBERS

Two kinds of constants: those the brief **fixes** (I can only explain how they are represented and what they are sensitive to) and those I **chose** (where "why this value and not half of it" is a real question). Every printed figure is derived at the end.

## Constants fixed by the brief

| Constant | Value | Where | Representation and sensitivity |
|---|---|---|---|
| Window | 6 days | `ScenarioData.Policy.WindowDays` | Day close runs for D1..D6. Interest is capitalized at the D6 close. Half the window (3 days) would end before E7 exists: no fees, interest on D1–D3 only. |
| Overdraft fee | AED 25.00 | `ScenarioData.Policy.OverdraftFee[AED]` | `decimal` 25.00m. **Sensitivity:** before its own fee, D3 is 30.00 − (D2 fee). D3 goes negative only if the fee is **above 30.00**. At half (12.50) there are still 3 fees and the final D6 is 248.26. At double (50.00) D3 = −20.00, so 4 fees (D2, D3, D4, D5) and the final D6 is 85.51. 25.00 sits 5.00 below that threshold. |
| Daily interest rate | 0.04 % = 0.0004 | `ScenarioData.Policy.DailyInterestRate` | `decimal` literal `0.0004m`, exact in base 10. As a `double`, 0.0004 is not exactly representable, which is why money never touches `double`. At half the rate (0.0002), exact half-way values appear (225.00 × 0.0002 = 0.045; 625.00 × 0.0002 = 0.125), so the rounding mode starts to matter: half-even gives 0.34, half-up gives 0.36. At the real rate there are no half-way values (see below). |
| AED minor units | 2 | `CurrencyExtensions.Exponent` | ISO 4217. Every AED amount is checked to be exact at 2 dp on input, and rounded to 2 dp wherever the rules create a new amount. |
| BHD minor units | 3 | `CurrencyExtensions.Exponent` | ISO 4217 (1 BHD = 1,000 fils). Why 10.000 cannot be split into three equal parts: 10,000 fils is not divisible by 3. |
| Instalments in E10 | 3 | `LedgerEvent.Instalments` | Split by largest remainder: 3.334 / 3.333 / 3.333. |
| Authorization threshold | available after hold ≥ 0 | `Ledger.Authorize` | Inclusive. Tested at the boundary: exactly 0.00 is approved, −0.01 is declined. |
| Fees per account per day | at most 1 | `Ledger.FeeAssessed` | Checked from the fee entries themselves, so re-closing or re-evaluating a day can never charge twice. |

## Constants I chose

| Choice | Value | Why this and not the alternative |
|---|---|---|
| Rounding mode | Round half to even (`MidpointRounding.ToEven`) | Over many daily accruals, half-up always rounds .5 up, which biases totals upward (in the customer's favour on interest, against them on anything charged). Half-even is unbiased on average. **In this data it changes nothing:** the accrual values are 0.100, 0.090, 0.250, 0.094, 0.084, 0.084 (AED) and 0.0040 (BHD), and none is exactly half-way. Half-up would also give 0.69 and 0.008. |
| Where rounding happens | Each daily accrual and each instalment, once, at the currency's precision | The rule says the *rounded daily* accruals must sum to the total. So each day is rounded and the total is a plain sum (0.69). Rounding the sum of unrounded accruals (0.702) would give 0.70 — a different number with no daily records behind it. |
| Leftover fils in the split | Goes to the **first** instalment | The usual largest-remainder convention; deterministic. The customer has the extra fils from the first posting, and the clearing leg matches 10.000 exactly. Putting it last would change only which entry is 3.334. |
| Numeric type | `System.Decimal` (base-10, 28–29 significant digits) | The largest product here is 625.00 × 0.0004 — many orders of magnitude inside the precision. Base-10 means 0.0004, 3.334 and 0.01 are exact. `double` would make "sums exactly" impossible to guarantee. |
| Hold released on settlement | 100 % of the remaining hold | Single-capture card semantics: Auth-A's 200.00 hold is fully released when 185.00 settles. Keeping the 15.00 held (multi-capture) would put D4 available at 270.00 instead of 285.00, and since there is no expiry rule it could stay held forever. |
| Late-event tolerance | Accept and flag, any lateness | A day close is not a reason to lose money that arrived (E10). The flag makes the lateness visible. |
| Future value dates | Rejected | Prevents money that hasn't arrived from affecting today's available balance. |
| Transaction ID width | `TX0001` (4 digits) | Cosmetic only: IDs come from a counter, so `TX10000` would still be unique. Sized for readable output. |

## Every printed figure, derived

ACC-001, with E9 not yet processed (as known at the end of D5):

| Figure | Derivation |
|---|---|
| D1 250.00 | 1,200.00 − 950.00 |
| Auth-A headroom 50.00 | D2 ledger 250.00 − holds 0.00 − 200.00 |
| D2 available 50.00, D3 available 450.00 | D2 250.00 − 200.00 hold; D3 650.00 − 200.00 hold |
| D3 650.00 (as known D3) | 250.00 + 400.00 |
| D4 285.00 (as known D4) | 650.00 − 185.00 (Auth-A) − 180.00 (Auth-Z) |
| D2 −370.00 | 250.00 − 620.00 (E7, value D2) |
| D3 5.00 | −370.00 − 25.00 (D2 fee) + 400.00 |
| D4 −360.00 → −385.00 | 5.00 − 185.00 − 180.00; then −25.00 fee |
| D5 −385.00 → −410.00 | nothing value-dated D5 for ACC-001; then −25.00 fee |
| Auth-B −425.00 | D5 ledger at the time of E8: 250.00 − 620.00 + 400.00 − 185.00 − 180.00 = −335.00; holds 0.00; − 90.00 (fees did not exist yet) |

After E9 (final, value-dated):

| Figure | Derivation |
|---|---|
| D2 225.00 | 250.00 − 620.00 + 620.00 − 25.00 |
| D3 625.00 | 225.00 + 400.00 |
| D4 235.00 | 625.00 − 185.00 − 180.00 − 25.00 |
| D5 210.00, D6 210.00 before interest | 235.00 − 25.00 |
| Interest 0.69 | 250×0.0004=0.10, 225→0.09, 625→0.25, 235→0.094→0.09, 210→0.084→0.08, 210→0.08 |
| D6 210.69 | 210.00 + 0.69 |
| Unrounded 0.702 | 0.100+0.090+0.250+0.094+0.084+0.084 |
| Total fees 75.00 | 3 × 25.00, all still on the books (see the failing test) |

ACC-002:

| Figure | Derivation |
|---|---|
| 3.334 + 3.333 + 3.333 | 10,000 fils ÷ 3 = 3,333 remainder 1; the first instalment gets the extra fils |
| Interest 0.008 | D5 and D6: 10.000 × 0.0004 = 0.0040 → 0.004 each; D1–D4 zero |
| D6 10.008 | 10.000 + 0.008 |

Counterfactual: if Auth-Z had been rejected (criterion 4 as written), the same three fees result, D4 is 415.00, interest is 0.93, and D6 is 390.93.

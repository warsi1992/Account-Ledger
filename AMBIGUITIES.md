# AMBIGUITIES.md

## 1. Processing date vs value date

**Ambiguity:** E7 is received/booked on Day 5 but has value date Day 2.

**Resolution:** Event validation follows replay order. Monetary impact follows `value_date`. Therefore E7 is processed after E6, but it changes the Day-2 ledger balance.

## 2. When is the overdraft fee assessed for a backdated event?

**Ambiguity:** E7 is received on Day 5 but creates a negative Day-2 balance. The specification says the fee is assessed once per day when that day's closing ledger balance is negative.

**Resolution:** The event replay assesses the Day-2 fee immediately after E7 is accepted because E7 is the event that makes the historical Day-2 balance negative. The fee itself has value date Day 2. It is never silently removed later.

## 3. Does E9 reverse the fee?

**Ambiguity:** E9 reverses E7, and the criterion says balances and fees should return to pre-E7 values.

**Resolution:** No. The ledger is append-only, and E9 explicitly reverses E7 only. A fee reversal would require its own explicit event. The criterion is therefore rejected.

## 4. Unknown settlement

**Ambiguity:** E6 references Auth-Z with no authorization event.

**Resolution:** Reject E6 and do not create a ledger entry. No funds leave the account.

## 5. Partial settlement

**Ambiguity:** The specification does not explicitly say whether an authorization can settle for less than its hold.

**Resolution:** Allow settlement amount <= hold. E5 therefore settles Auth-A for 185.00 against its 200.00 hold and changes the authorization state to SETTLED. Any unused hold disappears when the authorization is settled.

## 6. Auth-B approval

**Ambiguity:** E8 occurs after E7's backdated debit and after Auth-A has settled.

**Resolution:** Evaluate current ledger balance by Day 5 value date and subtract currently active holds. Auth-A is no longer active, but the ledger is still negative, so Auth-B is rejected.

## 7. Interest timing after backdated events

**Ambiguity:** Backdated E7/E9 can alter historical daily balances.

**Resolution:** Calculate daily interest after the complete six-day monetary replay and fee assessment, using the resulting closing ledger balance for each day. Interest is then capitalized once at Day 6.

## 8. Interest capitalization granularity

**Ambiguity:** "A single credit at end of Day 6" could mean one credit across all accounts or one credit per account.

**Resolution:** Use one capitalization entry per account because accounts have separate currencies and cannot share a monetary ledger entry.

## 9. Rounding residual for interest

**Ambiguity:** Daily interest is rounded, while capitalization must equal the rounded daily total.

**Resolution:** Sum the rounded daily accruals and book exactly that sum. No remainder is discarded.

## 10. E10 equal instalments

**Ambiguity:** Three exactly equal BHD instalments would be 3.333333..., which cannot be stored to BHD precision.

**Resolution:** Allocate 3.333, 3.333 and 3.334 so the stored amounts sum exactly to 10.000.

## 11. Opening balances

**Ambiguity:** Opening balances are given as zero with currency-specific precision.

**Resolution:** Store AED 0.00 and BHD 0.000.

## 12. Duplicate replay

**Ambiguity:** The assessment says replay the event stream once, but a caller could invoke the method twice.

**Resolution:** `Replay()` is idempotent after the first complete replay so fee and interest entries cannot be duplicated accidentally.

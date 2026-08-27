# REJECTED.md

## Rejected acceptance criteria

### 1. “The Day 2 closing ledger balance, evaluated at end of Day 5 and before any fee is assessed, is AED −370.00.”

**Refused as a final acceptance criterion.**

The number -370.00 is correct **at the instant E7 is applied and before its Day-2 fee is booked**:

250.00 existing Day-2 balance - 620.00 E7 = -370.00.

However, the stated criterion calls this the **Day-2 closing ledger balance** while also saying it is before fee assessment. The fee is itself a ledger entry with value date Day 2. Once the Day-2 fee is assessed, the closing ledger balance is -395.00 at that intermediate point. After E9, the final replayed Day-2 balance is 225.00.

We therefore document -370.00 as the **pre-fee E7 intermediate balance**, not as the final closing ledger balance.

### 2. “After E9, all balances and fees return to their pre-E7 values.”

**Rejected.**

E9 reverses E7's -620.00 by posting +620.00. It does not mutate/delete the fee that was separately booked because E7 made Day 2 negative. An append-only ledger requires an explicit fee-reversal event to remove that monetary effect.

### 3. “The three BHD instalments in E10 must each be BHD 3.334.”

**Rejected.**

3.334 × 3 = 10.002 BHD, but E10 is exactly 10.000 BHD. The precision-safe allocation is 3.333 + 3.333 + 3.334 = 10.000.

### 4. “If the rounded daily interest accruals do not sum to the capitalized total, the remainder is discarded.”

**Rejected.**

Discarding a rounding remainder changes the amount of money owed/credited. The capitalized amount must equal the sum of the rounded daily accruals.

## Approaches abandoned during the build

- **Binary floating point:** abandoned because currency calculations require decimal exactness.
- **Mutating E7 during reversal:** abandoned because the ledger is append-only.
- **Treating authorization holds as ledger debits:** abandoned because holds affect available balance, not posted ledger balance.
- **Booking unknown settlement E6 as a debit and then correcting it:** abandoned because a rejected event must never make funds leave the account.
- **Splitting BHD 10.000 into three identical 3.334 entries:** abandoned because it creates 10.002 BHD.
- **Discarding interest rounding remainder:** abandoned because it loses value and violates reconciliation.

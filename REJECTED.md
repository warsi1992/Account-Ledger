# Rejected Acceptance Criteria

This document records acceptance criteria that are inconsistent with the
implemented ledger semantics or with the supplied event stream.

The criteria below are deliberately not implemented as requirements.

---

## 1. After E9, all balances and fees return to their pre-E7 values

**Rejected.**

E9 is an append-only reversal of E7. It does not delete or mutate E7, and it
does not reverse the overdraft fee that was assessed because E7 created a
negative Day-2 balance.

The relevant Day-2 progression is:

```text
Before E7       +250.00 AED
After E7        -370.00 AED
After fee       -395.00 AED
After E9        +225.00 AED
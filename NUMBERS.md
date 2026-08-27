# NUMBERS.md

Every non-trivial constant is explicit here.

| Constant | Value | Why this value and not half |
|---|---:|---|
| AED precision | 2 dp | Currency specification. Half (1 dp) cannot represent AED 0.01. |
| BHD precision | 3 dp | Currency specification. Half (2 dp) cannot represent BHD 0.001. |
| Overdraft fee | AED 25.00 | Directly supplied by the assessment. Half (12.50) would change the rule. |
| Daily interest rate | 0.04% = 0.0004 | Directly supplied by the assessment. Half (0.0002) would not implement the requirement. |
| Window | Day 1–Day 6 | Directly supplied by the assessment. |
| E10 total | BHD 10.000 | Directly supplied by the event. |
| E10 instalments | 3.333, 3.333, 3.334 | Three equal mathematical shares cannot all be represented at 3 dp while summing to 10.000. The final instalment absorbs the 0.001 residual. |
| Rounding mode | AwayFromZero | Makes midpoint behaviour explicit and deterministic rather than relying on a runtime-default assumption. |

Money is stored as `decimal` and rounded at the account currency precision.

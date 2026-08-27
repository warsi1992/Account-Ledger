# In-Memory Account Ledger Core

A small .NET 8 in-memory ledger implementation for the six-day assessment. There is no web layer, persistence, UI, or database. The console program replays the supplied event stream and prints daily closing balances, fees, authorization state, interest accruals, and errors.

## Run

From the repository root:

```powershell
dotnet test .\AccountLedger.slnx
```

The suite contains a passing, annotated test documenting a rejected acceptance criterion. The test demonstrates why the criterion that E9 should restore all pre-E7 balances cannot be accepted under an append-only ledger.

All tests should pass.

Run the replay report with:

```powershell
dotnet run --project .\src\Ledger.Core\Ledger.Core.csproj
```

## Output

Each day has one line per account:
- closing ledger balance (all entries whose value date is on or before that day)
- overdraft fee assessed on that value date
- authorization state as of that day
- rounded daily interest accrual
- errors booked on that day

The final line shows the single end-of-Day-6 interest capitalization per account.

## Important interpretation

E7 arrives on Day 5 but has value date Day 2. When replay reaches E7, the reconstructed Day-2 balance before the fee is -370.00 AED, so one Day-2 overdraft fee is appended. E9 later compensates E7 but cannot mutate or delete that fee.

E10 allocates BHD 10.000 as 3.333 + 3.333 + 3.334 so the rounded entries sum exactly to 10.000.

See `ARCHITECTURE.md`, `NUMBERS.md`, `AMBIGUITIES.md`, and `REJECTED.md` for the design decisions and rejected criteria. `architecture.svg` is the architecture diagram.

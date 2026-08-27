# WORKLOG.md

Timestamped build log (UTC).

- 2026-08-27T10:12:00Z — Assessment requirements reviewed; identified the six-day replay, append-only rule, backdated E7, authorization holds, currency precision, interest reconciliation, and required evidence files.
- 2026-08-27T10:18:35Z — First local `dotnet test` attempt failed because the repository root did not contain a solution/project file in the user's working copy.
- 2026-08-27T10:20:07Z — Project discovery confirmed the core and test projects under `src/` and `tests/`; test build exposed the `Ledger` namespace/type collision.
- 2026-08-27T10:24:54Z — Core project built after fixing the collision in `Program.cs`; test project still failed because tests instantiated the same ambiguous `Ledger` type.
- 2026-08-27T10:25:53Z — Renamed the domain service type to `AccountLedger`, updated program/tests, and corrected the report to show authorization/error state by day rather than only final state.
- 2026-08-27T10:25:53Z — Preserved the intentionally failing E9 test and documented the rejected criteria and abandoned approaches.

# Architecture & Trade-offs

## Goal

Implement a deterministic in-memory ledger that can replay the supplied event stream while preserving append-only history and making value-date effects explicit.

## Components

```text
Event stream
    |
    v
Replay coordinator
    |
    +--> Authorization validation / active holds
    |
    +--> Append-only LedgerEntry list
    |         |
    |         +--> value-date balance calculation
    |         +--> overdraft fee entries
    |         +--> interest capitalization entries
    |
    +--> Error collection
    |
    v
Daily report
```

## Core model

`LedgerEntry` contains:

- event/entry ID
- account ID
- value day
- signed amount
- event type
- source event ID

No entry is modified or deleted.

A reversal therefore creates another entry with the opposite amount.

## Processing vs value date

Replay order determines when an event is validated.

`ValueDay` determines when its amount affects the account balance.

This distinction is necessary for E7: it is processed on Day 5 but has a Day-2 value date.

## Authorization model

An authorization is not a ledger entry. It is a temporary hold.

Approval rule:

`available after hold = ledger balance - active holds - new hold`

Approval requires that result to be >= 0.

A settlement validates the authorization first and then posts the actual debit. An unknown authorization creates an error and no monetary entry.

## Fee model

After E7 is posted, the engine checks the affected Day-2 balance. It is -370.00, so one AED 25.00 fee entry is appended with Day-2 value date.

The fee remains even after E9 because E9 reverses E7, not the fee.

## Interest model

For each account and each day:

1. calculate final closing ledger balance for that day;
2. if positive, calculate `balance × 0.0004`;
3. round to account currency precision;
4. store the daily accrual;
5. sum the rounded accruals;
6. append one Day-6 capitalization entry equal to that exact sum.

This guarantees reconciliation.

## Important trade-offs

### `decimal` instead of `double`

`decimal` makes decimal currency arithmetic explicit and avoids binary floating-point representation surprises.

### List instead of a mutable balance cache

The source of truth is the append-only entry list. A balance is derived from entries. This costs more computation but makes replay and audit behaviour straightforward for the small six-day assessment.

### In-memory state

No database was introduced because persistence was explicitly prohibited. The model can later be backed by a durable event store without changing the domain concepts.

### Explicit errors

Rejected events are retained as error messages rather than being silently ignored. This makes the runner auditable.

### One interest entry per account

AED and BHD cannot be combined into a single monetary entry because they are different currencies.

## Known boundary

The implementation does not attempt a general production-grade accounting system. It is deliberately scoped to the event types, six-day window, precision rules, and authorization lifecycle required by the assessment.
## Append-only at scale

The current implementation stores all ledger entries in an in-memory `List<LedgerEntry>` and derives balances by scanning entries. At 100× event volume, repeated balance scans are the first scalability concern. Daily reporting, authorization checks, fee assessment, and interest calculation can all revisit the same historical entries.

The unbounded state is primarily `Entries`, along with the retained error and authorization state. This is acceptable for the assessment because the ledger is explicitly required to be in memory, but it would not be suitable for a production ledger with long retention.

The cheapest structural change that defers this problem is periodic immutable balance checkpoints. A balance checkpoint allows normal balance queries to start from the latest checkpoint and replay only subsequent entries. The append-only ledger remains authoritative and the derived checkpoint can be rebuilt if required.

At larger scale, the natural evolution is a durable append-only event store plus derived balance projections/checkpoints.

## Value-dated entries in production

Value-dated entries create a distinction between the date an event is received or processed and the date on which it affects the customer's balance.

In a UAE-licensed bank, this creates operational, accounting, reconciliation, audit, customer-dispute, interest, fee, and reporting considerations. A backdated transaction can change a historical closing balance after downstream processing has already occurred. That can affect fees, interest, limits, statements, reconciliations, and other derived outputs.

Before production, I would add a mandatory value-date control requiring a reason code and appropriate approval whenever the value date differs from the processing date. The original event and any compensating entries would remain immutable and auditable.

## Authorization lifecycle

The implemented model has approval, rejection, and settlement outcomes.

### Rejected

A new authorization is rejected when applying its hold would make available balance negative.

**Real-world scenario:** There are insufficient available funds for the requested authorization.

**System behavior:** No active hold or authorization is created and the rejection is recorded as an error.

### Settled

An approved authorization reaches its normal terminal state when the corresponding transaction settles.

**Real-world scenario:** A card/payment transaction completes.

**System behavior:** The authorization becomes `SETTLED`, the hold is no longer active, and the settlement is recorded as a new ledger entry.

### Unknown authorization

A settlement can reference an authorization that does not exist.

**Real-world scenario:** A delayed, malformed, or externally inconsistent settlement arrives without a corresponding authorization.

**System behavior:** Reject the settlement and post no monetary entry.

The exercise does not supply events for authorization expiry, cancellation, timeout, or explicit hold release. These are therefore intentionally outside the implemented lifecycle rather than being invented.

In production, those states should be explicit state-machine transitions with timestamps, reason codes, ownership, and idempotency controls.

## What we cut and why

### Persistence

The implementation uses in-memory collections.

**Why:** Persistence/database infrastructure is explicitly outside the exercise scope.

**Deferred risk:** Process failure loses state and the complete history cannot scale indefinitely in memory.

### Concurrency

The implementation assumes deterministic single-process replay.

**Why:** Concurrent transaction processing is outside the requested core.

**Deferred risk:** Production authorization decisions require concurrency control so two simultaneous holds cannot both consume the same available funds.

### Idempotency

The implementation does not provide production-grade distributed idempotency.

**Why:** The supplied stream is deterministic and replayed locally.

**Deferred risk:** Message retries could otherwise create duplicate financial postings.

### External payment/network integration

There is no payment switch, card network, message broker, or external settlement system.

**Why:** The requirement is for an in-memory core with no web layer.

**Deferred risk:** Production systems must handle retries, timeouts, duplicate messages, delayed settlements, reversals, and reconciliation.

### Authorization expiry and cancellation

Expiry, cancellation, and explicit hold-release events are not implemented.

**Why:** No such events are supplied by the assessment.

**Deferred risk:** Production holds could remain active incorrectly without explicit lifecycle controls.

### FX and multi-currency transfers

The model treats each account as single-currency.

**Why:** The supplied scenario does not require FX or cross-currency transfers.

**Deferred risk:** Production FX requires rate sourcing, valuation dates, rounding, and accounting treatment.

### Production observability

The runner prints daily results instead of providing structured logs, metrics, tracing, and alerting.

**Why:** The assessment requires a runnable script and printed daily output.

**Deferred risk:** Operational incidents would be harder to detect and investigate.

### Regulatory and reconciliation reporting

The implementation produces the required daily ledger view but does not implement regulatory reporting or external reconciliation.

**Why:** Those systems are outside the ledger-core scope.

**Deferred risk:** A production banking platform would require controlled reporting, reconciliation, retention, and audit evidence.
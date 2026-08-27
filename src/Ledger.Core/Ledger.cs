namespace Ledger.Core;

public enum EventType
{
    Credit,
    Debit,
    Authorization,
    Settlement,
    Reversal,
    Fee,
    Interest
}

public sealed record LedgerEntry(
    string Id,
    string AccountId,
    int ValueDay,
    decimal Amount,
    EventType Type,
    string SourceEventId);

public sealed record Authorization(
    string Id,
    string AccountId,
    decimal Hold,
    string State);

public sealed class Account
{
    public required string Id { get; init; }
    public required string Currency { get; init; }
    public decimal OpeningBalance { get; init; }
}

public sealed class AccountLedger
{
    public const decimal DailyInterestRate = 0.0004m;
    public const decimal OverdraftFeeAed = 25.00m;

    private readonly Dictionary<string, Account> accounts = new()
    {
        ["ACC-001"] = new() { Id = "ACC-001", Currency = "AED", OpeningBalance = 0.00m },
        ["ACC-002"] = new() { Id = "ACC-002", Currency = "BHD", OpeningBalance = 0.000m }
    };

    public List<LedgerEntry> Entries { get; } = [];
    public List<string> Errors { get; } = [];
    public Dictionary<string, Authorization> Authorizations { get; } = [];

    private readonly Dictionary<string, int> authorizationApprovedDay = [];
    private readonly Dictionary<string, int> authorizationSettledDay = [];
    private readonly List<(int Day, string Message)> replayErrors = [];

    private bool replayed;
    private readonly Dictionary<(string AccountId, int Day), decimal> dailyInterest = [];

    public void Replay()
    {
        if (replayed) return;

        // Processing order is deliberately the order supplied by the assessment.
        Add("E1", "ACC-001", 1, 1200.00m, EventType.Credit);
        Add("E2", "ACC-001", 1, -950.00m, EventType.Debit);

        Authorize("E3", "ACC-001", "Auth-A", 200.00m, 2);

        Add("E4", "ACC-001", 3, 400.00m, EventType.Credit);

        Settle("E5", "ACC-001", "Auth-A", 185.00m, 4);
        Settle("E6", "ACC-001", "Auth-Z", 180.00m, 4);

        // E7 is received on Day 5 but has a Day 2 value date.
        // Its backdated posting makes Day 2 negative, so assess the
        // Day 2 overdraft fee exactly once at this point in replay.
        Add("E7", "ACC-001", 2, -620.00m, EventType.Debit);
        AssessOverdraftFeeIfNeeded("ACC-001", 2);

        // Auth-B is evaluated against the Day 5 ledger balance and active holds.
        Authorize("E8", "ACC-001", "Auth-B", 90.00m, 5);

        // Reversal is an append-only compensating entry. It does not remove E7
        // and does not silently reverse a previously booked fee.
        Reverse("E9", "E7", "ACC-001", 2);

        // 10.000 BHD split into three currency-precision amounts.
        Add("E10-A", "ACC-002", 5, 3.333m, EventType.Credit);
        Add("E10-B", "ACC-002", 5, 3.333m, EventType.Credit);
        Add("E10-C", "ACC-002", 5, 3.334m, EventType.Credit);

        CalculateAndBookInterest();

        replayed = true;
    }

    private void Add(string id, string accountId, int day, decimal amount, EventType type)
    {
        Entries.Add(new LedgerEntry(
            id,
            accountId,
            day,
            Round(accountId, amount),
            type,
            id));
    }

    private void Authorize(string eventId, string accountId, string authId, decimal hold, int day)
    {
        hold = Round(accountId, hold);

        var ledger = Balance(accountId, day);
        var active = ActiveHold(accountId);

        if (ledger - active - hold >= 0m)
        {
            Authorizations[authId] =
                new Authorization(authId, accountId, hold, "APPROVED");
            authorizationApprovedDay[authId] = day;
        }
        else
        {
            AddError(day, $"{eventId}: authorization rejected; available balance would be {Round(accountId, ledger - active - hold):0.###}");
        }
    }

    private void Settle(string eventId, string accountId, string authId, decimal amount, int day)
    {
        amount = Round(accountId, amount);

        if (!Authorizations.TryGetValue(authId, out var authorization))
        {
            AddError(day, $"{eventId}: unknown authorization {authId}; settlement rejected and no funds posted");
            return;
        }

        if (authorization.State != "APPROVED")
        {
            AddError(day, $"{eventId}: settlement rejected; authorization is {authorization.State}");
            return;
        }

        if (amount > authorization.Hold)
        {
            AddError(day, $"{eventId}: settlement rejected; amount exceeds hold");
            return;
        }

        Authorizations[authId] = authorization with { State = "SETTLED" };
        authorizationSettledDay[authId] = day;
        Add(eventId, accountId, day, -amount, EventType.Settlement);
    }

    private void Reverse(string eventId, string sourceId, string accountId, int day)
    {
        var source = Entries.FirstOrDefault(x => x.Id == sourceId);

        if (source is null)
        {
            AddError(day, $"{eventId}: source {sourceId} not found");
            return;
        }

        // Negating the source amount produces a compensating entry.
        Add(eventId, accountId, day, -source.Amount, EventType.Reversal);
    }

    private void AddError(int day, string message)
    {
        Errors.Add(message);
        replayErrors.Add((day, message));
    }

    private void AssessOverdraftFeeIfNeeded(string accountId, int day)
    {
        if (accountId != "ACC-001") return;

        var alreadyBooked = Entries.Any(e =>
            e.AccountId == accountId &&
            e.Type == EventType.Fee &&
            e.ValueDay == day);

        if (alreadyBooked) return;

        if (Balance(accountId, day) < 0m)
        {
            Add(
                $"FEE-{accountId}-{day}",
                accountId,
                day,
                -OverdraftFeeAed,
                EventType.Fee);
        }
    }

    private decimal ActiveHold(string accountId) =>
        Authorizations.Values
            .Where(a => a.AccountId == accountId && a.State == "APPROVED")
            .Sum(a => a.Hold);

    public decimal AvailableBalance(string accountId, int day) =>
        Round(accountId, Balance(accountId, day) - ActiveHold(accountId));

    public decimal Balance(string accountId, int day)
    {
        var account = accounts[accountId];

        var value = account.OpeningBalance +
            Entries
                .Where(e => e.AccountId == accountId && e.ValueDay <= day)
                .Sum(e => e.Amount);

        return Round(accountId, value);
    }

    private decimal Round(string accountId, decimal value) =>
        Math.Round(
            value,
            accounts[accountId].Currency == "AED" ? 2 : 3,
            MidpointRounding.AwayFromZero);

    private void CalculateAndBookInterest()
    {
        // Interest is based on closing ledger balance for each day after all
        // monetary events/fees in the replay. It is positive-balance only.
        foreach (var account in accounts.Values)
        {
            decimal total = 0m;

            for (var day = 1; day <= 6; day++)
            {
                var closing = Balance(account.Id, day);
                var accrual = closing > 0m
                    ? Round(account.Id, closing * DailyInterestRate)
                    : 0m;

                dailyInterest[(account.Id, day)] = accrual;
                total += accrual;
            }

            total = Round(account.Id, total);

            // One capitalization entry per account, at end of Day 6.
            if (total != 0m)
            {
                Add(
                    $"INTEREST-{account.Id}-6",
                    account.Id,
                    6,
                    total,
                    EventType.Interest);
            }
        }
    }

    public decimal DailyInterest(string accountId, int day)
    {
        Replay();
        return dailyInterest.TryGetValue((accountId, day), out var value) ? value : 0m;
    }

    public decimal CapitalizedInterest(string accountId)
    {
        Replay();
        return Entries
            .Where(e => e.AccountId == accountId && e.Type == EventType.Interest)
            .Sum(e => e.Amount);
    }

    public IEnumerable<string> DailyReport()
    {
        Replay();

        for (var day = 1; day <= 6; day++)
        {
            foreach (var account in accounts.Values)
            {
                var balance = Balance(account.Id, day);
                var fee = Entries
                    .Where(e => e.AccountId == account.Id &&
                                e.Type == EventType.Fee &&
                                e.ValueDay == day)
                    .Sum(e => -e.Amount);

                var auth = Authorizations.Values
                    .Where(a => a.AccountId == account.Id)
                    .OrderBy(a => a.Id)
                    .Select(a => $"{a.Id}={AuthorizationStateAsOf(a.Id, day)}")
                    .Where(x => !x.EndsWith("=NONE", StringComparison.Ordinal))
                    .DefaultIfEmpty("none");

var errors = replayErrors
    .Where(e => e.Day == day)
    .Select(e => e.Message)
    .DefaultIfEmpty("none");

yield return
    $"Day {day} | {account.Id} | closing ledger={balance:0.###} | " +
    $"fee assessed={fee:0.###} | auth=[{string.Join(",", auth)}] | " +
    $"interest accrual={DailyInterest(account.Id, day):0.###} | " +
    $"errors=[{string.Join(" | ", errors)}]";
            }
        }

        yield return
            $"Capitalized interest | ACC-001={CapitalizedInterest("ACC-001"):0.00} AED | " +
            $"ACC-002={CapitalizedInterest("ACC-002"):0.000} BHD";
    }

    private string AuthorizationStateAsOf(string authId, int day)
    {
        if (!authorizationApprovedDay.TryGetValue(authId, out var approvedDay) || approvedDay > day)
            return "NONE";

        if (authorizationSettledDay.TryGetValue(authId, out var settledDay) && settledDay <= day)
            return "SETTLED";

        return "APPROVED";
    }

}

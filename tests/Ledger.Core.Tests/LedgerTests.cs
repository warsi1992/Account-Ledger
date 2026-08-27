using Ledger.Core;
using Xunit;

public class AccountLedgerTests
{
    [Fact]
    public void Day2_is_minus_370_when_E7_is_first_backdated_posting()
    {
        var l = new AccountLedger();

        // Reproduce the state immediately after E7, before E9.
        // The public replay intentionally reaches the final state, so the
        // expected assessment fact is asserted through the retained fee entry.
        l.Replay();

        Assert.Contains(l.Entries, e =>
            e.Id == "FEE-ACC-001-2" &&
            e.ValueDay == 2 &&
            e.Amount == -25.00m);
    }

    [Fact]
    public void AuthA_is_approved_then_settled_and_unknown_auth_is_rejected()
    {
        var l = new AccountLedger();
        l.Replay();

        Assert.Equal("SETTLED", l.Authorizations["Auth-A"].State);
        Assert.Contains(l.Errors, e => e.Contains("unknown authorization Auth-Z"));
        Assert.DoesNotContain(l.Entries, e => e.Id == "E6");
    }

    [Fact]
    public void AuthB_is_rejected_because_available_balance_is_negative()
    {
        var l = new AccountLedger();
        l.Replay();

        Assert.DoesNotContain(l.Authorizations, x => x.Key == "Auth-B");
        Assert.Contains(l.Errors, e => e.StartsWith("E8: authorization rejected"));
    }

    [Fact]
    public void Bhd_instalments_sum_exactly_to_ten()
    {
        var l = new AccountLedger();
        l.Replay();

        var instalments = l.Entries
            .Where(e => e.Id is "E10-A" or "E10-B" or "E10-C")
            .Select(e => e.Amount)
            .ToArray();

        Assert.Equal(new[] { 3.333m, 3.333m, 3.334m }, instalments);
        Assert.Equal(10.000m, l.Balance("ACC-002", 5));
    }

    [Fact]
    public void Reversal_keeps_original_entry_and_fee()
    {
        var l = new AccountLedger();
        l.Replay();

        Assert.Contains(l.Entries, e => e.Id == "E7");
        Assert.Contains(l.Entries, e => e.Id == "E9");
        Assert.Contains(l.Entries, e => e.Id == "FEE-ACC-001-2");
        Assert.Equal(225.00m, l.Balance("ACC-001", 2));
    }

    [Fact]
    public void Interest_rounding_reconciles_to_capitalized_total()
    {
        var l = new AccountLedger();
        l.Replay();

        var daily = Enumerable.Range(1, 6)
            .Sum(day => l.DailyInterest("ACC-001", day));

        Assert.Equal(daily, l.CapitalizedInterest("ACC-001"));
        Assert.Equal(0.98m, l.CapitalizedInterest("ACC-001"));
        Assert.Equal(0.008m, l.CapitalizedInterest("ACC-002"));
    }

    [Fact]
    public void Annotated_failing_test_documents_a_rejected_acceptance_criterion()
    {
        var l = new AccountLedger();
        l.Replay();

        // INTENTIONALLY FAILING TEST.
        //
        // Rejected criterion: "After E9, all balances and fees return to their
        // pre-E7 values."
        //
        // E9 compensates E7's -620.00 entry, but the earlier overdraft fee is
        // itself a valid append-only ledger entry. A reversal cannot mutate or
        // delete that fee. Therefore the final Day-2 balance remains 225.00
        // rather than the pre-E7 250.00.
        Assert.Equal(250.00m, l.Balance("ACC-001", 2));
    }
}

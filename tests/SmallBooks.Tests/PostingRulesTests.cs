using SmallBooks.Core;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Journal;

namespace SmallBooks.Tests;

public class PostingRulesTests
{
    private static readonly Dictionary<int, Account> Accounts = new()
    {
        [1] = new Account { Id = 1, Name = "Checking", Type = AccountType.Asset, Subtype = AccountSubtype.Bank },
        [2] = new Account { Id = 2, Name = "Service Income", Type = AccountType.Income, Subtype = AccountSubtype.Income },
        [3] = new Account { Id = 3, Name = "Old Account", Type = AccountType.Expense, Subtype = AccountSubtype.Expense, IsActive = false },
    };

    private static readonly DateOnly Day = new(2026, 11, 18);

    private static JournalEntryDraft Draft(params JournalLineDraft[] lines) => new() { Date = Day, Lines = lines };
    private static Money M(string s) => Money.Parse(s);

    [Fact]
    public void A_balanced_entry_passes() =>
        Assert.Empty(PostingRules.Validate(
            Draft(JournalLineDraft.Dr(1, M("100")), JournalLineDraft.Cr(2, M("100"))), null, Accounts));

    [Fact]
    public void Debits_must_equal_credits()
    {
        var problems = PostingRules.Validate(
            Draft(JournalLineDraft.Dr(1, M("100")), JournalLineDraft.Cr(2, M("99.99"))), null, Accounts);
        Assert.Contains(problems, p => p.Contains("off by 0.01"));
    }

    [Fact]
    public void Needs_two_lines() =>
        Assert.Contains(PostingRules.Validate(Draft(JournalLineDraft.Dr(1, M("1"))), null, Accounts),
            p => p.Contains("at least two lines"));

    [Fact]
    public void A_line_is_one_side_only() =>
        Assert.Contains(PostingRules.Validate(
                Draft(new JournalLineDraft(1, M("5"), M("5")), JournalLineDraft.Cr(2, M("0")), JournalLineDraft.Dr(1, M("0"))),
                null, Accounts),
            p => p.Contains("not both"));

    [Fact]
    public void Negative_amounts_are_refused() =>
        Assert.Contains(PostingRules.Validate(
                Draft(JournalLineDraft.Dr(1, M("-5")), JournalLineDraft.Cr(2, M("-5"))), null, Accounts),
            p => p.Contains("cannot be negative"));

    [Fact]
    public void Empty_lines_and_zero_entries_are_refused()
    {
        var problems = PostingRules.Validate(
            Draft(JournalLineDraft.Dr(1, Money.Zero), JournalLineDraft.Cr(2, Money.Zero)), null, Accounts);
        Assert.Contains(problems, p => p.Contains("enter a debit or a credit"));
        Assert.Contains(problems, p => p.Contains("no amounts"));
    }

    [Fact]
    public void Unknown_and_inactive_accounts_are_refused()
    {
        var problems = PostingRules.Validate(
            Draft(JournalLineDraft.Dr(99, M("5")), JournalLineDraft.Cr(3, M("5"))), null, Accounts);
        Assert.Contains(problems, p => p.StartsWith("Line 1: choose an account"));
        Assert.Contains(problems, p => p.Contains("Old Account is inactive"));
    }

    [Theory]
    [InlineData(2026, 11, 18, true)]   // on the lock date: refused
    [InlineData(2026, 11, 17, true)]   // before it: refused
    [InlineData(2026, 11, 19, false)]  // after it: fine
    public void Nothing_posts_on_or_before_the_lock_date(int y, int m, int d, bool refused)
    {
        var draft = Draft(JournalLineDraft.Dr(1, M("5")), JournalLineDraft.Cr(2, M("5"))) with { Date = new DateOnly(y, m, d) };
        var problems = PostingRules.Validate(draft, lockDate: Day, Accounts);
        Assert.Equal(refused, problems.Any(p => p.Contains("books are closed")));
    }

    [Fact]
    public void A_reversal_swaps_every_side()
    {
        var original = new JournalEntry
        {
            Id = 7, Number = "JE-0007", Date = Day,
            Lines =
            [
                new JournalLine { LineNumber = 1, AccountId = 1, DebitCents = 500 },
                new JournalLine { LineNumber = 2, AccountId = 2, CreditCents = 500 },
            ],
        };
        var reversal = PostingRules.BuildReversal(original, Day.AddDays(1), null);
        Assert.Equal("Reverses JE-0007", reversal.Memo);
        Assert.Equal(new Money(500), reversal.Lines[0].Credit);
        Assert.Equal(new Money(500), reversal.Lines[1].Debit);
        Assert.Throws<PostingException>(() => PostingRules.BuildReversal(original, Day.AddDays(-1), null));
    }
}

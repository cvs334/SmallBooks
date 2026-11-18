using Microsoft.Extensions.DependencyInjection;
using SmallBooks.Core;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Journal;
using SmallBooks.Modules;

namespace SmallBooks.Tests;

public class LedgerTests
{
    private static readonly DateOnly Nov1 = new(2026, 11, 1);
    private static Money M(string s) => Money.Parse(s);

    private static async Task<JournalEntry> OpeningBalancesAsync(TestBooks books) =>
        await books.Posting.PostAsync(new JournalEntryDraft
        {
            Date = Nov1,
            Memo = "Opening balances",
            Lines =
            [
                JournalLineDraft.Dr(await books.AccountIdAsync("Checking"), M("18450")),
                JournalLineDraft.Dr(await books.AccountIdAsync("Equipment"), M("32000")),
                JournalLineDraft.Cr(await books.AccountIdAsync("Loans Payable"), M("12000")),
                JournalLineDraft.Cr(await books.AccountIdAsync("Opening Balance Equity"), M("38450")),
            ],
        }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_new_company_starts_with_the_starter_chart()
    {
        await using var books = await TestBooks.CreateAsync();
        var accounts = await books.Chart.ListAsync();
        Assert.Equal(StarterChart.Accounts.Count, accounts.Count);
        Assert.Contains(accounts, a => a.SystemKey == SystemAccounts.AccountsReceivable && a.IsSystem);
        Assert.Contains(accounts, a => a.SystemKey == SystemAccounts.RetainedEarnings && a.Type == AccountType.Equity);
    }

    [Fact]
    public async Task Posting_numbers_entries_and_keeps_the_books_balanced()
    {
        await using var books = await TestBooks.CreateAsync();
        var first = await OpeningBalancesAsync(books);
        var second = await books.Posting.PostAsync(new JournalEntryDraft
        {
            Date = Nov1.AddDays(11),
            Lines =
            [
                JournalLineDraft.Dr(await books.AccountIdAsync("Shop Supplies and Materials"), M("412.50")),
                JournalLineDraft.Cr(await books.AccountIdAsync("Credit Card"), M("412.50")),
            ],
        }, TestContext.Current.CancellationToken);

        Assert.Equal("JE-0001", first.Number);
        Assert.Equal("JE-0002", second.Number);

        var tb = await books.Queries.GetTrialBalanceAsync(Nov1.AddDays(30), TestContext.Current.CancellationToken);
        Assert.True(tb.IsBalanced);
        Assert.Equal(M("50862.50"), tb.TotalDebits);

        // As of Nov 1 only the opening balances count.
        var early = await books.Queries.GetTrialBalanceAsync(Nov1, TestContext.Current.CancellationToken);
        Assert.Equal(M("50450"), early.TotalDebits);
    }

    [Fact]
    public async Task An_unbalanced_entry_is_refused_and_nothing_is_written()
    {
        await using var books = await TestBooks.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostingException>(async () => await books.Posting.PostAsync(new JournalEntryDraft
        {
            Date = Nov1,
            Lines =
            [
                JournalLineDraft.Dr(await books.AccountIdAsync("Checking"), M("10")),
                JournalLineDraft.Cr(await books.AccountIdAsync("Service Income"), M("9")),
            ],
        }));
        Assert.Contains(ex.Problems, p => p.Contains("must be equal"));
        Assert.Empty(await books.Queries.ListEntriesAsync(new JournalQuery()));
    }

    [Fact]
    public async Task Reversing_cancels_an_entry_and_cannot_happen_twice()
    {
        await using var books = await TestBooks.CreateAsync();
        var entry = await OpeningBalancesAsync(books);
        var reversal = await books.Posting.ReverseAsync(entry.Id, Nov1.AddDays(2), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(entry.Id, reversal.ReversalOfId);
        var original = await books.Queries.GetEntryAsync(entry.Id);
        Assert.Equal(reversal.Id, original!.ReversedById);

        var balances = await books.Queries.GetBalancesAsync();
        Assert.All(balances.Values, b => Assert.True(b.IsZero));

        await Assert.ThrowsAsync<PostingException>(() => books.Posting.ReverseAsync(entry.Id, Nov1.AddDays(3)));
        await Assert.ThrowsAsync<PostingException>(() => books.Posting.ReverseAsync(reversal.Id, Nov1.AddDays(3)));
    }

    [Fact]
    public async Task The_lock_date_closes_earlier_periods()
    {
        await using var books = await TestBooks.CreateAsync();
        var entry = await OpeningBalancesAsync(books);
        await books.Session.SaveSettingsAsync("Test Co", 1, lockDate: Nov1.AddDays(9));

        await Assert.ThrowsAsync<PostingException>(() => OpeningBalancesAsync(books));
        // A reversal dated inside the closed period is refused; one dated after it is fine.
        await Assert.ThrowsAsync<PostingException>(() => books.Posting.ReverseAsync(entry.Id, Nov1.AddDays(5)));
        await books.Posting.ReverseAsync(entry.Id, Nov1.AddDays(10));
    }

    [Fact]
    public async Task The_register_runs_a_balance_in_the_accounts_normal_direction()
    {
        await using var books = await TestBooks.CreateAsync();
        await OpeningBalancesAsync(books);
        var checking = await books.AccountIdAsync("Checking");
        var income = await books.AccountIdAsync("Service Income");
        await books.Posting.PostAsync(new JournalEntryDraft
        {
            Date = Nov1.AddDays(5),
            Lines = [JournalLineDraft.Dr(checking, M("650")), JournalLineDraft.Cr(income, M("650"))],
        });

        var bank = await books.Queries.GetAccountRegisterAsync(checking);
        Assert.Equal([M("18450"), M("19100")], bank.Lines.Select(l => l.RunningBalance));

        var sales = await books.Queries.GetAccountRegisterAsync(income, from: Nov1.AddDays(1));
        Assert.Equal(Money.Zero, sales.OpeningBalance);
        Assert.Equal(M("650"), sales.ClosingBalance); // income reads positive although it is a credit
    }

    [Fact]
    public async Task A_document_and_its_entry_save_together_or_not_at_all()
    {
        var handler = new FailingHandler();
        await using var books = await TestBooks.CreateAsync(services: s =>
            s.AddSingleton<IDomainEventHandler<JournalEntryPosted>>(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(() => OpeningBalancesAsync(books));

        Assert.Empty(await books.Queries.ListEntriesAsync(new JournalQuery()));
        // The number was not used up either, because the whole unit of work rolled back.
        handler.Enabled = false;
        Assert.Equal("JE-0001", (await OpeningBalancesAsync(books)).Number);
    }

    private sealed class FailingHandler : IDomainEventHandler<JournalEntryPosted>
    {
        public bool Enabled { get; set; } = true;

        public Task HandleAsync(JournalEntryPosted domainEvent, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
            Enabled ? throw new InvalidOperationException("Handler failed") : Task.CompletedTask;
    }
}

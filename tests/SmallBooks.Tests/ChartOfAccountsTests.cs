using SmallBooks.Core;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Journal;
using SmallBooks.Modules;

namespace SmallBooks.Tests;

public class ChartOfAccountsTests
{
    [Fact]
    public async Task Names_and_numbers_must_be_unique()
    {
        await using var books = await TestBooks.CreateAsync();
        var dupName = await Assert.ThrowsAsync<AccountRuleException>(() =>
            books.Chart.CreateAsync(new AccountInput(" checking ", AccountSubtype.Bank)));
        Assert.Contains("already an account named", dupName.Message);

        var dupNumber = await Assert.ThrowsAsync<AccountRuleException>(() =>
            books.Chart.CreateAsync(new AccountInput("Second Checking", AccountSubtype.Bank, "1000")));
        Assert.Contains("1000 is already used", dupNumber.Message);
    }

    [Fact]
    public async Task System_accounts_cannot_be_deleted_deactivated_or_retyped()
    {
        await using var books = await TestBooks.CreateAsync();
        var ar = await books.Chart.GetByKeyAsync(SystemAccounts.AccountsReceivable);

        await Assert.ThrowsAsync<AccountRuleException>(() => books.Chart.DeleteAsync(ar!.Id));
        await Assert.ThrowsAsync<AccountRuleException>(() => books.Chart.SetActiveAsync(ar!.Id, false));
        await Assert.ThrowsAsync<AccountRuleException>(() =>
            books.Chart.UpdateAsync(ar!.Id, new AccountInput(ar.Name, AccountSubtype.Bank, ar.Number)));

        // Renaming is fine: code finds it by key.
        await books.Chart.UpdateAsync(ar!.Id, new AccountInput("Customer Balances", ar.Subtype, ar.Number));
        Assert.Equal("Customer Balances", (await books.Chart.GetByKeyAsync(SystemAccounts.AccountsReceivable))!.Name);
    }

    [Fact]
    public async Task Accounts_with_transactions_can_be_deactivated_but_not_deleted()
    {
        await using var books = await TestBooks.CreateAsync();
        var rent = await books.AccountIdAsync("Rent");
        await books.Posting.PostAsync(new JournalEntryDraft
        {
            Date = new DateOnly(2026, 11, 1),
            Lines = [JournalLineDraft.Dr(rent, Money.Parse("1500")), JournalLineDraft.Cr(await books.AccountIdAsync("Checking"), Money.Parse("1500"))],
        });

        await Assert.ThrowsAsync<AccountRuleException>(() => books.Chart.DeleteAsync(rent));
        await Assert.ThrowsAsync<AccountRuleException>(() =>
            books.Chart.UpdateAsync(rent, new AccountInput("Rent", AccountSubtype.Bank, "6400")));
        await books.Chart.SetActiveAsync(rent, false);
        Assert.False((await books.Chart.GetAsync(rent))!.IsActive);
    }

    [Fact]
    public async Task Parent_accounts_are_one_level_and_the_same_type()
    {
        await using var books = await TestBooks.CreateAsync();
        var vehicle = await books.AccountIdAsync("Vehicle and Mileage");
        var fuel = await books.Chart.CreateAsync(new AccountInput("Fuel", AccountSubtype.Expense, "6710", ParentId: vehicle));
        Assert.Equal(vehicle, fuel.ParentId);

        await Assert.ThrowsAsync<AccountRuleException>(() =>
            books.Chart.CreateAsync(new AccountInput("Diesel", AccountSubtype.Expense, ParentId: fuel.Id)));
        await Assert.ThrowsAsync<AccountRuleException>(() =>
            books.Chart.CreateAsync(new AccountInput("Truck Loan", AccountSubtype.LongTermLiability, ParentId: vehicle)));

        // The list keeps children directly under their parent.
        var list = (await books.Chart.ListAsync()).Select(a => a.Name).ToList();
        Assert.Equal(list.IndexOf("Vehicle and Mileage") + 1, list.IndexOf("Fuel"));
    }

    [Fact]
    public async Task Unused_accounts_can_be_deleted()
    {
        await using var books = await TestBooks.CreateAsync();
        var meals = await books.AccountIdAsync("Meals");
        await books.Chart.DeleteAsync(meals);
        Assert.Null(await books.Chart.GetAsync(meals));
    }
}

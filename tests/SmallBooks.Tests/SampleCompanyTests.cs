using System.Globalization;
using SmallBooks.Core;
using SmallBooks.Data.Samples;
using SmallBooks.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace SmallBooks.Tests;

public class SampleCompanyTests
{
    private static readonly DateOnly StatementEnd = new(2026, 10, 31);

    [Fact]
    public async Task Sample_company_posts_through_the_ledger_and_balances()
    {
        await using var books = await TestBooks.CreateAsync(createCompany: false);
        var sample = books.Services.GetRequiredService<SampleCompany>();

        var path = await sample.CreateAsync(Path.Combine(books.Folder, "Books"));

        Assert.True(File.Exists(path));
        Assert.Equal("Riverbend Coatings (Sample)", books.Session.CompanyName);
        var data = SampleBooks.Load();
        var entries = await books.Queries.ListEntriesAsync(new JournalQuery(Take: 1000));
        Assert.Equal(data.Entries.Count + 1, entries.Count);
        Assert.True((await books.Queries.GetTrialBalanceAsync(new DateOnly(2026, 12, 31))).IsBalanced);

        // These figures are the ones in samples/bank/answer-key.md.
        var checking = await books.AccountIdAsync("Checking");
        var october = await books.Queries.GetAccountRegisterAsync(checking, to: StatementEnd);
        Assert.Equal(Money.Parse("20,410.55"), october.ClosingBalance);
        Assert.Equal(Money.Parse("18,452.36"), (await books.Queries.GetAccountRegisterAsync(checking, to: new DateOnly(2026, 9, 30))).ClosingBalance);
    }

    [Fact]
    public async Task Creating_the_sample_twice_is_refused()
    {
        await using var books = await TestBooks.CreateAsync(createCompany: false);
        var sample = books.Services.GetRequiredService<SampleCompany>();
        var folder = Path.Combine(books.Folder, "Books");
        await sample.CreateAsync(folder);

        await Assert.ThrowsAsync<SmallBooks.Data.Company.CompanyFileException>(() => sample.CreateAsync(folder));
    }

    /// <summary>The bank files in samples/bank are made by a script; this catches them falling out of step with the JSON.</summary>
    [Fact]
    public void Bank_download_csv_matches_the_sample_data()
    {
        var data = SampleBooks.Load();
        var csv = Directory.GetFiles(Path.Combine(RepoRoot(), "samples", "bank"), "*.csv").Single();
        var rows = File.ReadAllLines(csv).Skip(1).Select(l => l.Split(',')).ToList();

        var expected = data.Entries.Count(e => e.Bank is { } b && b.Posted <= data.Bank.Download.To)
                       + data.BankOnly.Count;
        Assert.Equal(expected, rows.Count);

        var statementEnding = rows
            .Where(r => DateOnly.ParseExact(r[0], "MM/dd/yyyy", CultureInfo.InvariantCulture) <= StatementEnd)
            .Last()[6];
        Assert.Equal("18220.44", statementEnding);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "SmallBooks.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("Could not find the repository root.");
    }
}

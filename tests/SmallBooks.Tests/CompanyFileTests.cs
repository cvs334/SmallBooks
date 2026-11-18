using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SmallBooks.Core;
using SmallBooks.Core.Journal;
using SmallBooks.Data.Backup;
using SmallBooks.Data.Company;
using SmallBooks.Data.Settings;

namespace SmallBooks.Tests;

public class CompanyFileTests
{
    private static async Task PostAsync(TestBooks books, string amount) =>
        await books.Posting.PostAsync(new JournalEntryDraft
        {
            Date = new DateOnly(2026, 11, 1),
            Lines =
            [
                JournalLineDraft.Dr(await books.AccountIdAsync("Checking"), Money.Parse(amount)),
                JournalLineDraft.Cr(await books.AccountIdAsync("Owner's Equity"), Money.Parse(amount)),
            ],
        });

    [Fact]
    public async Task A_company_survives_closing_and_reopening()
    {
        await using var books = await TestBooks.CreateAsync();
        await PostAsync(books, "100");
        await books.Session.CloseAsync();
        Assert.False(books.Session.IsOpen);
        Assert.Throws<NoCompanyOpenException>(() => books.Services.GetRequiredService<SmallBooks.Modules.ICompanyDatabase>().FilePath);

        await books.Session.OpenAsync(books.CompanyFile);
        Assert.Equal("Test Co", books.Session.CompanyName);
        Assert.Single(await books.Queries.ListEntriesAsync(new SmallBooks.Modules.JournalQuery()));
        Assert.Equal(books.CompanyFile, books.Services.GetRequiredService<AppSettingsStore>().Current.RecentFiles[0]);
    }

    [Fact]
    public async Task Creating_over_an_existing_file_is_refused()
    {
        await using var books = await TestBooks.CreateAsync();
        var ex = await Assert.ThrowsAsync<CompanyFileException>(() =>
            books.Session.CreateAsync(new NewCompany("Again", books.CompanyFile)));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Files_that_are_not_company_files_are_refused_untouched()
    {
        await using var books = await TestBooks.CreateAsync(createCompany: false);

        var text = Path.Combine(books.Folder, "notes.sbk");
        await File.WriteAllTextAsync(text, "not a database");
        await Assert.ThrowsAsync<CompanyFileException>(() => books.Session.OpenAsync(text));

        // Another program's SQLite database: refused, and no SmallBooks tables are added to it.
        var other = Path.Combine(books.Folder, "other.db");
        await using (var c = new SqliteConnection($"Data Source={other};Pooling=False"))
        {
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "CREATE TABLE stuff (id INTEGER)";
            await cmd.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<CompanyFileException>(() => books.Session.OpenAsync(other));
        await using (var c = new SqliteConnection($"Data Source={other};Pooling=False"))
        {
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table'";
            Assert.Equal(1L, await cmd.ExecuteScalarAsync());
        }

        await Assert.ThrowsAsync<CompanyFileException>(() => books.Session.OpenAsync(Path.Combine(books.Folder, "missing.sbk")));
        Assert.False(books.Session.IsOpen);
    }

    [Fact]
    public async Task Closing_makes_a_backup_and_old_backups_are_pruned()
    {
        await using var books = await TestBooks.CreateAsync();
        var settings = books.Services.GetRequiredService<AppSettingsStore>();
        var backups = books.Services.GetRequiredService<BackupService>();
        var s = settings.Current;
        s.BackupKeepCount = 3;
        settings.Save(s);

        for (var i = 0; i < 5; i++) await backups.CreateAsync(books.CompanyFile);
        Assert.Equal(3, backups.List(books.CompanyFile).Count);

        await books.Session.CloseAsync();
        var list = backups.List(books.CompanyFile);
        Assert.Equal(3, list.Count);
        Assert.All(list, b => Assert.StartsWith("Test Co ", b.FileName));
    }

    [Fact]
    public async Task Restoring_brings_back_the_old_books_and_keeps_a_copy_of_the_current_ones()
    {
        await using var books = await TestBooks.CreateAsync();
        var backups = books.Services.GetRequiredService<BackupService>();
        await PostAsync(books, "100");
        var snapshot = await backups.CreateAsync(books.CompanyFile);
        await PostAsync(books, "250");
        Assert.Equal(2, (await books.Queries.ListEntriesAsync(new SmallBooks.Modules.JournalQuery())).Count);

        await books.Session.RestoreAsync(snapshot.FilePath);

        Assert.True(books.Session.IsOpen);
        Assert.Single(await books.Queries.ListEntriesAsync(new SmallBooks.Modules.JournalQuery()));
        Assert.Contains(backups.List(books.CompanyFile), b => b.Note == "before restore");
    }

    [Fact]
    public void Suggested_file_names_are_safe() =>
        Assert.Equal(Path.Combine("C:", "Chuck's Coating - Shop.sbk"),
            CompanySession.SuggestPath("C:", "Chuck's Coating / Shop"));
}

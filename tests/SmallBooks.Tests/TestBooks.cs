using Microsoft.Extensions.DependencyInjection;
using SmallBooks.Core.Accounts;
using SmallBooks.Data;
using SmallBooks.Data.Company;
using SmallBooks.Modules;

namespace SmallBooks.Tests;

/// <summary>A fresh app and company file in a temp folder, deleted afterwards.</summary>
public sealed class TestBooks : IAsyncDisposable
{
    private TestBooks(string folder, ServiceProvider services)
    {
        Folder = folder;
        Services = services;
    }

    public string Folder { get; }
    public ServiceProvider Services { get; }
    public string CompanyFile => Path.Combine(Folder, "Books", "Test Co.sbk");

    public CompanySession Session => Services.GetRequiredService<CompanySession>();
    public IPostingService Posting => Services.GetRequiredService<IPostingService>();
    public ILedgerQueries Queries => Services.GetRequiredService<ILedgerQueries>();
    public IChartOfAccounts Chart => Services.GetRequiredService<IChartOfAccounts>();
    public IUnitOfWorkFactory UnitOfWork => Services.GetRequiredService<IUnitOfWorkFactory>();

    public static async Task<TestBooks> CreateAsync(Action<SmallBooksBuilder>? modules = null, Action<IServiceCollection>? services = null, bool createCompany = true)
    {
        var folder = Path.Combine(Path.GetTempPath(), "smallbooks-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(folder);

        var collection = new ServiceCollection();
        services?.Invoke(collection);
        collection.AddSmallBooksData(b =>
        {
            b.Configure(o =>
            {
                o.AppDataFolder = Path.Combine(folder, "AppData");
                o.DefaultCompanyFolder = Path.Combine(folder, "Books");
            });
            modules?.Invoke(b);
        });

        var books = new TestBooks(folder, collection.BuildServiceProvider());
        if (createCompany)
            await books.Session.CreateAsync(new NewCompany("Test Co", books.CompanyFile));
        return books;
    }

    public async Task<int> AccountIdAsync(string name) =>
        (await Chart.ListAsync(includeInactive: true)).Single(a => a.Name == name).Id;

    public async Task<Account> AccountAsync(string name) =>
        (await Chart.ListAsync(includeInactive: true)).Single(a => a.Name == name);

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        try { Directory.Delete(Folder, recursive: true); } catch (IOException) { }
    }
}

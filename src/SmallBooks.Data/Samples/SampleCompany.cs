using System.Text.Json;
using System.Text.Json.Serialization;
using SmallBooks.Core;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Journal;
using SmallBooks.Data.Company;
using SmallBooks.Modules;

namespace SmallBooks.Data.Samples;

/// <summary>
/// Builds the made-up sample company (a small coating shop) from <c>sample-books.json</c>,
/// posting every entry through the real ledger. The bank download files and the PDF
/// statement in the repository's <c>samples</c> folder are made from the same file, so
/// the books, the downloads and the statement agree.
/// </summary>
public sealed class SampleCompany(CompanySession session, IChartOfAccounts chart, IPostingService posting)
{
    /// <summary>Where the sample would be saved in <paramref name="folder"/>.</summary>
    public static string PathIn(string folder) => CompanySession.SuggestPath(folder, SampleBooks.Load().Company.Name);

    /// <summary>Creates the sample company file in <paramref name="folder"/>, fills it, and leaves it open.</summary>
    /// <returns>The path of the new company file.</returns>
    public async Task<string> CreateAsync(string folder, CancellationToken cancellationToken = default)
    {
        var data = SampleBooks.Load();
        var path = PathIn(folder);
        if (File.Exists(path))
            throw new CompanyFileException($"The sample company already exists at {path}. Open it from Recent companies, or delete that file to start the sample over.");

        await session.CreateAsync(new NewCompany(data.Company.Name, path, data.Company.FiscalYearStartMonth), cancellationToken);

        foreach (var extra in data.ExtraAccounts)
            await chart.CreateAsync(new AccountInput(extra.Name, Enum.Parse<AccountSubtype>(extra.Subtype), extra.Number), cancellationToken);

        var ids = (await chart.ListAsync(includeInactive: true, cancellationToken))
            .Where(a => a.Number is not null)
            .ToDictionary(a => a.Number!, a => a.Id);

        await posting.PostAsync(Draft(data.Opening, ids), cancellationToken);
        foreach (var entry in data.Entries)
            await posting.PostAsync(Draft(entry, ids), cancellationToken);

        return path;
    }

    private static JournalEntryDraft Draft(SampleBooks.Entry entry, Dictionary<string, int> ids) => new()
    {
        Date = entry.Date,
        Memo = entry.Memo,
        Lines = entry.Lines.Select(l => new JournalLineDraft(
            ids.TryGetValue(l.Account, out var id) ? id : throw new InvalidOperationException($"Sample uses account {l.Account}, which is not in the chart."),
            l.Debit is null ? Money.Zero : Money.Parse(l.Debit),
            l.Credit is null ? Money.Zero : Money.Parse(l.Credit),
            l.Description)).ToList(),
    };
}

/// <summary>The contents of <c>sample-books.json</c>.</summary>
public sealed record SampleBooks(
    SampleBooks.CompanyInfo Company,
    SampleBooks.BankInfo Bank,
    IReadOnlyList<SampleBooks.ExtraAccount> ExtraAccounts,
    SampleBooks.Entry Opening,
    IReadOnlyList<SampleBooks.Entry> Entries,
    IReadOnlyList<SampleBooks.BankOnlyItem> BankOnly)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static SampleBooks Load()
    {
        using var stream = typeof(SampleBooks).Assembly.GetManifestResourceStream("SmallBooks.Data.Samples.sample-books.json")
            ?? throw new InvalidOperationException("sample-books.json is not embedded in SmallBooks.Data.");
        return JsonSerializer.Deserialize<SampleBooks>(stream, Json)
            ?? throw new InvalidOperationException("sample-books.json is empty.");
    }

    public sealed record CompanyInfo(string Name, int FiscalYearStartMonth);

    public sealed record Period(DateOnly From, DateOnly To);

    public sealed record BankInfo(string Name, string AccountNumber, string LedgerAccount, Period Statement, Period Download);

    public sealed record ExtraAccount(string Number, string Name, string Subtype);

    public sealed record Entry(DateOnly Date, string Memo, IReadOnlyList<Line> Lines, BankSide? Bank = null);

    public sealed record Line(
        [property: JsonPropertyName("a")] string Account,
        [property: JsonPropertyName("dr")] string? Debit = null,
        [property: JsonPropertyName("cr")] string? Credit = null,
        [property: JsonPropertyName("d")] string? Description = null);

    /// <summary>How an entry shows up at the bank.</summary>
    /// <param name="Amount">Set only when the bank's amount differs from the books (a deliberate mistake to find).</param>
    public sealed record BankSide(DateOnly Posted, string Type, string Payee, string? Check = null, string? Amount = null, string? Note = null);

    /// <summary>Something on the bank's side that the books don't have yet.</summary>
    public sealed record BankOnlyItem(DateOnly Posted, string Type, string Payee, string Amount, string Suggest, string? Note = null);
}

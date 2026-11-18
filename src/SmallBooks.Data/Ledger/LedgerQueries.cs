using Microsoft.EntityFrameworkCore;
using SmallBooks.Core;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Journal;
using SmallBooks.Modules;

namespace SmallBooks.Data.Ledger;

internal sealed class LedgerQueries(ICompanyDatabase database) : ILedgerQueries
{
    public async Task<IReadOnlyDictionary<int, Money>> GetBalancesAsync(DateOnly? asOf = null, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        return await BalancesAsync(db, asOf, cancellationToken);
    }

    private static async Task<Dictionary<int, Money>> BalancesAsync(LedgerDbContext db, DateOnly? asOf, CancellationToken ct)
    {
        var lines = from l in db.JournalLines
                    join e in db.JournalEntries on l.JournalEntryId equals e.Id
                    where asOf == null || e.Date <= asOf
                    select l;

        var sums = await lines
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Cents = g.Sum(l => l.DebitCents - l.CreditCents) })
            .ToListAsync(ct);

        return sums.ToDictionary(s => s.AccountId, s => new Money(s.Cents));
    }

    public async Task<TrialBalance> GetTrialBalanceAsync(DateOnly asOf, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var balances = await BalancesAsync(db, asOf, cancellationToken);
        var accounts = await db.Accounts.AsNoTracking().ToListAsync(cancellationToken);

        var rows = accounts
            .Where(a => balances.TryGetValue(a.Id, out var b) && !b.IsZero)
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Number ?? "~")
            .ThenBy(a => a.Name)
            .Select(a =>
            {
                var b = balances[a.Id];
                return new TrialBalanceRow(a.Id, a.Number, a.Name, a.Type,
                    b.IsNegative ? Money.Zero : b,
                    b.IsNegative ? -b : Money.Zero);
            })
            .ToList();

        return new TrialBalance(asOf, rows);
    }

    public async Task<AccountRegister> GetAccountRegisterAsync(int accountId, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Account {accountId} does not exist.");
        var sign = account.NormalBalance == NormalBalance.Debit ? 1 : -1;

        long opening = 0;
        if (from is { } start)
        {
            opening = await (from l in db.JournalLines
                             join e in db.JournalEntries on l.JournalEntryId equals e.Id
                             where l.AccountId == accountId && e.Date < start
                             select l.DebitCents - l.CreditCents).SumAsync(cancellationToken);
        }

        var rows = await (from l in db.JournalLines
                          join e in db.JournalEntries on l.JournalEntryId equals e.Id
                          where l.AccountId == accountId
                                && (@from == null || e.Date >= @from)
                                && (to == null || e.Date <= to)
                          orderby e.Date, e.Id, l.LineNumber
                          select new { e.Id, e.Number, e.Date, e.Memo, l.Description, e.SourceModule, l.DebitCents, l.CreditCents })
                         .ToListAsync(cancellationToken);

        var running = opening * sign;
        var lines = new List<RegisterLine>(rows.Count);
        foreach (var r in rows)
        {
            running += (r.DebitCents - r.CreditCents) * sign;
            lines.Add(new RegisterLine(r.Id, r.Number, r.Date, r.Memo, r.Description, r.SourceModule,
                new Money(r.DebitCents), new Money(r.CreditCents), new Money(running)));
        }

        return new AccountRegister(account, from, to, new Money(opening * sign), lines);
    }

    public async Task<JournalEntry?> GetEntryAsync(int journalEntryId, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var entry = await db.JournalEntries.AsNoTracking().Include(e => e.Lines)
            .SingleOrDefaultAsync(e => e.Id == journalEntryId, cancellationToken);
        entry?.Lines.Sort((a, b) => a.LineNumber.CompareTo(b.LineNumber));
        return entry;
    }

    public async Task<IReadOnlyList<JournalEntrySummary>> ListEntriesAsync(JournalQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var entries = db.JournalEntries.AsNoTracking();
        if (query.From is { } from) entries = entries.Where(e => e.Date >= from);
        if (query.To is { } to) entries = entries.Where(e => e.Date <= to);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var text = query.Search.Trim().ToLower();
            entries = entries.Where(e => e.Number.ToLower().Contains(text) || (e.Memo != null && e.Memo.ToLower().Contains(text)));
        }

        var rows = await entries
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            .Take(query.Take)
            .Select(e => new
            {
                e.Id, e.Number, e.Date, e.Memo, e.SourceModule, e.ReversedById, e.ReversalOfId,
                Total = e.Lines.Sum(l => l.DebitCents),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new JournalEntrySummary(r.Id, r.Number, r.Date, r.Memo, r.SourceModule,
            new Money(r.Total), r.ReversedById != null, r.ReversalOfId != null)).ToList();
    }
}

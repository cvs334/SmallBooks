using Microsoft.EntityFrameworkCore;
using SmallBooks.Core.Journal;
using SmallBooks.Modules;

namespace SmallBooks.Data.Ledger;

/// <summary>Posts and reverses entries inside a unit of work. The only code that writes journal entries.</summary>
internal sealed class LedgerWriter(UnitOfWork work) : ILedger
{
    public const string JournalSequence = "journal";
    public const string JournalPrefix = "JE-";

    public Task<JournalEntry> PostAsync(JournalEntryDraft draft, CancellationToken cancellationToken = default) =>
        PostCoreAsync(draft, reversalOfId: null, cancellationToken);

    public async Task<JournalEntry> ReverseAsync(int journalEntryId, DateOnly date, string? memo = null, CancellationToken cancellationToken = default)
    {
        var db = work.GetContext<LedgerDbContext>();
        var original = await db.JournalEntries.Include(e => e.Lines)
            .SingleOrDefaultAsync(e => e.Id == journalEntryId, cancellationToken)
            ?? throw new PostingException($"Journal entry {journalEntryId} does not exist.");

        var draft = PostingRules.BuildReversal(original, date, memo);
        var reversal = await PostCoreAsync(draft, original.Id, cancellationToken);

        original.ReversedById = reversal.Id;
        await db.SaveChangesAsync(cancellationToken);
        work.Audit("reverse", "JournalEntry", original.Id.ToString(), $"{original.Number} reversed by {reversal.Number}");
        await work.PublishAsync(new JournalEntryReversed(original, reversal), cancellationToken);
        return reversal;
    }

    private async Task<JournalEntry> PostCoreAsync(JournalEntryDraft draft, int? reversalOfId, CancellationToken ct)
    {
        var db = work.GetContext<LedgerDbContext>();
        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);
        var accountIds = draft.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var problems = PostingRules.Validate(draft, settings.LockDate, accounts);
        if (problems.Count > 0) throw new PostingException(problems);

        var entry = new JournalEntry
        {
            Number = await work.NextNumberAsync(JournalSequence, JournalPrefix, ct),
            Date = draft.Date,
            Memo = string.IsNullOrWhiteSpace(draft.Memo) ? null : draft.Memo.Trim(),
            SourceModule = draft.SourceModule,
            SourceType = draft.SourceType,
            SourceId = draft.SourceId,
            CreatedAtUtc = DateTime.UtcNow,
            ReversalOfId = reversalOfId,
            Lines = draft.Lines.Select((l, i) => new JournalLine
            {
                LineNumber = i + 1,
                AccountId = l.AccountId,
                DebitCents = l.Debit.Cents,
                CreditCents = l.Credit.Cents,
                Description = string.IsNullOrWhiteSpace(l.Description) ? null : l.Description.Trim(),
                ContactId = l.ContactId,
            }).ToList(),
        };

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        work.Audit("post", "JournalEntry", entry.Id.ToString(), $"{entry.Number} {entry.Date:yyyy-MM-dd} {entry.TotalDebits}");
        await work.PublishAsync(new JournalEntryPosted(entry), ct);
        return entry;
    }
}

/// <summary>Posts or reverses a single entry in its own transaction.</summary>
internal sealed class PostingService(IUnitOfWorkFactory unitOfWork) : IPostingService
{
    public async Task<JournalEntry> PostAsync(JournalEntryDraft draft, CancellationToken cancellationToken = default)
    {
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        var entry = await work.Ledger.PostAsync(draft, cancellationToken);
        await work.CommitAsync(cancellationToken);
        return entry;
    }

    public async Task<JournalEntry> ReverseAsync(int journalEntryId, DateOnly date, string? memo = null, CancellationToken cancellationToken = default)
    {
        await using var work = await unitOfWork.BeginAsync(cancellationToken);
        var entry = await work.Ledger.ReverseAsync(journalEntryId, date, memo, cancellationToken);
        await work.CommitAsync(cancellationToken);
        return entry;
    }
}

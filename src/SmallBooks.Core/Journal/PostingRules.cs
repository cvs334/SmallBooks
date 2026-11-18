using SmallBooks.Core.Accounts;

namespace SmallBooks.Core.Journal;

/// <summary>
/// The rules every journal entry must pass before it reaches the ledger. They are
/// the reason the books always balance, whichever module is doing the posting.
/// </summary>
public static class PostingRules
{
    /// <summary>Returns every problem with the draft, in plain words; an empty list means it can post.</summary>
    /// <param name="lockDate">Nothing may post on or before this date. Null means the books are open.</param>
    /// <param name="accounts">The accounts the draft's lines refer to, by id.</param>
    public static IReadOnlyList<string> Validate(
        JournalEntryDraft draft,
        DateOnly? lockDate,
        IReadOnlyDictionary<int, Account> accounts)
    {
        var problems = new List<string>();

        if (draft.Date == default)
            problems.Add("The entry needs a date.");
        else if (lockDate is { } locked && draft.Date <= locked)
            problems.Add($"The books are closed through {locked:MMM d, yyyy}. Use a date after that, or move the lock date in Settings.");

        if (draft.Lines.Count < 2)
            problems.Add("An entry needs at least two lines.");

        for (var i = 0; i < draft.Lines.Count; i++)
        {
            var line = draft.Lines[i];
            var n = i + 1;

            if (line.Debit.IsNegative || line.Credit.IsNegative)
                problems.Add($"Line {n}: amounts cannot be negative. Put the amount in the other column instead.");
            else if (!line.Debit.IsZero && !line.Credit.IsZero)
                problems.Add($"Line {n}: use either a debit or a credit, not both.");
            else if (line.Debit.IsZero && line.Credit.IsZero)
                problems.Add($"Line {n}: enter a debit or a credit amount.");

            if (!accounts.TryGetValue(line.AccountId, out var account))
                problems.Add($"Line {n}: choose an account.");
            else if (!account.IsActive)
                problems.Add($"Line {n}: {account.Name} is inactive. Reactivate it in the chart of accounts to post to it.");
        }

        var debits = draft.TotalDebits;
        var credits = draft.TotalCredits;
        if (debits != credits)
            problems.Add($"Debits ({debits}) and credits ({credits}) must be equal. They are off by {(debits - credits).Abs()}.");
        else if (debits.IsZero && draft.Lines.Count >= 2)
            problems.Add("The entry has no amounts.");

        return problems;
    }

    /// <summary>Builds the entry that exactly undoes <paramref name="original"/>, by swapping every debit and credit.</summary>
    public static JournalEntryDraft BuildReversal(JournalEntry original, DateOnly date, string? memo)
    {
        if (original.IsReversed)
            throw new PostingException($"{original.Number} has already been reversed.");
        if (original.IsReversal)
            throw new PostingException($"{original.Number} is itself a reversal. Post a new entry instead of reversing it.");
        if (date < original.Date)
            throw new PostingException($"A reversal cannot be dated before the entry it reverses ({original.Date:MMM d, yyyy}).");

        return new JournalEntryDraft
        {
            Date = date,
            Memo = string.IsNullOrWhiteSpace(memo) ? $"Reverses {original.Number}" : memo,
            SourceModule = original.SourceModule,
            SourceType = original.SourceType,
            SourceId = original.SourceId,
            Lines = original.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new JournalLineDraft(l.AccountId, l.Credit, l.Debit, l.Description, l.ContactId))
                .ToList(),
        };
    }
}

/// <summary>Raised when an entry breaks a posting rule. <see cref="Problems"/> lists each reason in plain words.</summary>
public sealed class PostingException : Exception
{
    public IReadOnlyList<string> Problems { get; }

    public PostingException(string problem) : this([problem]) { }

    public PostingException(IReadOnlyList<string> problems)
        : base(string.Join(" ", problems))
    {
        Problems = problems;
    }
}

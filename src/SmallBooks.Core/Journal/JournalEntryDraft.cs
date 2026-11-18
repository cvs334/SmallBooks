namespace SmallBooks.Core.Journal;

/// <summary>What a module (or a person, for a manual entry) asks the ledger to post.</summary>
public sealed record JournalEntryDraft
{
    public required DateOnly Date { get; init; }
    public string? Memo { get; init; }
    public required IReadOnlyList<JournalLineDraft> Lines { get; init; }

    public string SourceModule { get; init; } = SourceModules.Core;
    public string? SourceType { get; init; }
    public string? SourceId { get; init; }

    public Money TotalDebits => Money.Sum(Lines.Select(l => l.Debit));
    public Money TotalCredits => Money.Sum(Lines.Select(l => l.Credit));
}

/// <param name="Debit">Amount debited; leave zero for a credit line.</param>
/// <param name="Credit">Amount credited; leave zero for a debit line.</param>
public sealed record JournalLineDraft(
    int AccountId,
    Money Debit,
    Money Credit,
    string? Description = null,
    int? ContactId = null)
{
    public static JournalLineDraft Dr(int accountId, Money amount, string? description = null) =>
        new(accountId, amount, Money.Zero, description);

    public static JournalLineDraft Cr(int accountId, Money amount, string? description = null) =>
        new(accountId, Money.Zero, amount, description);
}

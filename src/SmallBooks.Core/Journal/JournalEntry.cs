namespace SmallBooks.Core.Journal;

/// <summary>
/// A posted journal entry. Entries are never edited after posting: a mistake is
/// undone with a reversing entry, which keeps a clean audit trail.
/// </summary>
public class JournalEntry
{
    public int Id { get; set; }

    /// <summary>Ledger number, such as JE-0001. Every entry gets one, whichever module posted it.</summary>
    public string Number { get; set; } = "";

    public DateOnly Date { get; set; }
    public string? Memo { get; set; }

    /// <summary>The module that posted the entry: "core" for manual entries, "sales" for an invoice, and so on.</summary>
    public string SourceModule { get; set; } = SourceModules.Core;

    /// <summary>The kind of document behind the entry, such as "invoice". Null for manual entries.</summary>
    public string? SourceType { get; set; }

    /// <summary>The id of the document behind the entry, in the source module's own tables.</summary>
    public string? SourceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Set when this entry reverses another one.</summary>
    public int? ReversalOfId { get; set; }

    /// <summary>Set on the original entry once it has been reversed.</summary>
    public int? ReversedById { get; set; }

    public List<JournalLine> Lines { get; set; } = [];

    public bool IsReversed => ReversedById is not null;
    public bool IsReversal => ReversalOfId is not null;

    public Money TotalDebits => new(Lines.Sum(l => l.DebitCents));
    public Money TotalCredits => new(Lines.Sum(l => l.CreditCents));
}

/// <summary>One line of a journal entry: an amount on one side of one account.</summary>
public class JournalLine
{
    public int Id { get; set; }
    public int JournalEntryId { get; set; }

    /// <summary>Order of the line within its entry, starting at 1.</summary>
    public int LineNumber { get; set; }

    public int AccountId { get; set; }

    /// <summary>Debit in cents; zero when the line is a credit.</summary>
    public long DebitCents { get; set; }

    /// <summary>Credit in cents; zero when the line is a debit.</summary>
    public long CreditCents { get; set; }

    public string? Description { get; set; }

    /// <summary>Optional customer or vendor this line is about (contacts arrive with the Sales and Purchases modules).</summary>
    public int? ContactId { get; set; }

    public Money Debit => new(DebitCents);
    public Money Credit => new(CreditCents);

    /// <summary>Debit minus credit: positive for a debit line, negative for a credit line.</summary>
    public Money SignedAmount => new(DebitCents - CreditCents);
}

public static class SourceModules
{
    public const string Core = "core";
}

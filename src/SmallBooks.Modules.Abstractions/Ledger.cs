using SmallBooks.Core;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Journal;

namespace SmallBooks.Modules;

/// <summary>
/// The only way to record money. Every entry is checked by <see cref="PostingRules"/>
/// (balanced, dated after the lock date, active accounts) before it is written.
/// </summary>
public interface ILedger
{
    /// <exception cref="PostingException">The draft breaks a posting rule.</exception>
    Task<JournalEntry> PostAsync(JournalEntryDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Posts the mirror image of an entry, dated <paramref name="date"/>, and marks the original reversed.</summary>
    /// <exception cref="PostingException">The entry is already reversed, is a reversal, or the date is locked.</exception>
    Task<JournalEntry> ReverseAsync(int journalEntryId, DateOnly date, string? memo = null, CancellationToken cancellationToken = default);
}

/// <summary>Posts or reverses one entry in its own transaction. Use <see cref="IUnitOfWork"/> to save a document with it.</summary>
public interface IPostingService : ILedger;

/// <summary>Read-only questions about the ledger, used by reports and screens.</summary>
public interface ILedgerQueries
{
    Task<TrialBalance> GetTrialBalanceAsync(DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>Balance of every account that has activity, as debits minus credits, up to and including <paramref name="asOf"/>.</summary>
    Task<IReadOnlyDictionary<int, Money>> GetBalancesAsync(DateOnly? asOf = null, CancellationToken cancellationToken = default);

    Task<AccountRegister> GetAccountRegisterAsync(int accountId, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    Task<JournalEntry?> GetEntryAsync(int journalEntryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JournalEntrySummary>> ListEntriesAsync(JournalQuery query, CancellationToken cancellationToken = default);
}

public sealed record TrialBalance(DateOnly AsOf, IReadOnlyList<TrialBalanceRow> Rows)
{
    public Money TotalDebits => Money.Sum(Rows.Select(r => r.Debit));
    public Money TotalCredits => Money.Sum(Rows.Select(r => r.Credit));
    public bool IsBalanced => TotalDebits == TotalCredits;
}

/// <summary>An account's balance shown in the debit column or the credit column.</summary>
public sealed record TrialBalanceRow(int AccountId, string? Number, string Name, AccountType Type, Money Debit, Money Credit);

/// <param name="OpeningBalance">Balance before <c>from</c>, in the account's normal direction.</param>
public sealed record AccountRegister(Account Account, DateOnly? From, DateOnly? To, Money OpeningBalance, IReadOnlyList<RegisterLine> Lines)
{
    public Money ClosingBalance => Lines.Count == 0 ? OpeningBalance : Lines[^1].RunningBalance;
}

/// <param name="RunningBalance">Balance after this line, in the account's normal direction (positive is normal).</param>
public sealed record RegisterLine(
    int JournalEntryId,
    string Number,
    DateOnly Date,
    string? Memo,
    string? Description,
    string SourceModule,
    Money Debit,
    Money Credit,
    Money RunningBalance);

public sealed record JournalEntrySummary(
    int Id,
    string Number,
    DateOnly Date,
    string? Memo,
    string SourceModule,
    Money Total,
    bool IsReversed,
    bool IsReversal);

public sealed record JournalQuery(DateOnly? From = null, DateOnly? To = null, string? Search = null, int Take = 200);

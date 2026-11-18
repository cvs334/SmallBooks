using SmallBooks.Core.Journal;

namespace SmallBooks.Modules;

/// <summary>
/// Something that happened in the books, raised after a document posts. Modules
/// react to each other's events instead of calling each other: Inventory will
/// handle InvoicePosted to lower stock, for example.
/// </summary>
public interface IDomainEvent;

/// <summary>
/// Reacts to a domain event. Register handlers in <see cref="IAccountingModule.ConfigureServices"/>.
/// Handlers run inside the same transaction as the document that raised the event,
/// so throwing an exception undoes the whole save.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, IUnitOfWork unitOfWork, CancellationToken cancellationToken);
}

/// <summary>Raised whenever any journal entry posts, including reversals.</summary>
public sealed record JournalEntryPosted(JournalEntry Entry) : IDomainEvent;

/// <summary>Raised when an entry is reversed. <see cref="Reversal"/> is the new entry.</summary>
public sealed record JournalEntryReversed(JournalEntry Original, JournalEntry Reversal) : IDomainEvent;

// Document events named in the plan (InvoicePosted, InvoiceVoided, BillPosted,
// PaymentReceived, ReceiptArrived) are added here alongside the Sales, Purchases
// and Receipt Inbox modules, so that no module has to reference another to hear them.

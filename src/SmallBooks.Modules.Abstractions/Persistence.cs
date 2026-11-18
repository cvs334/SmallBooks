using Microsoft.EntityFrameworkCore;

namespace SmallBooks.Modules;

/// <summary>
/// The open company file. Every module's DbContext lives in the same SQLite file,
/// each with its own migrations history table, so a module can be added to an
/// existing company without touching anyone else's migrations.
/// </summary>
public interface ICompanyDatabase
{
    /// <summary>Full path of the open company file.</summary>
    string FilePath { get; }

    /// <summary>
    /// Creates a context on its own connection, for reads or a single save. The
    /// context type must carry <see cref="MigrationsHistoryTableAttribute"/>.
    /// </summary>
    TContext CreateContext<TContext>() where TContext : DbContext;

    /// <summary>Applies the context's pending migrations to the open company file.</summary>
    Task MigrateAsync<TContext>(CancellationToken cancellationToken = default) where TContext : DbContext;
}

/// <summary>
/// Names the migrations history table of a module's DbContext, such as
/// "__sales_migrations". Required on every context so modules never share one.
/// Tables should be prefixed with the module id too, such as "sales_Invoice".
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MigrationsHistoryTableAttribute(string tableName) : Attribute
{
    public string TableName { get; } = tableName;
}

/// <summary>
/// Saves a document and its journal entry together: either both are written or
/// neither is. Contexts handed out by <see cref="GetContext{TContext}"/> share one
/// connection and one transaction.
/// <code>
/// await using var work = await unitOfWork.BeginAsync(ct);
/// var db = work.GetContext&lt;SalesDbContext&gt;();
/// db.Invoices.Add(invoice);
/// await db.SaveChangesAsync(ct);
/// var entry = await work.Ledger.PostAsync(draft, ct);
/// await work.CommitAsync(ct);
/// </code>
/// Disposing without <see cref="CommitAsync"/> rolls everything back.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    TContext GetContext<TContext>() where TContext : DbContext;

    /// <summary>Posts and reverses journal entries inside this unit of work.</summary>
    ILedger Ledger { get; }

    /// <summary>Hands out the next document number for a sequence, such as INV-0001.</summary>
    Task<string> NextNumberAsync(string sequenceKey, string defaultPrefix, CancellationToken cancellationToken = default);

    /// <summary>Raises a domain event; handlers run now, inside this transaction.</summary>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;

    /// <summary>Records a line in the audit log as part of this unit of work.</summary>
    void Audit(string action, string entity, string? entityId, string? details = null);

    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);
}

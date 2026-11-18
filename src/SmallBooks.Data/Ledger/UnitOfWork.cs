using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SmallBooks.Core.Company;
using SmallBooks.Data.Company;
using SmallBooks.Modules;

namespace SmallBooks.Data.Ledger;

internal sealed class UnitOfWorkFactory(CompanySession session, IServiceProvider services) : IUnitOfWorkFactory
{
    public async Task<IUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
    {
        var database = session.Database ?? throw new NoCompanyOpenException();
        return await UnitOfWork.BeginAsync(database, services, cancellationToken);
    }
}

/// <summary>One connection and one transaction shared by every context the work touches.</summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly CompanyDatabase _database;
    private readonly IServiceProvider _services;
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly Dictionary<Type, DbContext> _contexts = [];
    private bool _committed;

    private UnitOfWork(CompanyDatabase database, IServiceProvider services, SqliteConnection connection, SqliteTransaction transaction)
    {
        _database = database;
        _services = services;
        _connection = connection;
        _transaction = transaction;
        Ledger = new LedgerWriter(this);
    }

    public static async Task<UnitOfWork> BeginAsync(CompanyDatabase database, IServiceProvider services, CancellationToken ct)
    {
        var connection = database.CreateConnection();
        await connection.OpenAsync(ct);
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        return new UnitOfWork(database, services, connection, transaction);
    }

    public ILedger Ledger { get; }

    public TContext GetContext<TContext>() where TContext : DbContext
    {
        if (_contexts.TryGetValue(typeof(TContext), out var existing)) return (TContext)existing;

        var context = _database.CreateContext<TContext>(_connection);
        context.Database.UseTransaction(_transaction);
        _contexts[typeof(TContext)] = context;
        return context;
    }

    public async Task<string> NextNumberAsync(string sequenceKey, string defaultPrefix, CancellationToken cancellationToken = default)
    {
        var db = GetContext<LedgerDbContext>();
        var sequence = await db.NumberSequences.FindAsync([sequenceKey], cancellationToken);
        if (sequence is null)
        {
            sequence = new NumberSequence { Key = sequenceKey, Prefix = defaultPrefix, NextValue = 1 };
            db.NumberSequences.Add(sequence);
        }
        var value = sequence.NextValue;
        sequence.NextValue = value + 1;
        await db.SaveChangesAsync(cancellationToken);
        return sequence.Format(value);
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
    {
        foreach (var handler in _services.GetServices<IDomainEventHandler<TEvent>>())
            await handler.HandleAsync(domainEvent, this, cancellationToken);
    }

    public void Audit(string action, string entity, string? entityId, string? details = null) =>
        GetContext<LedgerDbContext>().AuditLog.Add(AuditLog.Entry(action, entity, entityId, details));

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_committed) throw new InvalidOperationException("This unit of work has already been committed.");
        foreach (var context in _contexts.Values)
            if (context.ChangeTracker.HasChanges())
                await context.SaveChangesAsync(cancellationToken);
        await _transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            try { await _transaction.RollbackAsync(); }
            catch (InvalidOperationException) { /* already completed */ }
        }
        foreach (var context in _contexts.Values) await context.DisposeAsync();
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal static class AuditLog
{
    public static AuditEntry Entry(string action, string entity, string? entityId, string? details) => new()
    {
        AtUtc = DateTime.UtcNow,
        User = Environment.UserName,
        Action = action,
        Entity = entity,
        EntityId = entityId,
        Details = details,
    };
}

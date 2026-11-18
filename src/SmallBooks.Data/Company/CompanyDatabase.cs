using System.Data.Common;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmallBooks.Modules;

namespace SmallBooks.Data.Company;

/// <summary>One open company file (a SQLite database) and the way to reach it.</summary>
public sealed class CompanyDatabase : ICompanyDatabase
{
    public CompanyDatabase(string filePath, bool allowCreate = false)
    {
        FilePath = Path.GetFullPath(filePath);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = allowCreate ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            ForeignKeys = true,
            // No pooling: when the company closes, no handle is left open on the
            // file, so it can be backed up, restored or copied straight away.
            Pooling = false,
        }.ToString();
    }

    public string FilePath { get; }
    public string ConnectionString { get; }

    public SqliteConnection CreateConnection() => new(ConnectionString);

    public TContext CreateContext<TContext>() where TContext : DbContext =>
        Create<TContext>(o => o.UseSqlite(ConnectionString, s => s.MigrationsHistoryTable(HistoryTableOf<TContext>())));

    /// <summary>A context on an existing connection, used to share one transaction across contexts.</summary>
    public TContext CreateContext<TContext>(DbConnection connection) where TContext : DbContext =>
        Create<TContext>(o => o.UseSqlite(connection, s => s.MigrationsHistoryTable(HistoryTableOf<TContext>())));

    public async Task MigrateAsync<TContext>(CancellationToken cancellationToken = default) where TContext : DbContext
    {
        await using var db = CreateContext<TContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static TContext Create<TContext>(Action<DbContextOptionsBuilder<TContext>> configure) where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        configure(builder);
        return (TContext?)Activator.CreateInstance(typeof(TContext), builder.Options)
            ?? throw new InvalidOperationException(
                $"{typeof(TContext).Name} needs a public constructor that takes DbContextOptions<{typeof(TContext).Name}>.");
    }

    private static string HistoryTableOf<TContext>() =>
        typeof(TContext).GetCustomAttribute<MigrationsHistoryTableAttribute>()?.TableName
        ?? throw new InvalidOperationException(
            $"{typeof(TContext).Name} must be marked [MigrationsHistoryTable(\"__<module>_migrations\")] so modules never share a history table.");
}

/// <summary>The <see cref="ICompanyDatabase"/> handed to services: whichever company is open right now.</summary>
internal sealed class ActiveCompanyDatabase(CompanySession session) : ICompanyDatabase
{
    private CompanyDatabase Current => session.Database ?? throw new NoCompanyOpenException();

    public string FilePath => Current.FilePath;
    public TContext CreateContext<TContext>() where TContext : DbContext => Current.CreateContext<TContext>();
    public Task MigrateAsync<TContext>(CancellationToken cancellationToken = default) where TContext : DbContext =>
        Current.MigrateAsync<TContext>(cancellationToken);
}

public sealed class NoCompanyOpenException() : InvalidOperationException("No company file is open.");

/// <summary>A company file could not be created or opened. The message is meant for people.</summary>
public sealed class CompanyFileException(string message, Exception? inner = null) : Exception(message, inner);

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Company;
using SmallBooks.Data.Backup;
using SmallBooks.Data.Hosting;
using SmallBooks.Data.Ledger;
using SmallBooks.Data.Settings;
using SmallBooks.Modules;

namespace SmallBooks.Data.Company;

public sealed record NewCompany(string Name, string FilePath, int FiscalYearStartMonth = 1);

/// <summary>Creates, opens and closes company files, and holds the one that is open.</summary>
public sealed class CompanySession(ModuleHost modules, BackupService backups, AppSettingsStore appSettings) : ICompanySession
{
    public const string FileExtension = ".sbk";
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal CompanyDatabase? Database { get; private set; }

    public bool IsOpen => Database is not null;
    public string? FilePath => Database?.FilePath;
    public string? CompanyName { get; private set; }

    /// <summary>Set when the last automatic backup on close failed; shown on the start screen.</summary>
    public string? LastBackupError { get; private set; }

    public event Action? Changed;

    /// <summary>A safe file name for a company in a folder, such as Documents\SmallBooks\Acme LLC.sbk.</summary>
    public static string SuggestPath(string folder, string companyName)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var name = new string((companyName ?? "").Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim('.', ' ');
        if (name.Length == 0) name = "Company";
        return Path.Combine(folder, name + FileExtension);
    }

    public async Task CreateAsync(NewCompany request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) throw new CompanyFileException("Enter the company's name.");
        if (request.FiscalYearStartMonth is < 1 or > 12) throw new CompanyFileException("Choose the month the fiscal year starts.");
        if (string.IsNullOrWhiteSpace(request.FilePath)) throw new CompanyFileException("Choose where to save the company file.");

        var path = Path.GetFullPath(request.FilePath.Trim());
        if (!path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)) path += FileExtension;
        if (File.Exists(path)) throw new CompanyFileException($"A file already exists at {path}. Open it instead, or choose another name.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var database = new CompanyDatabase(path, allowCreate: true);
            try
            {
                await database.MigrateAsync<LedgerDbContext>(cancellationToken);
                await using var db = database.CreateContext<LedgerDbContext>();
                db.Settings.Add(new CompanySettings
                {
                    CompanyName = name,
                    FiscalYearStartMonth = request.FiscalYearStartMonth,
                    CreatedAtUtc = DateTime.UtcNow,
                });
                db.AuditLog.Add(AuditLog.Entry("create", "Company", null, name));
                await db.SaveChangesAsync(cancellationToken);

                var seeder = new ChartOfAccountsSeeder("core");
                seeder.AddStarter(StarterChart.Accounts);
                await seeder.ApplyAsync(db, cancellationToken);
            }
            catch
            {
                SqliteConnection.ClearAllPools();
                try { File.Delete(path); } catch (IOException) { }
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }

        await OpenAsync(path, cancellationToken);
    }

    public async Task OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsOpen) await CloseCoreAsync(backup: true, cancellationToken);

            var path = Path.GetFullPath(filePath);
            if (!File.Exists(path))
            {
                appSettings.RemoveRecent(path);
                throw new CompanyFileException($"Could not find {path}. It may have been moved or renamed.");
            }

            var database = new CompanyDatabase(path);
            CompanySettings settings;
            try
            {
                await CheckIsCompanyFileAsync(database, cancellationToken);
                await using (var db = database.CreateContext<LedgerDbContext>())
                {
                    var known = db.Database.GetMigrations().ToHashSet();
                    var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
                    if (applied.Any(m => !known.Contains(m)))
                        throw new CompanyFileException($"{Path.GetFileName(path)} was saved by a newer version of SmallBooks. Update the app to open it.");
                    if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                        await backups.CreateAsync(path, "before upgrade", cancellationToken);
                }

                await database.MigrateAsync<LedgerDbContext>(cancellationToken);
                await using (var db = database.CreateContext<LedgerDbContext>())
                {
                    settings = await db.Settings.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                        ?? throw new CompanyFileException($"{Path.GetFileName(path)} is not a SmallBooks company file.");
                }
            }
            catch (SqliteException ex)
            {
                throw new CompanyFileException($"{Path.GetFileName(path)} is not a SmallBooks company file, or it is damaged.", ex);
            }

            Database = database;
            CompanyName = settings.CompanyName;
            try
            {
                await modules.LoadForCompanyAsync(cancellationToken);
            }
            catch
            {
                Database = null;
                CompanyName = null;
                modules.Clear();
                throw;
            }

            appSettings.AddRecent(path);
            LastBackupError = null;
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke();
    }

    /// <summary>Closes the company, making a backup first unless that is turned off.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsOpen) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await CloseCoreAsync(backup: true, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Replaces the open company with a backup. The current file is backed up first
    /// (marked "before restore"), so a restore can itself be undone.
    /// </summary>
    public async Task RestoreAsync(string backupFile, CancellationToken cancellationToken = default)
    {
        var path = FilePath ?? throw new NoCompanyOpenException();
        if (!File.Exists(backupFile)) throw new CompanyFileException("That backup file no longer exists.");
        await CheckIsCompanyFileAsync(new CompanyDatabase(backupFile), cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await backups.CreateAsync(path, "before restore", cancellationToken);
            await CloseCoreAsync(backup: false, cancellationToken);
            File.Copy(backupFile, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
        await OpenAsync(path, cancellationToken);
    }

    public async Task<CompanySettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var database = Database ?? throw new NoCompanyOpenException();
        await using var db = database.CreateContext<LedgerDbContext>();
        return await db.Settings.AsNoTracking().SingleAsync(cancellationToken);
    }

    /// <summary>Saves the company name, fiscal year start and lock date.</summary>
    public async Task SaveSettingsAsync(string companyName, int fiscalYearStartMonth, DateOnly? lockDate, CancellationToken cancellationToken = default)
    {
        var database = Database ?? throw new NoCompanyOpenException();
        var name = (companyName ?? "").Trim();
        if (name.Length == 0) throw new CompanyFileException("Enter the company's name.");
        if (fiscalYearStartMonth is < 1 or > 12) throw new CompanyFileException("Choose the month the fiscal year starts.");

        await using var db = database.CreateContext<LedgerDbContext>();
        var settings = await db.Settings.SingleAsync(cancellationToken);
        if (settings.LockDate != lockDate)
            db.AuditLog.Add(AuditLog.Entry("lock-date", "Company", null,
                $"{settings.LockDate?.ToString("yyyy-MM-dd") ?? "none"} -> {lockDate?.ToString("yyyy-MM-dd") ?? "none"}"));
        if (settings.CompanyName != name)
            db.AuditLog.Add(AuditLog.Entry("rename", "Company", null, $"{settings.CompanyName} -> {name}"));

        settings.CompanyName = name;
        settings.FiscalYearStartMonth = fiscalYearStartMonth;
        settings.LockDate = lockDate;
        await db.SaveChangesAsync(cancellationToken);

        CompanyName = name;
        Changed?.Invoke();
    }

    private async Task CloseCoreAsync(bool backup, CancellationToken ct)
    {
        if (Database is null) return;
        var path = Database.FilePath;
        if (backup && appSettings.Current.BackupOnClose)
        {
            try
            {
                await backups.CreateAsync(path, cancellationToken: ct);
                LastBackupError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                LastBackupError = $"The backup when closing {Path.GetFileName(path)} failed: {ex.Message}";
            }
        }
        Database = null;
        CompanyName = null;
        modules.Clear();
    }

    /// <summary>Refuses SQLite files that some other program made, before anything is written to them.</summary>
    private static async Task CheckIsCompanyFileAsync(CompanyDatabase database, CancellationToken ct)
    {
        try
        {
            await using var connection = database.CreateConnection();
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '__core_migrations'";
            var found = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            if (found == 0)
                throw new CompanyFileException($"{Path.GetFileName(database.FilePath)} is not a SmallBooks company file.");
        }
        catch (SqliteException ex)
        {
            throw new CompanyFileException($"{Path.GetFileName(database.FilePath)} is not a SmallBooks company file, or it is damaged.", ex);
        }
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SmallBooks.Data.Company;
using SmallBooks.Data.Settings;

namespace SmallBooks.Data.Backup;

/// <summary>A backup copy of a company file.</summary>
public sealed record BackupInfo(string FilePath, DateTime CreatedLocal, long SizeBytes, string? Note)
{
    public string FileName => Path.GetFileName(FilePath);
}

/// <summary>
/// Copies a company file to the backup folder using SQLite's online backup, so a
/// backup is always a complete, consistent file even if taken while the company is open.
/// </summary>
public sealed class BackupService(AppSettingsStore settings)
{
    private const string Stamp = "yyyy-MM-dd HHmmss";

    public string FolderFor(string companyFile) =>
        settings.Current.BackupFolder is { Length: > 0 } folder
            ? folder
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(companyFile))!, "Backups");

    /// <param name="note">Optional word added to the file name, such as "before restore".</param>
    public async Task<BackupInfo> CreateAsync(string companyFile, string? note = null, CancellationToken cancellationToken = default)
    {
        var folder = FolderFor(companyFile);
        Directory.CreateDirectory(folder);

        var stem = Path.GetFileNameWithoutExtension(companyFile);
        var now = DateTime.Now;
        var suffix = string.IsNullOrWhiteSpace(note) ? "" : $" ({note.Trim()})";
        var target = Path.Combine(folder, $"{stem} {now.ToString(Stamp, CultureInfo.InvariantCulture)}{suffix}.sbk");
        for (var i = 2; File.Exists(target); i++)
            target = Path.Combine(folder, $"{stem} {now.ToString(Stamp, CultureInfo.InvariantCulture)}{suffix} {i}.sbk");

        await Task.Run(() =>
        {
            using var source = new CompanyDatabase(companyFile).CreateConnection();
            using var destination = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }, cancellationToken);

        Prune(companyFile, keepPath: target);
        var info = new FileInfo(target);
        return new BackupInfo(target, now, info.Length, note);
    }

    /// <summary>Backups of this company, newest first.</summary>
    public IReadOnlyList<BackupInfo> List(string companyFile)
    {
        var folder = FolderFor(companyFile);
        if (!Directory.Exists(folder)) return [];

        var pattern = NamePattern(companyFile);
        return Directory.EnumerateFiles(folder, "*.sbk")
            .Select(path => (path, match: pattern.Match(Path.GetFileName(path))))
            .Where(x => x.match.Success)
            .Select(x =>
            {
                DateTime.TryParseExact(x.match.Groups["stamp"].Value, Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at);
                var note = x.match.Groups["note"].Success ? x.match.Groups["note"].Value : null;
                var n = x.match.Groups["n"].Success ? int.Parse(x.match.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
                var file = new FileInfo(x.path);
                return (info: new BackupInfo(x.path, at, file.Length, note), written: file.LastWriteTimeUtc, n);
            })
            // Newest first by when the file was written; backups are never modified afterwards.
            .OrderByDescending(x => x.written)
            .ThenByDescending(x => x.info.CreatedLocal)
            .ThenByDescending(x => x.n)
            .Select(x => x.info)
            .ToList();
    }

    /// <summary>Deletes the oldest backups beyond the number to keep.</summary>
    public void Prune(string companyFile, string? keepPath = null)
    {
        var keep = Math.Max(1, settings.Current.BackupKeepCount);
        foreach (var old in List(companyFile).Skip(keep))
        {
            if (string.Equals(old.FilePath, keepPath, StringComparison.OrdinalIgnoreCase)) continue;
            try { File.Delete(old.FilePath); }
            catch (IOException) { /* in use; try again next time */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static Regex NamePattern(string companyFile) => new(
        "^" + Regex.Escape(Path.GetFileNameWithoutExtension(companyFile)) +
        @" (?<stamp>\d{4}-\d{2}-\d{2} \d{6})(?: \((?<note>[^)]*)\))?(?: (?<n>\d+))?\.sbk$",
        RegexOptions.IgnoreCase);
}

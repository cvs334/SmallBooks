using System.Text.Json;

namespace SmallBooks.Data.Settings;

/// <summary>Where the app keeps its own files. Hosts and tests can change these.</summary>
public sealed class SmallBooksOptions
{
    /// <summary>Folder for app settings such as the recent-files list.</summary>
    public string AppDataFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmallBooks");

    /// <summary>Where new company files are suggested, such as Documents\SmallBooks.</summary>
    public string DefaultCompanyFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SmallBooks");
}

/// <summary>Settings that belong to this PC rather than to a company file.</summary>
public sealed class AppSettings
{
    public List<string> RecentFiles { get; set; } = [];

    /// <summary>Open the last company automatically when the app starts.</summary>
    public bool ReopenLastCompany { get; set; } = true;

    /// <summary>Make a backup every time a company is closed (including when the app exits).</summary>
    public bool BackupOnClose { get; set; } = true;

    /// <summary>How many automatic backups to keep per company. Older ones are deleted.</summary>
    public int BackupKeepCount { get; set; } = 30;

    /// <summary>Backup folder; empty means a "Backups" folder next to the company file. Can point at OneDrive or Dropbox.</summary>
    public string? BackupFolder { get; set; }
}

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON in the app data folder.</summary>
public sealed class AppSettingsStore
{
    private const int MaxRecent = 8;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;
    private readonly Lock _gate = new();

    public AppSettingsStore(SmallBooksOptions options)
    {
        _path = Path.Combine(options.AppDataFolder, "settings.json");
        Current = Load(_path);
    }

    public AppSettings Current { get; private set; }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            settings.BackupKeepCount = Math.Clamp(settings.BackupKeepCount, 1, 1000);
            settings.BackupFolder = string.IsNullOrWhiteSpace(settings.BackupFolder) ? null : settings.BackupFolder.Trim();
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
            File.Move(temp, _path, overwrite: true);
            Current = settings;
        }
    }

    public void AddRecent(string filePath)
    {
        var s = Current;
        s.RecentFiles.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));
        s.RecentFiles.Insert(0, filePath);
        if (s.RecentFiles.Count > MaxRecent) s.RecentFiles.RemoveRange(MaxRecent, s.RecentFiles.Count - MaxRecent);
        Save(s);
    }

    public void RemoveRecent(string filePath)
    {
        var s = Current;
        if (s.RecentFiles.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase)) > 0) Save(s);
    }

    private static AppSettings Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged settings file should never stop the app from starting.
            return new AppSettings();
        }
    }
}

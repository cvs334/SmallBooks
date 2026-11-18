using SmallBooks.Data.Company;
using SmallBooks.Data.Settings;

namespace SmallBooks.Shell.Services;

/// <summary>App start-up: reopens the last company once, and remembers why if that failed.</summary>
public sealed class ShellState(CompanySession session, AppSettingsStore settings)
{
    private Task? _startup;

    public string? StartupProblem { get; private set; }

    public Task EnsureStartedAsync() => _startup ??= StartAsync();

    private async Task StartAsync()
    {
        var s = settings.Current;
        if (session.IsOpen || !s.ReopenLastCompany || s.RecentFiles.Count == 0) return;
        var last = s.RecentFiles[0];
        if (!File.Exists(last)) return;
        try
        {
            await session.OpenAsync(last);
        }
        catch (Exception ex)
        {
            StartupProblem = $"Could not reopen {Path.GetFileName(last)}: {ex.Message}";
        }
    }
}

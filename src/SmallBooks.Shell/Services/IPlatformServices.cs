namespace SmallBooks.Shell.Services;

/// <summary>Things only the host (the Windows app, or the browser dev host) can do.</summary>
public interface IPlatformServices
{
    /// <summary>Whether <see cref="PickCompanyFileAsync"/> shows a real file dialog.</summary>
    bool CanPickFiles { get; }

    /// <summary>Shows an Open dialog for .sbk files; null if cancelled.</summary>
    Task<string?> PickCompanyFileAsync();

    /// <summary>Opens a folder in Explorer, if the host can.</summary>
    void ShowFolder(string path);
}

/// <summary>Used when the host offers nothing extra: people type file paths instead.</summary>
public sealed class NoPlatformServices : IPlatformServices
{
    public bool CanPickFiles => false;
    public Task<string?> PickCompanyFileAsync() => Task.FromResult<string?>(null);
    public void ShowFolder(string path) { }
}

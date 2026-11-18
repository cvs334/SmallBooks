using System.Diagnostics;
using SmallBooks.Shell.Services;

namespace SmallBooks.Desktop;

/// <summary>File dialogs and Explorer, which only the Windows app can offer.</summary>
public sealed class WindowsPlatformServices : IPlatformServices
{
    public bool CanPickFiles => true;

    public async Task<string?> PickCompanyFileAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Open a SmallBooks company file",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = [".sbk"],
            }),
        };
        var result = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickAsync(options));
        return result?.FullPath;
    }

    public void ShowFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}

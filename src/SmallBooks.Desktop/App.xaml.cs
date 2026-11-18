using System.Diagnostics;
using SmallBooks.Data.Company;

namespace SmallBooks.Desktop;

public partial class App : Application
{
    private readonly CompanySession _session;

    public App(CompanySession session)
    {
        InitializeComponent();
        _session = session;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "SmallBooks",
            Width = 1280,
            Height = 860,
            MinimumWidth = 900,
            MinimumHeight = 600,
        };

        // Close the company (which makes the automatic backup) when the window closes.
        window.Destroying += (_, _) => CloseCompany();
        return window;
    }

    private void CloseCompany()
    {
        try
        {
            // Run on a worker thread so the UI thread isn't blocked waiting on itself.
            Task.Run(() => _session.CloseAsync()).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Closing the company failed: {ex}");
        }
    }
}

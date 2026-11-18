using Microsoft.Extensions.Logging;
using SmallBooks.Modules.Reports;
using SmallBooks.Shell;
using SmallBooks.Shell.Services;

namespace SmallBooks.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<IPlatformServices, WindowsPlatformServices>();
        builder.Services.AddSmallBooks(app =>
        {
            // The modules compiled into the app. Each company file turns them on or
            // off in Settings, Modules. Add Sales, Purchases and the rest here as
            // they are built. Keep SmallBooks.DevHost/Program.cs in step.
            app.AddModule<ReportsModule>();
        });

        return builder.Build();
    }
}

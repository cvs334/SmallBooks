using SmallBooks.Data.Company;
using SmallBooks.Data.Hosting;
using SmallBooks.DevHost.Components;
using SmallBooks.Modules.Reports;
using SmallBooks.Shell;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSmallBooks(app =>
{
    // Keep this list the same as in SmallBooks.Desktop/MauiProgram.cs.
    app.AddModule<ReportsModule>();

    // Optional overrides, e.g. --SmallBooks:AppDataFolder=/tmp/sb for a clean run.
    var section = builder.Configuration.GetSection("SmallBooks");
    app.Configure(o =>
    {
        o.AppDataFolder = section["AppDataFolder"] ?? o.AppDataFolder;
        o.DefaultCompanyFolder = section["DefaultCompanyFolder"] ?? o.DefaultCompanyFolder;
    });
});

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        new[] { typeof(Routes).Assembly }
            .Concat(app.Services.GetRequiredService<UiCatalog>().PageAssemblies)
            .Distinct()
            .ToArray());

// Back up and close the company when the dev host stops, as the desktop app does on exit.
app.Lifetime.ApplicationStopping.Register(() =>
    app.Services.GetRequiredService<CompanySession>().CloseAsync().GetAwaiter().GetResult());

app.Run();

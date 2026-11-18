using Microsoft.Extensions.DependencyInjection;
using SmallBooks.Data.Backup;
using SmallBooks.Data.Company;
using SmallBooks.Data.Hosting;
using SmallBooks.Data.Ledger;
using SmallBooks.Data.Settings;
using SmallBooks.Modules;

namespace SmallBooks.Data;

/// <summary>Chooses which modules are compiled into the app. Used by the host's start-up code.</summary>
public sealed class SmallBooksBuilder(IServiceCollection services)
{
    internal List<IAccountingModule> Modules { get; } = [];
    internal SmallBooksOptions Options { get; } = new();

    public IServiceCollection Services { get; } = services;

    public SmallBooksBuilder AddModule<TModule>() where TModule : IAccountingModule, new() => AddModule(new TModule());

    public SmallBooksBuilder AddModule(IAccountingModule module)
    {
        Modules.Add(module);
        return this;
    }

    public SmallBooksBuilder Configure(Action<SmallBooksOptions> configure)
    {
        configure(Options);
        return this;
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ledger, company files, backups and the module host, then lets each
    /// module register its own services, screens and reports.
    /// </summary>
    public static IServiceCollection AddSmallBooksData(this IServiceCollection services, Action<SmallBooksBuilder> configure)
    {
        var builder = new SmallBooksBuilder(services);
        configure(builder);

        // Sorting here makes a missing dependency or a circle fail at start-up.
        var catalog = new ModuleCatalog(builder.Modules);
        var ui = new UiCatalog();
        var reports = new ReportCatalog();
        foreach (var module in catalog.Ordered)
        {
            module.ConfigureServices(services);
            module.RegisterUi(ui.For(module.Id));
            module.RegisterReports(reports.For(module.Id));
        }

        services.AddSingleton(builder.Options);
        services.AddSingleton(catalog);
        services.AddSingleton(ui);
        services.AddSingleton(reports);

        services.AddSingleton<AppSettingsStore>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<ModuleHost>();
        services.AddSingleton<IModuleState>(sp => sp.GetRequiredService<ModuleHost>());
        services.AddSingleton<CompanySession>();
        services.AddSingleton<ICompanySession>(sp => sp.GetRequiredService<CompanySession>());
        services.AddSingleton<ICompanyDatabase, ActiveCompanyDatabase>();

        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.AddSingleton<IPostingService, PostingService>();
        services.AddSingleton<ILedgerQueries, LedgerQueries>();
        services.AddSingleton<IChartOfAccounts, ChartOfAccounts>();
        return services;
    }
}

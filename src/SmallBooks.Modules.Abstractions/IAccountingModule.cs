using Microsoft.Extensions.DependencyInjection;

namespace SmallBooks.Modules;

/// <summary>
/// A feature area that plugs into the core ledger: Sales, Purchases, Reports,
/// and later Inventory or Purchase Orders. A module is a class library with one
/// class implementing this interface.
/// <para>
/// Modules never reference each other. They talk through domain events and the
/// extension points in this project, and they record money only by posting
/// journal entries through <see cref="ILedger"/>.
/// </para>
/// </summary>
public interface IAccountingModule
{
    /// <summary>Short, permanent id such as "sales". Used as the table prefix and in the database; never change it.</summary>
    string Id { get; }

    /// <summary>Name shown to people, such as "Sales".</summary>
    string Name { get; }

    /// <summary>One sentence shown in Settings, Modules.</summary>
    string Description { get; }

    Version Version { get; }

    /// <summary>Ids of modules that must be turned on before this one, such as ["sales", "purchases"].</summary>
    IReadOnlyList<string> DependsOn { get; }

    /// <summary>Whether a new company starts with this module turned on.</summary>
    bool EnabledByDefault => true;

    /// <summary>Called once at app start-up, for every compiled-in module, to register its services.</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Creates or updates the module's own tables in the open company file. Called
    /// each time a company is opened with the module turned on, and when it is turned on.
    /// Use <see cref="ICompanyDatabase.MigrateAsync{TContext}"/> for an EF Core context.
    /// </summary>
    Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>Asks for the accounts this module needs, such as Inventory Asset. Safe to call repeatedly.</summary>
    void SeedAccounts(IChartOfAccountsSeeder accounts);

    /// <summary>Adds the module's navigation items, pages, dashboard tiles and settings pages.</summary>
    void RegisterUi(IUiRegistry ui);

    /// <summary>Adds the module's reports to the Reports screen.</summary>
    void RegisterReports(IReportRegistry reports);
}

/// <summary>
/// Convenience base class: override only what the module uses.
/// </summary>
public abstract class AccountingModule : IAccountingModule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public virtual string Description => "";
    public virtual Version Version => GetType().Assembly.GetName().Version ?? new Version(1, 0);
    public virtual IReadOnlyList<string> DependsOn => [];
    public virtual bool EnabledByDefault => true;

    public virtual void ConfigureServices(IServiceCollection services) { }
    public virtual Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual void SeedAccounts(IChartOfAccountsSeeder accounts) { }
    public virtual void RegisterUi(IUiRegistry ui) { }
    public virtual void RegisterReports(IReportRegistry reports) { }
}

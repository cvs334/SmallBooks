using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmallBooks.Data;
using SmallBooks.Shell.Services;

namespace SmallBooks.Shell;

public static class ShellServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything SmallBooks needs: the ledger, company files, modules and screens.
    /// <code>
    /// services.AddSmallBooks(app => app.AddModule&lt;ReportsModule&gt;());
    /// </code>
    /// </summary>
    public static IServiceCollection AddSmallBooks(this IServiceCollection services, Action<SmallBooksBuilder> configure)
    {
        services.AddSmallBooksData(configure);
        services.AddSingleton<ShellState>();
        services.TryAddSingleton<IPlatformServices, NoPlatformServices>();
        return services;
    }
}

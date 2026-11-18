using System.Reflection;

namespace SmallBooks.Modules;

/// <summary>Where a module adds itself to the app's screens.</summary>
public interface IUiRegistry
{
    /// <summary>Adds a link to the left-hand navigation.</summary>
    void AddNavItem(NavItem item);

    /// <summary>
    /// Makes the routable Razor pages (those with @page) in this assembly reachable.
    /// Call once with the module's own assembly.
    /// </summary>
    void AddPages(Assembly assembly);

    /// <summary>Adds a Razor component to the home dashboard. It is shown only while the module is on.</summary>
    void AddDashboardTile(Type componentType, int order = 100);

    /// <summary>Adds a tab to Settings. The component is shown only while the module is on.</summary>
    void AddSettingsPage(string title, Type componentType, int order = 100);
}

/// <param name="Title">Text of the link.</param>
/// <param name="Href">Relative address, such as "sales/invoices".</param>
/// <param name="Group">Navigation group heading, such as "Sales" or "Reports".</param>
/// <param name="Order">Lower numbers sort first.</param>
public sealed record NavItem(string Title, string Href, string Group, int Order = 100);

/// <summary>A navigation item, dashboard tile or settings page together with the module that added it.</summary>
public sealed record ModuleUiItem<T>(string ModuleId, T Item, int Order);

public sealed record DashboardTile(Type ComponentType);

public sealed record SettingsPage(string Title, Type ComponentType);

using System.Reflection;
using SmallBooks.Modules;

namespace SmallBooks.Data.Hosting;

/// <summary>Everything modules added to the screens, each item tagged with the module that added it.</summary>
public sealed class UiCatalog
{
    private readonly List<ModuleUiItem<NavItem>> _nav = [];
    private readonly List<ModuleUiItem<DashboardTile>> _tiles = [];
    private readonly List<ModuleUiItem<SettingsPage>> _settings = [];
    private readonly List<(string ModuleId, Assembly Assembly)> _pages = [];

    public IReadOnlyList<ModuleUiItem<NavItem>> NavItems => _nav;
    public IReadOnlyList<ModuleUiItem<DashboardTile>> DashboardTiles => _tiles;
    public IReadOnlyList<ModuleUiItem<SettingsPage>> SettingsPages => _settings;

    /// <summary>Assemblies holding module pages, for the router.</summary>
    public IReadOnlyList<Assembly> PageAssemblies => _pages.Select(p => p.Assembly).Distinct().ToList();

    internal IUiRegistry For(string moduleId) => new Registry(this, moduleId);

    private sealed class Registry(UiCatalog catalog, string moduleId) : IUiRegistry
    {
        public void AddNavItem(NavItem item) => catalog._nav.Add(new(moduleId, item, item.Order));
        public void AddPages(Assembly assembly) => catalog._pages.Add((moduleId, assembly));
        public void AddDashboardTile(Type componentType, int order = 100) =>
            catalog._tiles.Add(new(moduleId, new DashboardTile(componentType), order));
        public void AddSettingsPage(string title, Type componentType, int order = 100) =>
            catalog._settings.Add(new(moduleId, new SettingsPage(title, componentType), order));
    }
}

/// <summary>Every report modules have listed, tagged with the module that added it.</summary>
public sealed class ReportCatalog
{
    private readonly List<(string ModuleId, ReportDefinition Report)> _reports = [];

    public IReadOnlyList<(string ModuleId, ReportDefinition Report)> Reports => _reports;

    internal IReportRegistry For(string moduleId) => new Registry(this, moduleId);

    private sealed class Registry(ReportCatalog catalog, string moduleId) : IReportRegistry
    {
        public void Add(ReportDefinition report) => catalog._reports.Add((moduleId, report));
    }
}

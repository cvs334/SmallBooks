using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmallBooks.Data.Ledger;
using SmallBooks.Modules;

namespace SmallBooks.Data.Hosting;

/// <summary>
/// Turns modules on and off for the open company, and sets up each module's
/// tables and accounts when a company opens.
/// </summary>
public sealed class ModuleHost(ModuleCatalog catalog, IServiceProvider services) : IModuleState
{
    private HashSet<string> _enabled = [];

    public ModuleCatalog Catalog => catalog;

    public event Action? Changed;

    public bool IsEnabled(string moduleId) => _enabled.Contains(moduleId);

    private ICompanyDatabase Database => services.GetRequiredService<ICompanyDatabase>();

    /// <summary>
    /// Reads which modules this company has turned on, gives newly added modules their
    /// default, and migrates and seeds every module that is on, in dependency order.
    /// </summary>
    internal async Task LoadForCompanyAsync(CancellationToken ct)
    {
        Dictionary<string, ModuleStateRecord> states;
        await using (var db = Database.CreateContext<LedgerDbContext>())
        {
            states = await db.ModuleStates.ToDictionaryAsync(s => s.ModuleId, ct);
            foreach (var module in catalog.Ordered.Where(m => !states.ContainsKey(m.Id)))
            {
                var record = new ModuleStateRecord
                {
                    ModuleId = module.Id,
                    Enabled = module.EnabledByDefault,
                    UpdatedAtUtc = DateTime.UtcNow,
                };
                db.ModuleStates.Add(record);
                states[module.Id] = record;
            }
            await db.SaveChangesAsync(ct);
        }

        var enabled = new HashSet<string>();
        foreach (var module in catalog.Ordered)
        {
            // A module whose dependency is off is treated as off too.
            if (states[module.Id].Enabled && module.DependsOn.All(enabled.Contains))
            {
                await InstallAsync(module, ct);
                enabled.Add(module.Id);
            }
        }

        _enabled = enabled;
        Changed?.Invoke();
    }

    internal void Clear()
    {
        _enabled = [];
        Changed?.Invoke();
    }

    public async Task EnableAsync(string moduleId, CancellationToken ct = default)
    {
        var module = catalog.Find(moduleId) ?? throw new ModuleRuleException($"There is no module '{moduleId}'.");
        if (IsEnabled(moduleId)) return;

        var missing = module.DependsOn.Where(d => !IsEnabled(d)).Select(d => catalog.Find(d)!.Name).ToList();
        if (missing.Count > 0)
            throw new ModuleRuleException($"Turn on {string.Join(" and ", missing)} first; {module.Name} needs it.");

        await InstallAsync(module, ct);
        await SaveEnabledAsync(moduleId, true, ct);
        _enabled.Add(moduleId);
        Changed?.Invoke();
    }

    /// <summary>Turns a module off. Its data stays in the file and returns when it is turned back on.</summary>
    public async Task DisableAsync(string moduleId, CancellationToken ct = default)
    {
        var module = catalog.Find(moduleId) ?? throw new ModuleRuleException($"There is no module '{moduleId}'.");
        if (!IsEnabled(moduleId)) return;

        var dependents = catalog.DependentsOf(moduleId).Where(m => IsEnabled(m.Id)).Select(m => m.Name).ToList();
        if (dependents.Count > 0)
            throw new ModuleRuleException($"Turn off {string.Join(" and ", dependents)} first; it needs {module.Name}.");

        await SaveEnabledAsync(moduleId, false, ct);
        _enabled.Remove(moduleId);
        Changed?.Invoke();
    }

    private async Task InstallAsync(IAccountingModule module, CancellationToken ct)
    {
        await module.MigrateAsync(services, ct);

        var seeder = new ChartOfAccountsSeeder(module.Id);
        module.SeedAccounts(seeder);
        await using var db = Database.CreateContext<LedgerDbContext>();
        await seeder.ApplyAsync(db, ct);

        var state = await db.ModuleStates.SingleAsync(s => s.ModuleId == module.Id, ct);
        var version = module.Version.ToString();
        if (state.InstalledVersion != version)
        {
            state.InstalledVersion = version;
            state.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task SaveEnabledAsync(string moduleId, bool enabled, CancellationToken ct)
    {
        await using var db = Database.CreateContext<LedgerDbContext>();
        var state = await db.ModuleStates.SingleAsync(s => s.ModuleId == moduleId, ct);
        state.Enabled = enabled;
        state.UpdatedAtUtc = DateTime.UtcNow;
        db.AuditLog.Add(AuditLog.Entry(enabled ? "enable" : "disable", "Module", moduleId, null));
        await db.SaveChangesAsync(ct);
    }
}

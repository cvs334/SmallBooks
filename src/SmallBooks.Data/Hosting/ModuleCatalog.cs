using System.Text.RegularExpressions;
using SmallBooks.Modules;

namespace SmallBooks.Data.Hosting;

/// <summary>
/// Every module compiled into the app, sorted so that each module comes after the
/// modules it depends on. Built at start-up; a bad module list stops the app
/// immediately with a clear message rather than failing later.
/// </summary>
public sealed partial class ModuleCatalog
{
    public ModuleCatalog(IEnumerable<IAccountingModule> modules)
    {
        Ordered = Sort(modules.ToList());
    }

    /// <summary>Modules in dependency order: a module always appears after everything it depends on.</summary>
    public IReadOnlyList<IAccountingModule> Ordered { get; }

    public IAccountingModule? Find(string id) => Ordered.FirstOrDefault(m => m.Id == id);

    /// <summary>Modules that list <paramref name="id"/> in their DependsOn.</summary>
    public IEnumerable<IAccountingModule> DependentsOf(string id) => Ordered.Where(m => m.DependsOn.Contains(id));

    [GeneratedRegex("^[a-z][a-z0-9_]{0,31}$")]
    private static partial Regex IdPattern();

    private static List<IAccountingModule> Sort(List<IAccountingModule> modules)
    {
        var byId = new Dictionary<string, IAccountingModule>();
        foreach (var m in modules)
        {
            if (m.Id is null || !IdPattern().IsMatch(m.Id))
                throw new ModuleConfigurationException(
                    $"Module {m.GetType().Name} has id '{m.Id}'. Ids must be lower-case letters, digits or _, starting with a letter.");
            if (m.Id == "core")
                throw new ModuleConfigurationException("The id 'core' is reserved for the ledger itself.");
            if (!byId.TryAdd(m.Id, m))
                throw new ModuleConfigurationException($"Two modules use the id '{m.Id}'.");
        }

        foreach (var m in modules)
            foreach (var dependency in m.DependsOn)
                if (!byId.ContainsKey(dependency))
                    throw new ModuleConfigurationException($"Module '{m.Id}' depends on '{dependency}', which is not in the app.");

        // Depth-first topological sort, visiting modules in the order they were added
        // so that the result is stable and predictable.
        var result = new List<IAccountingModule>(modules.Count);
        var state = new Dictionary<string, bool>(); // false = visiting, true = done
        var path = new Stack<string>();

        void Visit(IAccountingModule m)
        {
            if (state.TryGetValue(m.Id, out var done))
            {
                if (done) return;
                var cycle = path.Reverse().SkipWhile(id => id != m.Id).Append(m.Id);
                throw new ModuleConfigurationException($"Modules depend on each other in a circle: {string.Join(" -> ", cycle)}.");
            }
            state[m.Id] = false;
            path.Push(m.Id);
            foreach (var dependency in m.DependsOn) Visit(byId[dependency]);
            path.Pop();
            state[m.Id] = true;
            result.Add(m);
        }

        foreach (var m in modules) Visit(m);
        return result;
    }
}

public sealed class ModuleConfigurationException(string message) : Exception(message);

/// <summary>A module cannot be turned on or off right now. The message is meant for people.</summary>
public sealed class ModuleRuleException(string message) : Exception(message);

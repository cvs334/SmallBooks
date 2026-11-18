using SmallBooks.Core.Accounts;

namespace SmallBooks.Modules;

/// <summary>
/// Lets a module ask for the accounts it needs. Accounts are matched by key, so
/// asking again on every start-up is harmless, and people may rename them.
/// </summary>
public interface IChartOfAccountsSeeder
{
    /// <summary>Makes sure an account with <see cref="AccountSeed.Key"/> exists, creating it if not. The key is required.</summary>
    void Ensure(AccountSeed account);
}

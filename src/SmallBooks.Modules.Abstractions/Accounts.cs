using SmallBooks.Core.Accounts;

namespace SmallBooks.Modules;

/// <summary>Reads and maintains the chart of accounts, enforcing its rules.</summary>
public interface IChartOfAccounts
{
    Task<IReadOnlyList<Account>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Account?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Account?> GetByKeyAsync(string systemKey, CancellationToken cancellationToken = default);

    /// <exception cref="AccountRuleException">Name or number already used, or another rule is broken.</exception>
    Task<Account> CreateAsync(AccountInput input, CancellationToken cancellationToken = default);

    /// <exception cref="AccountRuleException">See <see cref="CreateAsync"/>.</exception>
    Task<Account> UpdateAsync(int id, AccountInput input, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default);

    /// <summary>Deletes an account that has never been posted to. System accounts cannot be deleted.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> HasActivityAsync(int id, CancellationToken cancellationToken = default);
}

public sealed record AccountInput(
    string Name,
    AccountSubtype Subtype,
    string? Number = null,
    string? Description = null,
    int? ParentId = null);

public sealed class AccountRuleException(string message) : Exception(message);

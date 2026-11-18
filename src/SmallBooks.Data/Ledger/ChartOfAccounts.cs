using Microsoft.EntityFrameworkCore;
using SmallBooks.Core.Accounts;
using SmallBooks.Modules;

namespace SmallBooks.Data.Ledger;

internal sealed class ChartOfAccounts(ICompanyDatabase database) : IChartOfAccounts
{
    public async Task<IReadOnlyList<Account>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => includeInactive || a.IsActive)
            .ToListAsync(cancellationToken);
        return Sort(accounts);
    }

    /// <summary>Type order, then number, then name; children directly after their parent.</summary>
    internal static List<Account> Sort(List<Account> accounts)
    {
        static string Key(Account a) => (a.Number ?? "~") + "\u0001" + a.Name;
        var byParent = accounts.Where(a => a.ParentId is not null).ToLookup(a => a.ParentId!.Value);
        var ids = accounts.Select(a => a.Id).ToHashSet();
        var result = new List<Account>(accounts.Count);
        foreach (var top in accounts.Where(a => a.ParentId is null || !ids.Contains(a.ParentId.Value))
                     .OrderBy(a => a.Type).ThenBy(a => (int)a.Subtype).ThenBy(Key, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(top);
            result.AddRange(byParent[top.Id].OrderBy(Key, StringComparer.OrdinalIgnoreCase));
        }
        return result;
    }

    public async Task<Account?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        return await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Account?> GetByKeyAsync(string systemKey, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        return await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.SystemKey == systemKey, cancellationToken);
    }

    public async Task<bool> HasActivityAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        return await db.JournalLines.AnyAsync(l => l.AccountId == id, cancellationToken);
    }

    public async Task<Account> CreateAsync(AccountInput input, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var all = await db.Accounts.ToListAsync(cancellationToken);
        var clean = Clean(input);
        Validate(clean, existing: null, all, hasActivity: false);

        var account = new Account
        {
            Name = clean.Name,
            Number = clean.Number,
            Description = clean.Description,
            Subtype = clean.Subtype,
            Type = clean.Subtype.TypeOf(),
            ParentId = clean.ParentId,
            IsActive = true,
        };
        db.Accounts.Add(account);
        db.AuditLog.Add(AuditLog.Entry("create", "Account", null, account.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<Account> UpdateAsync(int id, AccountInput input, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var all = await db.Accounts.ToListAsync(cancellationToken);
        var account = all.SingleOrDefault(a => a.Id == id) ?? throw new AccountRuleException("That account no longer exists.");
        var hasActivity = await db.JournalLines.AnyAsync(l => l.AccountId == id, cancellationToken);
        var clean = Clean(input);
        Validate(clean, account, all, hasActivity);

        var before = account.DisplayName;
        account.Name = clean.Name;
        account.Number = clean.Number;
        account.Description = clean.Description;
        account.Subtype = clean.Subtype;
        account.Type = clean.Subtype.TypeOf();
        account.ParentId = clean.ParentId;
        db.AuditLog.Add(AuditLog.Entry("update", "Account", id.ToString(), $"{before} -> {account.DisplayName}"));
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new AccountRuleException("That account no longer exists.");
        if (account.IsActive == active) return;

        if (!active)
        {
            if (account.IsSystem)
                throw new AccountRuleException($"{account.Name} is a system account and must stay active.");
            if (await db.Accounts.AnyAsync(a => a.ParentId == id && a.IsActive, cancellationToken))
                throw new AccountRuleException($"Deactivate or move the accounts under {account.Name} first.");
        }
        else if (account.ParentId is { } parentId &&
                 await db.Accounts.AnyAsync(a => a.Id == parentId && !a.IsActive, cancellationToken))
        {
            throw new AccountRuleException("Reactivate the parent account first.");
        }

        account.IsActive = active;
        db.AuditLog.Add(AuditLog.Entry(active ? "activate" : "deactivate", "Account", id.ToString(), account.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext<LedgerDbContext>();
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new AccountRuleException("That account no longer exists.");
        if (account.IsSystem)
            throw new AccountRuleException($"{account.Name} is a system account and cannot be deleted.");
        if (await db.JournalLines.AnyAsync(l => l.AccountId == id, cancellationToken))
            throw new AccountRuleException($"{account.Name} has transactions, so it cannot be deleted. Deactivate it instead.");
        if (await db.Accounts.AnyAsync(a => a.ParentId == id, cancellationToken))
            throw new AccountRuleException($"Move or delete the accounts under {account.Name} first.");

        db.Accounts.Remove(account);
        db.AuditLog.Add(AuditLog.Entry("delete", "Account", id.ToString(), account.DisplayName));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AccountInput Clean(AccountInput input) => input with
    {
        Name = (input.Name ?? "").Trim(),
        Number = string.IsNullOrWhiteSpace(input.Number) ? null : input.Number.Trim(),
        Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
    };

    private static void Validate(AccountInput input, Account? existing, List<Account> all, bool hasActivity)
    {
        if (input.Name.Length == 0)
            throw new AccountRuleException("Give the account a name.");
        if (input.Name.Length > 100)
            throw new AccountRuleException("Account names can be at most 100 characters.");
        if (input.Number is { Length: > 20 })
            throw new AccountRuleException("Account numbers can be at most 20 characters.");
        if (!Enum.IsDefined(input.Subtype))
            throw new AccountRuleException("Choose an account type.");

        var others = all.Where(a => a.Id != existing?.Id).ToList();
        if (others.Any(a => string.Equals(a.Name, input.Name, StringComparison.OrdinalIgnoreCase)))
            throw new AccountRuleException($"There is already an account named {input.Name}.");
        if (input.Number is not null && others.Any(a => a.Number == input.Number))
            throw new AccountRuleException($"Account number {input.Number} is already used.");

        var newType = input.Subtype.TypeOf();
        if (existing is not null)
        {
            if (existing.IsSystem && input.Subtype != existing.Subtype)
                throw new AccountRuleException($"{existing.Name} is a system account, so its type cannot change.");
            if (hasActivity && newType != existing.Type)
                throw new AccountRuleException(
                    $"{existing.Name} has transactions, so it can only move to another {existing.Type.ToString().ToLower()} type.");
        }

        if (input.ParentId is { } parentId)
        {
            var parent = others.SingleOrDefault(a => a.Id == parentId)
                ?? throw new AccountRuleException("Choose a parent account that exists, and not the account itself.");
            if (parent.ParentId is not null)
                throw new AccountRuleException("Parent accounts can only be one level deep. Choose a top-level account as the parent.");
            if (parent.Type != newType)
                throw new AccountRuleException($"The parent account must also be an {newType.ToString().ToLower()} account.");
            if (existing is not null && all.Any(a => a.ParentId == existing.Id))
                throw new AccountRuleException($"{existing.Name} has accounts under it, so it cannot itself go under another account.");
        }
    }
}

/// <summary>Creates the accounts the starter chart or a module asks for, matching by key so it can run repeatedly.</summary>
internal sealed class ChartOfAccountsSeeder(string ownerModule) : IChartOfAccountsSeeder
{
    private readonly List<AccountSeed> _seeds = [];

    public void Ensure(AccountSeed account)
    {
        if (string.IsNullOrWhiteSpace(account.Key))
            throw new ArgumentException("Accounts a module asks for need a key, so they can be found again after people rename them.", nameof(account));
        _seeds.Add(account);
    }

    /// <summary>Adds the starter chart, including accounts without keys. Used only when a company is created.</summary>
    public void AddStarter(IEnumerable<AccountSeed> seeds) => _seeds.AddRange(seeds);

    public async Task<int> ApplyAsync(LedgerDbContext db, CancellationToken ct)
    {
        if (_seeds.Count == 0) return 0;
        var existing = await db.Accounts.ToListAsync(ct);
        var keys = existing.Where(a => a.SystemKey != null).Select(a => a.SystemKey!).ToHashSet();
        var numbers = existing.Where(a => a.Number != null).Select(a => a.Number!).ToHashSet();
        var names = existing.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var seed in _seeds)
        {
            if (seed.Key is not null && !keys.Add(seed.Key)) continue;

            var name = seed.Name;
            for (var i = 2; names.Contains(name); i++) name = $"{seed.Name} ({i})";
            names.Add(name);

            var number = seed.Number is not null && numbers.Add(seed.Number) ? seed.Number : null;

            db.Accounts.Add(new Account
            {
                SystemKey = seed.Key,
                Number = number,
                Name = name,
                Description = seed.Description,
                Subtype = seed.Subtype,
                Type = seed.Subtype.TypeOf(),
                IsSystem = seed.IsSystem,
                OwnerModule = ownerModule,
                IsActive = true,
            });
            added++;
        }
        await db.SaveChangesAsync(ct);
        return added;
    }
}

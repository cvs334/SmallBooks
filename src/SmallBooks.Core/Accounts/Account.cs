namespace SmallBooks.Core.Accounts;

/// <summary>An account in the chart of accounts.</summary>
public class Account
{
    public int Id { get; set; }

    /// <summary>Optional account number such as "1000". Unique when present.</summary>
    public string? Number { get; set; }

    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public AccountType Type { get; set; }
    public AccountSubtype Subtype { get; set; }

    /// <summary>Optional parent for grouping on reports (one level only).</summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// A stable key for accounts the app or a module relies on, such as "ar" or
    /// "retained_earnings". Code finds these accounts by key, never by name,
    /// so people can rename them freely.
    /// </summary>
    public string? SystemKey { get; set; }

    /// <summary>System accounts cannot be deleted, deactivated or moved to another type.</summary>
    public bool IsSystem { get; set; }

    /// <summary>The module that created this account ("core" for the starter chart).</summary>
    public string? OwnerModule { get; set; }

    public bool IsActive { get; set; } = true;

    public NormalBalance NormalBalance => Type.NormalBalance();

    /// <summary>"1000 · Checking", or just the name when there is no number.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Number) ? Name : $"{Number} · {Name}";
}

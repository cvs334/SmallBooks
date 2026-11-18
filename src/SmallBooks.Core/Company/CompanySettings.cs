namespace SmallBooks.Core.Company;

/// <summary>Company-wide settings. There is exactly one row per company file.</summary>
public class CompanySettings
{
    public int Id { get; set; } = 1;

    public string CompanyName { get; set; } = "";

    /// <summary>Month the fiscal year starts in, 1 = January.</summary>
    public int FiscalYearStartMonth { get; set; } = 1;

    /// <summary>Nothing can post on or before this date. Null means no periods are closed.</summary>
    public DateOnly? LockDate { get; set; }

    /// <summary>ISO currency code. Version 1 keeps the books in a single currency.</summary>
    public string Currency { get; set; } = "USD";

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Version of the file format, so later releases can tell old files apart.</summary>
    public int FileFormatVersion { get; set; } = 1;
}

/// <summary>One line of the audit log: who changed what, and when.</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime AtUtc { get; set; }
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}

/// <summary>The next number to hand out for a kind of document, such as JE-0001 or INV-0001.</summary>
public class NumberSequence
{
    public string Key { get; set; } = "";
    public string Prefix { get; set; } = "";
    public long NextValue { get; set; } = 1;
    public int Padding { get; set; } = 4;

    public string Format(long value) => Prefix + value.ToString().PadLeft(Padding, '0');
}

namespace SmallBooks.Core.Accounts;

/// <summary>One account to create when a company is set up or a module is turned on.</summary>
/// <param name="Key">Stable key; required for system accounts, optional otherwise.</param>
public sealed record AccountSeed(
    string? Key,
    string? Number,
    string Name,
    AccountSubtype Subtype,
    bool IsSystem = false,
    string? Description = null);

/// <summary>
/// The chart of accounts a new company starts with: a small service business.
/// People can rename, renumber, add and deactivate accounts afterwards; only the
/// system accounts are locked.
/// </summary>
public static class StarterChart
{
    public static IReadOnlyList<AccountSeed> Accounts { get; } =
    [
        // Assets
        new(null, "1000", "Checking", AccountSubtype.Bank),
        new(null, "1010", "Savings", AccountSubtype.Bank),
        new(null, "1050", "Petty Cash", AccountSubtype.Cash),
        new(SystemAccounts.AccountsReceivable, "1100", "Accounts Receivable", AccountSubtype.AccountsReceivable, true,
            "Money customers owe on invoices."),
        new(SystemAccounts.UndepositedFunds, "1150", "Undeposited Funds", AccountSubtype.OtherCurrentAsset, true,
            "Payments received but not yet deposited at the bank."),
        new(null, "1500", "Equipment", AccountSubtype.FixedAsset),
        new(null, "1550", "Accumulated Depreciation", AccountSubtype.FixedAsset),

        // Liabilities
        new(SystemAccounts.AccountsPayable, "2000", "Accounts Payable", AccountSubtype.AccountsPayable, true,
            "Money owed to vendors on bills."),
        new(null, "2100", "Credit Card", AccountSubtype.CreditCard),
        new(SystemAccounts.SalesTaxPayable, "2200", "Sales Tax Payable", AccountSubtype.OtherCurrentLiability, true),
        new(SystemAccounts.ReimbursementsPayable, "2300", "Reimbursements Payable", AccountSubtype.OtherCurrentLiability, true,
            "Business costs paid with personal money, owed back to that person."),
        new(null, "2500", "Loans Payable", AccountSubtype.LongTermLiability),

        // Equity
        new(null, "3000", "Owner's Equity", AccountSubtype.Equity),
        new(null, "3100", "Owner's Draws", AccountSubtype.Equity),
        new(SystemAccounts.OpeningBalanceEquity, "3200", "Opening Balance Equity", AccountSubtype.Equity, true,
            "Offsets opening balances entered when the books are set up."),
        new(SystemAccounts.RetainedEarnings, "3900", "Retained Earnings", AccountSubtype.Equity, true,
            "Profit and loss from earlier years."),

        // Income
        new(null, "4000", "Service Income", AccountSubtype.Income),
        new(null, "4100", "Product Sales", AccountSubtype.Income),
        new(null, "4900", "Other Income", AccountSubtype.OtherIncome),

        // Cost of goods sold
        new(null, "5000", "Shop Supplies and Materials", AccountSubtype.CostOfGoodsSold,
            Description: "Powder, chemicals and other materials used up doing the work."),

        // Expenses
        new(null, "6000", "Advertising", AccountSubtype.Expense),
        new(null, "6100", "Bank and Card Fees", AccountSubtype.Expense),
        new(null, "6200", "Insurance", AccountSubtype.Expense),
        new(null, "6300", "Office Supplies", AccountSubtype.Expense),
        new(null, "6400", "Rent", AccountSubtype.Expense),
        new(null, "6500", "Repairs and Maintenance", AccountSubtype.Expense),
        new(null, "6600", "Utilities", AccountSubtype.Expense),
        new(null, "6700", "Vehicle and Mileage", AccountSubtype.Expense),
        new(null, "6800", "Professional Fees", AccountSubtype.Expense),
        new(null, "6900", "Meals", AccountSubtype.Expense),
        new(null, "6950", "Miscellaneous", AccountSubtype.Expense),
    ];
}

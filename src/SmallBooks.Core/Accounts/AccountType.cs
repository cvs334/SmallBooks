namespace SmallBooks.Core.Accounts;

/// <summary>The five kinds of account every set of books is built from.</summary>
public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    Expense = 5,
}

/// <summary>
/// A finer grouping inside each <see cref="AccountType"/>. The subtype decides
/// where an account shows on reports and which screens offer it (for example,
/// only Bank and Credit Card accounts appear as places money is paid from).
/// </summary>
public enum AccountSubtype
{
    // Assets
    Bank = 100,
    Cash = 101,
    AccountsReceivable = 102,
    OtherCurrentAsset = 103,
    Inventory = 104,
    FixedAsset = 105,
    OtherAsset = 106,

    // Liabilities
    AccountsPayable = 200,
    CreditCard = 201,
    OtherCurrentLiability = 202,
    LongTermLiability = 203,

    // Equity
    Equity = 300,

    // Income
    Income = 400,
    OtherIncome = 401,

    // Expenses
    CostOfGoodsSold = 500,
    Expense = 501,
    OtherExpense = 502,
}

public enum NormalBalance
{
    Debit,
    Credit,
}

public static class AccountTypeExtensions
{
    public static AccountType TypeOf(this AccountSubtype subtype) => (int)subtype switch
    {
        >= 100 and < 200 => AccountType.Asset,
        >= 200 and < 300 => AccountType.Liability,
        >= 300 and < 400 => AccountType.Equity,
        >= 400 and < 500 => AccountType.Income,
        >= 500 and < 600 => AccountType.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(subtype), subtype, "Unknown account subtype."),
    };

    /// <summary>Assets and expenses grow with debits; liabilities, equity and income grow with credits.</summary>
    public static NormalBalance NormalBalance(this AccountType type) =>
        type is AccountType.Asset or AccountType.Expense ? Core.Accounts.NormalBalance.Debit : Core.Accounts.NormalBalance.Credit;

    public static IEnumerable<AccountSubtype> Subtypes(this AccountType type) =>
        Enum.GetValues<AccountSubtype>().Where(s => s.TypeOf() == type);

    /// <summary>True for the accounts that appear on the balance sheet (as opposed to profit and loss).</summary>
    public static bool IsBalanceSheet(this AccountType type) =>
        type is AccountType.Asset or AccountType.Liability or AccountType.Equity;

    public static string DisplayName(this AccountSubtype subtype) => subtype switch
    {
        AccountSubtype.Bank => "Bank",
        AccountSubtype.Cash => "Cash",
        AccountSubtype.AccountsReceivable => "Accounts receivable",
        AccountSubtype.OtherCurrentAsset => "Other current asset",
        AccountSubtype.Inventory => "Inventory",
        AccountSubtype.FixedAsset => "Fixed asset",
        AccountSubtype.OtherAsset => "Other asset",
        AccountSubtype.AccountsPayable => "Accounts payable",
        AccountSubtype.CreditCard => "Credit card",
        AccountSubtype.OtherCurrentLiability => "Other current liability",
        AccountSubtype.LongTermLiability => "Long-term liability",
        AccountSubtype.Equity => "Equity",
        AccountSubtype.Income => "Income",
        AccountSubtype.OtherIncome => "Other income",
        AccountSubtype.CostOfGoodsSold => "Cost of goods sold",
        AccountSubtype.Expense => "Expense",
        AccountSubtype.OtherExpense => "Other expense",
        _ => subtype.ToString(),
    };
}

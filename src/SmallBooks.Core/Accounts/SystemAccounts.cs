namespace SmallBooks.Core.Accounts;

/// <summary>Keys of the accounts the core ledger and built-in modules look up by key.</summary>
public static class SystemAccounts
{
    public const string AccountsReceivable = "ar";
    public const string AccountsPayable = "ap";
    public const string UndepositedFunds = "undeposited_funds";
    public const string SalesTaxPayable = "sales_tax_payable";
    public const string ReimbursementsPayable = "reimbursements_payable";
    public const string OpeningBalanceEquity = "opening_balance_equity";
    public const string RetainedEarnings = "retained_earnings";
}

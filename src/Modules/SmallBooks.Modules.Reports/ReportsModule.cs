namespace SmallBooks.Modules.Reports;

/// <summary>Read-only reports over the ledger. It posts nothing.</summary>
public sealed class ReportsModule : AccountingModule
{
    public const string ModuleId = "reports";

    public override string Id => ModuleId;
    public override string Name => "Reports";
    public override string Description => "Trial balance now; profit and loss, balance sheet, general ledger and aging reports next.";

    public override void RegisterUi(IUiRegistry ui)
    {
        ui.AddPages(typeof(ReportsModule).Assembly);
        ui.AddDashboardTile(typeof(BooksInBalanceTile), order: 10);
    }

    public override void RegisterReports(IReportRegistry reports)
    {
        reports.Add(new ReportDefinition(
            "Trial balance",
            "reports/trial-balance",
            "Accountant",
            "Every account's balance on a date, with debits and credits totalled to prove the books balance.",
            Order: 10));
    }
}

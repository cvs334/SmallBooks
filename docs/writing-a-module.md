# Writing a module

A module is a class library (a Razor class library if it has screens) that references only
`SmallBooks.Modules.Abstractions` and `SmallBooks.Ui.Shared`. `SmallBooks.Modules.Reports` is a working example.

## 1. The module class

```csharp
public sealed class SalesModule : AccountingModule
{
    public override string Id => "sales";               // permanent; used for table prefixes
    public override string Name => "Sales";
    public override string Description => "Customers, invoices and payments received.";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<InvoiceService>();
        services.AddSingleton<IItemCatalog, BasicItemCatalog>();
    }

    public override Task MigrateAsync(IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ICompanyDatabase>().MigrateAsync<SalesDbContext>(ct);

    public override void SeedAccounts(IChartOfAccountsSeeder accounts) =>
        accounts.Ensure(new AccountSeed("sales_discounts", "4950", "Sales Discounts", AccountSubtype.Income, IsSystem: true));

    public override void RegisterUi(IUiRegistry ui)
    {
        ui.AddPages(typeof(SalesModule).Assembly);
        ui.AddNavItem(new NavItem("Invoices", "sales/invoices", "Sales", Order: 10));
        ui.AddNavItem(new NavItem("Customers", "sales/customers", "Sales", Order: 20));
        ui.AddDashboardTile(typeof(UnpaidInvoicesTile));
        ui.AddSettingsPage("Sales", typeof(SalesSettings));
    }

    public override void RegisterReports(IReportRegistry reports) =>
        reports.Add(new ReportDefinition("AR aging", "sales/reports/aging", "Sales", "Who owes what, by age."));
}
```

Then add it in `SmallBooks.Desktop/MauiProgram.cs` (and `SmallBooks.DevHost/Program.cs`):

```csharp
app.AddModule<ReportsModule>();
app.AddModule<SalesModule>();
```

## 2. Its own tables

```csharp
[MigrationsHistoryTable("__sales_migrations")]
public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    protected override void OnModelCreating(ModelBuilder b) => b.Entity<Invoice>().ToTable("sales_Invoice");
}
```

* Prefix every table with the module id.
* Refer to core records (accounts, journal entries) by id only; don't add foreign keys into another module's tables.
* Add an `IDesignTimeDbContextFactory<SalesDbContext>` (copy `src/SmallBooks.Data/Ledger/DesignTimeFactory.cs`) so
  `dotnet ef migrations add Initial --project src/Modules/SmallBooks.Modules.Sales` works.

## 3. Recording money

Never write to the core tables. Save the document and post its entry in one unit of work (see
[architecture.md](architecture.md#saving-a-document-and-its-entry-together)). Find system accounts by key:

```csharp
var ar = await chart.GetByKeyAsync(SystemAccounts.AccountsReceivable, ct);
```

To change a posted document, reverse its entry with `work.Ledger.ReverseAsync(...)` and post a new one.

## 4. Talking to other modules

Never reference another module's project. Instead:

* Raise a domain event (`IDomainEvent`) with `work.PublishAsync`, and handle others' events with
  `IDomainEventHandler<T>`. Shared event types live in Abstractions.
* Use the extension points in Abstractions: `IItemCatalog` (Inventory replaces Sales' basic one) and
  `IDocumentSource` (Purchase Orders offers "create bill from PO").
* Declare hard dependencies in `DependsOn`, such as `["sales", "purchases"]`.

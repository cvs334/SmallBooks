using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SmallBooks.Core.Accounts;
using SmallBooks.Data.Hosting;
using SmallBooks.Modules;
using SmallBooks.Modules.Reports;

namespace SmallBooks.Tests;

public class ModuleTests
{
    private sealed class FakeModule(string id, params string[] dependsOn) : AccountingModule
    {
        public override string Id => id;
        public override string Name => char.ToUpperInvariant(id[0]) + id[1..];
        public override IReadOnlyList<string> DependsOn => dependsOn;
    }

    [Fact]
    public void Modules_are_ordered_after_their_dependencies()
    {
        var catalog = new ModuleCatalog([
            new FakeModule("purchaseorders", "purchases", "inventory"),
            new FakeModule("inventory", "sales", "purchases"),
            new FakeModule("sales"),
            new FakeModule("purchases"),
        ]);
        var order = catalog.Ordered.Select(m => m.Id).ToList();
        Assert.True(order.IndexOf("sales") < order.IndexOf("inventory"));
        Assert.True(order.IndexOf("purchases") < order.IndexOf("inventory"));
        Assert.True(order.IndexOf("inventory") < order.IndexOf("purchaseorders"));
    }

    [Fact]
    public void Bad_module_lists_fail_at_start_up()
    {
        Assert.Throws<ModuleConfigurationException>(() => new ModuleCatalog([new FakeModule("a", "missing")]));
        Assert.Throws<ModuleConfigurationException>(() => new ModuleCatalog([new FakeModule("a", "b"), new FakeModule("b", "a")]));
        Assert.Throws<ModuleConfigurationException>(() => new ModuleCatalog([new FakeModule("a"), new FakeModule("a")]));
        Assert.Throws<ModuleConfigurationException>(() => new ModuleCatalog([new FakeModule("Bad Id")]));
        Assert.Throws<ModuleConfigurationException>(() => new ModuleCatalog([new FakeModule("core")]));
    }

    [Fact]
    public async Task Turning_modules_on_and_off_respects_dependencies()
    {
        await using var books = await TestBooks.CreateAsync(b => b
            .AddModule(new FakeModule("sales"))
            .AddModule(new FakeModule("inventory", "sales")));
        var host = books.Services.GetRequiredService<ModuleHost>();
        Assert.True(host.IsEnabled("sales"));
        Assert.True(host.IsEnabled("inventory"));

        await Assert.ThrowsAsync<ModuleRuleException>(() => host.DisableAsync("sales"));
        await host.DisableAsync("inventory");
        await host.DisableAsync("sales");
        await Assert.ThrowsAsync<ModuleRuleException>(() => host.EnableAsync("inventory"));

        // The choice is saved in the company file.
        await books.Session.CloseAsync();
        await books.Session.OpenAsync(books.CompanyFile);
        Assert.False(host.IsEnabled("sales"));
        await host.EnableAsync("sales");
        await host.EnableAsync("inventory");
        Assert.True(host.IsEnabled("inventory"));
    }

    [Fact]
    public async Task A_module_gets_its_own_tables_history_and_accounts()
    {
        await using var books = await TestBooks.CreateAsync(b => b.AddModule<WidgetModule>());

        await using (var db = books.Services.GetRequiredService<ICompanyDatabase>().CreateContext<WidgetDbContext>())
        {
            db.Widgets.Add(new Widget { Name = "Sprocket" });
            await db.SaveChangesAsync();
            Assert.Equal(["20261118000000_Widgets"], await db.Database.GetAppliedMigrationsAsync());
        }

        var account = await books.Chart.GetByKeyAsync("widget_stock");
        Assert.NotNull(account);
        Assert.Equal("widgets", account.OwnerModule);

        // Seeding again on reopen does not duplicate, even after a rename.
        await books.Chart.UpdateAsync(account.Id, new AccountInput("Widgets on Hand", account.Subtype, account.Number));
        await books.Session.CloseAsync();
        await books.Session.OpenAsync(books.CompanyFile);
        Assert.Single(await books.Chart.ListAsync(), a => a.SystemKey == "widget_stock");
    }

    [Fact]
    public async Task The_reports_module_registers_its_report_and_tile()
    {
        await using var books = await TestBooks.CreateAsync(b => b.AddModule<ReportsModule>());
        var reports = books.Services.GetRequiredService<ReportCatalog>();
        var ui = books.Services.GetRequiredService<UiCatalog>();
        Assert.Contains(reports.Reports, r => r.ModuleId == "reports" && r.Report.Href == "reports/trial-balance");
        Assert.Contains(ui.DashboardTiles, t => t.ModuleId == "reports");
        Assert.Contains(typeof(ReportsModule).Assembly, ui.PageAssemblies);
        Assert.True(books.Services.GetRequiredService<IModuleState>().IsEnabled("reports"));
    }

    // A tiny module with its own DbContext and a hand-written migration, standing in for Sales or Inventory.
    public sealed class WidgetModule : AccountingModule
    {
        public override string Id => "widgets";
        public override string Name => "Widgets";

        public override Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            services.GetRequiredService<ICompanyDatabase>().MigrateAsync<WidgetDbContext>(cancellationToken);

        public override void SeedAccounts(IChartOfAccountsSeeder accounts) =>
            accounts.Ensure(new AccountSeed("widget_stock", "1300", "Widget Stock", AccountSubtype.Inventory, IsSystem: true));
    }

    public sealed class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    [MigrationsHistoryTable("__widgets_migrations")]
    public sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options) : DbContext(options)
    {
        public DbSet<Widget> Widgets => Set<Widget>();
        protected override void OnModelCreating(ModelBuilder b) => b.Entity<Widget>().ToTable("widgets_Widget");
    }

    [DbContext(typeof(WidgetDbContext))]
    [Migration("20261118000000_Widgets")]
    public sealed class WidgetsMigration : Migration
    {
        protected override void Up(MigrationBuilder m) => m.CreateTable(
            name: "widgets_Widget",
            columns: t => new
            {
                Id = t.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = t.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_widgets_Widget", x => x.Id));
    }
}

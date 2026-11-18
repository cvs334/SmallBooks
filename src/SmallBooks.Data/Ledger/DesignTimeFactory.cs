using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmallBooks.Data.Ledger;

/// <summary>
/// Used only by "dotnet ef migrations add" so the tools can build the model
/// without starting the app. Run from the repository root:
/// <c>dotnet ef migrations add Name --project src/SmallBooks.Data --output-dir Ledger/Migrations</c>
/// </summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite("Data Source=design-time.sbk", o => o.MigrationsHistoryTable("__core_migrations"))
            .Options);
}

using Microsoft.EntityFrameworkCore;
using SmallBooks.Core.Accounts;
using SmallBooks.Core.Company;
using SmallBooks.Core.Journal;
using SmallBooks.Modules;

namespace SmallBooks.Data.Ledger;

/// <summary>The core tables of a company file. All are prefixed "core_".</summary>
[MigrationsHistoryTable("__core_migrations")]
public class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<CompanySettings> Settings => Set<CompanySettings>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
    public DbSet<ModuleStateRecord> ModuleStates => Set<ModuleStateRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CompanySettings>(e =>
        {
            e.ToTable("core_CompanySettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        });

        b.Entity<Account>(e =>
        {
            e.ToTable("core_Account");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Number).HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.SystemKey).HasMaxLength(64);
            e.Property(x => x.OwnerModule).HasMaxLength(64);
            e.HasIndex(x => x.Number).IsUnique().HasFilter("Number IS NOT NULL");
            e.HasIndex(x => x.SystemKey).IsUnique().HasFilter("SystemKey IS NOT NULL");
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.NormalBalance);
            e.Ignore(x => x.DisplayName);
        });

        b.Entity<JournalEntry>(e =>
        {
            e.ToTable("core_JournalEntry");
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.SourceModule, x.SourceType, x.SourceId });
            e.Property(x => x.Memo).HasMaxLength(500);
            e.Property(x => x.SourceModule).HasMaxLength(64).IsRequired();
            e.Property(x => x.SourceType).HasMaxLength(64);
            e.Property(x => x.SourceId).HasMaxLength(64);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReversedById).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.IsReversed);
            e.Ignore(x => x.IsReversal);
            e.Ignore(x => x.TotalDebits);
            e.Ignore(x => x.TotalCredits);
        });

        b.Entity<JournalLine>(e =>
        {
            e.ToTable("core_JournalLine", t =>
            {
                // Belt and braces: the database itself refuses a line that is not
                // exactly one positive debit or one positive credit.
                t.HasCheckConstraint("CK_core_JournalLine_OneSide",
                    "(DebitCents > 0 AND CreditCents = 0) OR (CreditCents > 0 AND DebitCents = 0)");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountId);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.Debit);
            e.Ignore(x => x.Credit);
            e.Ignore(x => x.SignedAmount);
        });

        b.Entity<NumberSequence>(e =>
        {
            e.ToTable("core_NumberSequence");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(64);
            e.Property(x => x.Prefix).HasMaxLength(20);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("core_AuditLog");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AtUtc);
            e.Property(x => x.User).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.Entity).HasMaxLength(64);
            e.Property(x => x.EntityId).HasMaxLength(64);
        });

        b.Entity<ModuleStateRecord>(e =>
        {
            e.ToTable("core_ModuleState");
            e.HasKey(x => x.ModuleId);
            e.Property(x => x.ModuleId).HasMaxLength(64);
            e.Property(x => x.InstalledVersion).HasMaxLength(32);
        });
    }
}

/// <summary>Whether a module is turned on in this company file, and which version last set up its tables.</summary>
public class ModuleStateRecord
{
    public string ModuleId { get; set; } = "";
    public bool Enabled { get; set; }
    public string? InstalledVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

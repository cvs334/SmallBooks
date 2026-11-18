# How SmallBooks fits together

```
SmallBooks.Desktop (MAUI window)        SmallBooks.DevHost (browser, debugging only)
          \                                   /
           +------ SmallBooks.Shell (screens) +
                       |            \
                       |             SmallBooks.Ui.Shared (components)
                 SmallBooks.Data  (EF Core + SQLite, posting, company files, module host, backups)
                       |
        SmallBooks.Modules.Abstractions  (the module contract)   <---- every module references only this + Ui.Shared
                       |
                 SmallBooks.Core  (Money, accounts, journal entries, posting rules)
```

## The ledger

* Money is `Money`, a whole number of cents, so sums never drift.
* The only way to record money is `ILedger.PostAsync` (or `IPostingService`, which wraps it in its own transaction).
  It checks `PostingRules` first: balanced, at least two lines, each line one positive side, active accounts,
  dated after the lock date. The `core_JournalLine` table has a check constraint as a second guard.
* Posted entries are never edited. `ReverseAsync` posts the mirror image and links the two.
* Every entry gets a JE number from the `journal` number sequence, whichever module posted it.
  A module keeps its own document number (INV-0001) and records `SourceModule`, `SourceType` and `SourceId` on the entry.
* `ILedgerQueries` answers balances, the trial balance, account registers and entry lists.

## One company, one file

A company is one SQLite file (`.sbk`). Core tables are prefixed `core_` and keep their migration history in
`__core_migrations`. Each module's DbContext uses the same file, its own table prefix (`sales_Invoice`), and its own
history table (`__sales_migrations`), named with `[MigrationsHistoryTable]`. That lets a module be added to an
existing company without touching anyone else's migrations.

Connections are not pooled, so when a company closes nothing holds the file open and it can be copied or restored
straight away.

## Saving a document and its entry together

`IUnitOfWork` hands out DbContexts that share one connection and one transaction:

```csharp
await using var work = await unitOfWork.BeginAsync(ct);
var db = work.GetContext<SalesDbContext>();
invoice.Number = await work.NextNumberAsync("invoice", "INV-", ct);
db.Invoices.Add(invoice);
await db.SaveChangesAsync(ct);
invoice.JournalEntryId = (await work.Ledger.PostAsync(draft, ct)).Id;
await work.PublishAsync(new InvoicePosted(invoice.Id), ct);   // handlers run inside the same transaction
await work.CommitAsync(ct);                                     // or dispose without committing to roll back
```

## Modules

Modules are compiled into the app (listed in `MauiProgram.cs`) and switched on or off per company in
Settings, Modules. At start-up the host sorts them by `DependsOn` (a missing dependency or a circle stops the
app with a clear message), lets each register services, screens, dashboard tiles, settings pages and reports.
When a company opens, every module that is on is migrated and asked for its accounts, in dependency order.
Turning a module off hides it but keeps its data. See [writing-a-module.md](writing-a-module.md).

## Backups

`BackupService` uses SQLite's online backup, so a copy is always complete and consistent. Backups are made when a
company closes (including when the app exits), on Back up now, before a restore, and before a file is upgraded.
They go to a `Backups` folder beside the company file, or to a folder chosen in Settings. The newest 30 are kept.

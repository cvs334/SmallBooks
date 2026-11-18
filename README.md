# SmallBooks

A small desktop accounting app for one business on one Windows PC: a trimmed-down
QuickBooks Desktop / Zoho Books, built in C# with .NET MAUI (Blazor Hybrid) and
SQLite. The design is in the plan *Accounting App: Architecture and Module Plan*;
this repository follows it phase by phase.

**Phase 1 (this code): the foundation.** Company files, the chart of accounts,
manual journal entries, the trial balance, backups, and the plug-in module system
that Sales, Purchases, Banking, Receipt Inbox, Inventory and Purchase Orders will
use later.

## Run it in Visual Studio 2026

1. Install Visual Studio 2026 with the **.NET Multi-platform App UI development** workload
   (it brings the .NET 10 SDK and MAUI).
2. Open `SmallBooks.slnx`.
3. Make **SmallBooks.Desktop** the startup project, choose **Windows Machine**, and press F5.
4. On the start screen, type the company name and press **Create company**. The books are
   saved as one file, by default `Documents\SmallBooks\<Company>.sbk`.

Run the tests from Test Explorer, or `dotnet test --project tests/SmallBooks.Tests`.

To debug the screens in a web browser instead (handy for browser dev tools), start
**SmallBooks.DevHost** and open http://localhost:5180. It uses the same company files.

## What phase 1 does

| Area | What works |
| --- | --- |
| Company files | Create, open, close, recent list, reopen last company on start-up. Refuses files that aren't SmallBooks files without touching them, and files from a newer version. |
| Chart of accounts | Starter chart for a service business; add, rename, renumber, group under a parent (one level), deactivate, delete if unused. System accounts (AR, AP, Sales Tax Payable, Undeposited Funds, Reimbursements Payable, Opening Balance Equity, Retained Earnings) are found by key, can be renamed, but can't be deleted, deactivated or retyped. |
| Journal entries | Multi-line entries with debit and credit columns, live totals, and the balancing amount filled in for you. Posted entries are never edited; **Reverse** posts the mirror entry. Numbered JE-0001 onward. |
| Posting rules | Every entry, from any module, must balance, use active accounts, have at least two lines, and be dated after the lock date. The database also refuses a line with both or neither side. |
| Account registers | Every account's transactions with a running balance, filterable by date. |
| Trial balance | Any as-of date, grouped by account type, with a balance check and Print. |
| Home | Cash and bank, receivables, payables, profit this fiscal year, books-in-balance tile, recent entries. |
| Settings | Company name, fiscal year start, lock date; modules on/off; backups. |
| Backups | Automatic backup on close or exit (keeps the last 30), Back up now, Restore (the current file is backed up first), and a choice of folder, so it can point at OneDrive. A backup is also made before a file is upgraded to a newer format. |
| Audit log | Posting, reversing, account changes, lock date and module changes are recorded in the file. |

## Solution layout

| Project | What it holds |
| --- | --- |
| `SmallBooks.Desktop` | The Windows app: a MAUI window hosting the screens. Lists the modules compiled in. |
| `SmallBooks.Shell` | The screens: start screen, layout, home, accounts, journal, reports list, settings. |
| `SmallBooks.Ui.Shared` | Small shared components: money box, account picker, notices. |
| `SmallBooks.Core` | Ledger domain: `Money` (whole cents), accounts, journal entries, posting rules, starter chart. No database code. |
| `SmallBooks.Modules.Abstractions` | The module contract (`IAccountingModule`) and the services modules use: `ILedger`, `IUnitOfWork`, `ILedgerQueries`, `IChartOfAccounts`, domain events, `IItemCatalog`, `IDocumentSource`. |
| `SmallBooks.Data` | EF Core + SQLite: the core tables, posting service, company files, module host, backups. |
| `Modules/SmallBooks.Modules.Reports` | The first module: trial balance and the books-in-balance tile. |
| `SmallBooks.DevHost` | Runs the screens in a browser for debugging. Not shipped. |
| `SmallBooks.Tests` | xUnit tests: money, posting rules, ledger, company files, backups, modules. |

More detail: [docs/architecture.md](docs/architecture.md) and [docs/writing-a-module.md](docs/writing-a-module.md).

## Next phases

1. **Foundation** (this code).
2. **Sell and spend:** Sales (customers with terms or COD, pickup screen) and Purchases modules, invoice PDF and email, optional sales tax, P&L and balance sheet.
3. **Zoho import and parallel run.**
4. **Receipts and POS API:** built-in web server, Receipt Inbox, phone PWA, POS API, Mileage.
5. **Bank and close:** Banking module, reconciliation, AR/AP aging, period close, cut over from Zoho.
6. **Add-ons:** Inventory (including consumables such as powder), then Purchase Orders.

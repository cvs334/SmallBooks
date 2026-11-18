using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmallBooks.Data.Ledger.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_Account",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Subtype = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    SystemKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    OwnerModule = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_Account", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_Account_core_Account_ParentId",
                        column: x => x.ParentId,
                        principalTable: "core_Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "core_AuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    User = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Entity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_CompanySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    LockDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FileFormatVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_CompanySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_JournalEntry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Memo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SourceModule = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReversalOfId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReversedById = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_JournalEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_JournalEntry_core_JournalEntry_ReversalOfId",
                        column: x => x.ReversalOfId,
                        principalTable: "core_JournalEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_JournalEntry_core_JournalEntry_ReversedById",
                        column: x => x.ReversedById,
                        principalTable: "core_JournalEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "core_ModuleState",
                columns: table => new
                {
                    ModuleId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstalledVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_ModuleState", x => x.ModuleId);
                });

            migrationBuilder.CreateTable(
                name: "core_NumberSequence",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NextValue = table.Column<long>(type: "INTEGER", nullable: false),
                    Padding = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_NumberSequence", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "core_JournalLine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JournalEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    DebitCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CreditCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ContactId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_JournalLine", x => x.Id);
                    table.CheckConstraint("CK_core_JournalLine_OneSide", "(DebitCents > 0 AND CreditCents = 0) OR (CreditCents > 0 AND DebitCents = 0)");
                    table.ForeignKey(
                        name: "FK_core_JournalLine_core_Account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "core_Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_JournalLine_core_JournalEntry_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "core_JournalEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_Account_Number",
                table: "core_Account",
                column: "Number",
                unique: true,
                filter: "Number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_core_Account_ParentId",
                table: "core_Account",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_core_Account_SystemKey",
                table: "core_Account",
                column: "SystemKey",
                unique: true,
                filter: "SystemKey IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_core_AuditLog_AtUtc",
                table: "core_AuditLog",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_core_JournalEntry_Date",
                table: "core_JournalEntry",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_core_JournalEntry_Number",
                table: "core_JournalEntry",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_JournalEntry_ReversalOfId",
                table: "core_JournalEntry",
                column: "ReversalOfId");

            migrationBuilder.CreateIndex(
                name: "IX_core_JournalEntry_ReversedById",
                table: "core_JournalEntry",
                column: "ReversedById");

            migrationBuilder.CreateIndex(
                name: "IX_core_JournalEntry_SourceModule_SourceType_SourceId",
                table: "core_JournalEntry",
                columns: new[] { "SourceModule", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_core_JournalLine_AccountId",
                table: "core_JournalLine",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_core_JournalLine_JournalEntryId",
                table: "core_JournalLine",
                column: "JournalEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_AuditLog");

            migrationBuilder.DropTable(
                name: "core_CompanySettings");

            migrationBuilder.DropTable(
                name: "core_JournalLine");

            migrationBuilder.DropTable(
                name: "core_ModuleState");

            migrationBuilder.DropTable(
                name: "core_NumberSequence");

            migrationBuilder.DropTable(
                name: "core_Account");

            migrationBuilder.DropTable(
                name: "core_JournalEntry");
        }
    }
}

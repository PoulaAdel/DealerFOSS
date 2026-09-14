using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerCredits",
                schema: "receivables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceReceivableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RaisedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCredits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditUses",
                schema: "receivables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerCreditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceivableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditUses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditUses_CustomerCredits_CustomerCreditId",
                        column: x => x.CustomerCreditId,
                        principalSchema: "receivables",
                        principalTable: "CustomerCredits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditUses_CustomerCreditId_UsedAt",
                schema: "receivables",
                table: "CreditUses",
                columns: new[] { "CustomerCreditId", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditUses_ReceivableId",
                schema: "receivables",
                table: "CreditUses",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCredits_CustomerId_RaisedAt",
                schema: "receivables",
                table: "CustomerCredits",
                columns: new[] { "CustomerId", "RaisedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCredits_RooftopId_RaisedAt",
                schema: "receivables",
                table: "CustomerCredits",
                columns: new[] { "RooftopId", "RaisedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditUses",
                schema: "receivables");

            migrationBuilder.DropTable(
                name: "CustomerCredits",
                schema: "receivables");
        }
    }
}

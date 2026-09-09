using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class DealTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxedAtArea",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxedAtCountry",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxedAtCounty",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxedAtPostalCode",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DealTaxLines",
                schema: "deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Jurisdiction = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Basis = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Provenance = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PackId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PackVersion = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealTaxLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealTaxLines_Deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "deals",
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealTaxLines_DealId",
                schema: "deals",
                table: "DealTaxLines",
                column: "DealId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealTaxLines",
                schema: "deals");

            migrationBuilder.DropColumn(
                name: "TaxedAtArea",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "TaxedAtCountry",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "TaxedAtCounty",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "TaxedAtPostalCode",
                schema: "deals",
                table: "Deals");
        }
    }
}

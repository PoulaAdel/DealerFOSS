using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealFinancing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FinanceAnnualRate",
                schema: "deals",
                table: "Deals",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinanceDownPayment",
                schema: "deals",
                table: "Deals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinanceLender",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinanceTermMonths",
                schema: "deals",
                table: "Deals",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinanceAnnualRate",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "FinanceDownPayment",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "FinanceLender",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "FinanceTermMonths",
                schema: "deals",
                table: "Deals");
        }
    }
}

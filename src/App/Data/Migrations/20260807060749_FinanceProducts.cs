using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinanceProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "DealProducts",
                schema: "deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: true),
                    TermMiles = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealProducts_Deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "deals",
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinanceProducts",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: true),
                    TermMiles = table.Column<int>(type: "int", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceProducts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealProducts_DealId",
                schema: "deals",
                table: "DealProducts",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceProducts_IsAvailable_Kind",
                schema: "finance",
                table: "FinanceProducts",
                columns: new[] { "IsAvailable", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceProducts_Name",
                schema: "finance",
                table: "FinanceProducts",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealProducts",
                schema: "deals");

            migrationBuilder.DropTable(
                name: "FinanceProducts",
                schema: "finance");
        }
    }
}

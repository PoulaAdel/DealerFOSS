using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDealer360.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "deals");

            migrationBuilder.CreateTable(
                name: "Deals",
                schema: "deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SalespersonUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TradeDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TradeAllowance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TradePayoff = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealCharges",
                schema: "deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealCharges_Deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "deals",
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealHistory",
                schema: "deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AmountAtChange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealHistory_Deals_DealId",
                        column: x => x.DealId,
                        principalSchema: "deals",
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealCharges_DealId",
                schema: "deals",
                table: "DealCharges",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_DealHistory_DealId_OccurredAt",
                schema: "deals",
                table: "DealHistory",
                columns: new[] { "DealId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_CustomerId",
                schema: "deals",
                table: "Deals",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_InventoryUnitId",
                schema: "deals",
                table: "Deals",
                column: "InventoryUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_RooftopId_Status",
                schema: "deals",
                table: "Deals",
                columns: new[] { "RooftopId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_SalespersonUserId",
                schema: "deals",
                table: "Deals",
                column: "SalespersonUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealCharges",
                schema: "deals");

            migrationBuilder.DropTable(
                name: "DealHistory",
                schema: "deals");

            migrationBuilder.DropTable(
                name: "Deals",
                schema: "deals");
        }
    }
}

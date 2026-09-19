using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReconditioningCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconditioningCharges",
                schema: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SourceRepairOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconditioningCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconditioningCharges_InventoryUnits_InventoryUnitId",
                        column: x => x.InventoryUnitId,
                        principalSchema: "vehicles",
                        principalTable: "InventoryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReconditioningCharges_InventoryUnitId_OccurredAt",
                schema: "vehicles",
                table: "ReconditioningCharges",
                columns: new[] { "InventoryUnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconditioningCharges_SourceRepairOrderId",
                schema: "vehicles",
                table: "ReconditioningCharges",
                column: "SourceRepairOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconditioningCharges",
                schema: "vehicles");
        }
    }
}

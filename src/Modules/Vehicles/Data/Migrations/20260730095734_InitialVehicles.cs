using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenDealer360.Vehicles.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vehicles");

            migrationBuilder.CreateTable(
                name: "Vehicles",
                schema: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vin = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VinExceptionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ModelYear = table.Column<int>(type: "int", nullable: false),
                    Make = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Trim = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BodyStyle = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ExteriorColor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryUnits",
                schema: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CostCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    AcquiredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryUnits_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "vehicles",
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryStatusHistory",
                schema: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryStatusHistory_InventoryUnits_InventoryUnitId",
                        column: x => x.InventoryUnitId,
                        principalSchema: "vehicles",
                        principalTable: "InventoryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStatusHistory_InventoryUnitId_OccurredAt",
                schema: "vehicles",
                table: "InventoryStatusHistory",
                columns: new[] { "InventoryUnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryUnits_RooftopId_Status",
                schema: "vehicles",
                table: "InventoryUnits",
                columns: new[] { "RooftopId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryUnits_RooftopId_StockNumber",
                schema: "vehicles",
                table: "InventoryUnits",
                columns: new[] { "RooftopId", "StockNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryUnits_VehicleId",
                schema: "vehicles",
                table: "InventoryUnits",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Make_Model_ModelYear",
                schema: "vehicles",
                table: "Vehicles",
                columns: new[] { "Make", "Model", "ModelYear" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Vin",
                schema: "vehicles",
                table: "Vehicles",
                column: "Vin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryStatusHistory",
                schema: "vehicles");

            migrationBuilder.DropTable(
                name: "InventoryUnits",
                schema: "vehicles");

            migrationBuilder.DropTable(
                name: "Vehicles",
                schema: "vehicles");
        }
    }
}

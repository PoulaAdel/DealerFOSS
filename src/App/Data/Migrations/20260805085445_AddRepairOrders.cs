using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "service");

            migrationBuilder.CreateTable(
                name: "RepairOrders",
                schema: "service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Complaint = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OdometerReading = table.Column<int>(type: "int", nullable: true),
                    AdvisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TechnicianUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoicedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepairOrderHistory",
                schema: "service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepairOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AmountAtChange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairOrderHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairOrderHistory_RepairOrders_RepairOrderId",
                        column: x => x.RepairOrderId,
                        principalSchema: "service",
                        principalTable: "RepairOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLines",
                schema: "service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepairOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Authorization = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthorizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuthorizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorizationNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLines_RepairOrders_RepairOrderId",
                        column: x => x.RepairOrderId,
                        principalSchema: "service",
                        principalTable: "RepairOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrderHistory_RepairOrderId_OccurredAt",
                schema: "service",
                table: "RepairOrderHistory",
                columns: new[] { "RepairOrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrders_CustomerId",
                schema: "service",
                table: "RepairOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrders_RooftopId_Number",
                schema: "service",
                table: "RepairOrders",
                columns: new[] { "RooftopId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrders_RooftopId_Status",
                schema: "service",
                table: "RepairOrders",
                columns: new[] { "RooftopId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrders_TechnicianUserId",
                schema: "service",
                table: "RepairOrders",
                column: "TechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrders_VehicleId",
                schema: "service",
                table: "RepairOrders",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLines_RepairOrderId",
                schema: "service",
                table: "ServiceLines",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLines_RepairOrderId_Authorization",
                schema: "service",
                table: "ServiceLines",
                columns: new[] { "RepairOrderId", "Authorization" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepairOrderHistory",
                schema: "service");

            migrationBuilder.DropTable(
                name: "ServiceLines",
                schema: "service");

            migrationBuilder.DropTable(
                name: "RepairOrders",
                schema: "service");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServiceDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appointments",
                schema: "service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdvisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RepairOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId",
                schema: "service",
                table: "Appointments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RepairOrderId",
                schema: "service",
                table: "Appointments",
                column: "RepairOrderId",
                filter: "[RepairOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RooftopId_ScheduledFor",
                schema: "service",
                table: "Appointments",
                columns: new[] { "RooftopId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RooftopId_Status_ScheduledFor",
                schema: "service",
                table: "Appointments",
                columns: new[] { "RooftopId", "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_VehicleId",
                schema: "service",
                table: "Appointments",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments",
                schema: "service");
        }
    }
}

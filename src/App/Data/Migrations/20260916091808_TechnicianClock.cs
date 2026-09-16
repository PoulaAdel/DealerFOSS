using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class TechnicianClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnicianClockings",
                schema: "service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepairOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StoppedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StoppedBecause = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianClockings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianClockings_RepairOrderId",
                schema: "service",
                table: "TechnicianClockings",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianClockings_RooftopId_StoppedAt",
                schema: "service",
                table: "TechnicianClockings",
                columns: new[] { "RooftopId", "StoppedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianClockings_TechnicianUserId",
                schema: "service",
                table: "TechnicianClockings",
                column: "TechnicianUserId",
                unique: true,
                filter: "[StoppedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicianClockings",
                schema: "service");
        }
    }
}

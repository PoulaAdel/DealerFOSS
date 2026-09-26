using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConnectorSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectorSchedules",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Connector = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contract = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Settings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SuspendedReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastOutcome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorSchedules_Connector_RooftopId_Contract_Version",
                schema: "integration",
                table: "ConnectorSchedules",
                columns: new[] { "Connector", "RooftopId", "Contract", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorSchedules_State_NextRunAt",
                schema: "integration",
                table: "ConnectorSchedules",
                columns: new[] { "State", "NextRunAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectorSchedules",
                schema: "integration");
        }
    }
}

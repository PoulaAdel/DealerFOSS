using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.CreateTable(
                name: "ConnectorCursors",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Connector = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contract = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AdvancedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HeldBecause = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ConsecutiveHolds = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectorRuns",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Connector = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contract = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RecordsApplied = table.Column<int>(type: "int", nullable: false),
                    RecordsQuarantined = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    CoveredFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CoveredTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CursorHeld = table.Column<bool>(type: "bit", nullable: false),
                    CursorHeldReason = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectorRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuarantinedRecords",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Connector = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RooftopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contract = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReasonDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuarantinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantinedRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorCursors_Connector_RooftopId_Contract_Version",
                schema: "integration",
                table: "ConnectorCursors",
                columns: new[] { "Connector", "RooftopId", "Contract", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectorRuns_Connector_RooftopId_Contract_StartedAt",
                schema: "integration",
                table: "ConnectorRuns",
                columns: new[] { "Connector", "RooftopId", "Contract", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedRecords_RooftopId_Contract_ResolvedAt_ExpiresAt",
                schema: "integration",
                table: "QuarantinedRecords",
                columns: new[] { "RooftopId", "Contract", "ResolvedAt", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectorCursors",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "ConnectorRuns",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "QuarantinedRecords",
                schema: "integration");
        }
    }
}

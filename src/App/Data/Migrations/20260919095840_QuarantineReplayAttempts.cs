using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuarantineReplayAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReplayedAt",
                schema: "integration",
                table: "QuarantinedRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplayAttempts",
                schema: "integration",
                table: "QuarantinedRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReplayedAt",
                schema: "integration",
                table: "QuarantinedRecords");

            migrationBuilder.DropColumn(
                name: "ReplayAttempts",
                schema: "integration",
                table: "QuarantinedRecords");
        }
    }
}

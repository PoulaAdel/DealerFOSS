using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnRecordsOnlyToAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OwnRecordsOnly",
                schema: "identity",
                table: "UserAssignments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnRecordsOnly",
                schema: "identity",
                table: "UserAssignments");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RecoveryCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffEnrolments_UserId_ConsumedAt",
                schema: "identity",
                table: "StaffEnrolments");

            // Hand-corrected from the scaffolded defaultValue: "". EF cannot know
            // what an existing row meant, and an empty string matches NEITHER
            // enum name — so every enrolment code outstanding at upgrade time
            // would have become unredeemable, silently, leaving a starter holding
            // a code that simply stopped working. Every row predating this
            // migration was an enrolment code, because recovery codes did not
            // exist until it ran.
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "identity",
                table: "StaffEnrolments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Enrolment");

            migrationBuilder.CreateIndex(
                name: "IX_StaffEnrolments_UserId_Purpose_ConsumedAt",
                schema: "identity",
                table: "StaffEnrolments",
                columns: new[] { "UserId", "Purpose", "ConsumedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffEnrolments_UserId_Purpose_ConsumedAt",
                schema: "identity",
                table: "StaffEnrolments");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "identity",
                table: "StaffEnrolments");

            migrationBuilder.CreateIndex(
                name: "IX_StaffEnrolments_UserId_ConsumedAt",
                schema: "identity",
                table: "StaffEnrolments",
                columns: new[] { "UserId", "ConsumedAt" });
        }
    }
}

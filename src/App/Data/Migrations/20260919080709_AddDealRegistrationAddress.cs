using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealRegistrationAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationAddressLine1",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationAddressLine2",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationArea",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationCity",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationCountry",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationCounty",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationPostalCode",
                schema: "deals",
                table: "Deals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationAddressLine1",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RegistrationAddressLine2",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RegistrationArea",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RegistrationCity",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RegistrationCountry",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RegistrationCounty",
                schema: "deals",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RegistrationPostalCode",
                schema: "deals",
                table: "Deals");
        }
    }
}

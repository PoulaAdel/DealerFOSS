using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServicePayType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayType",
                schema: "service",
                table: "ServiceLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CustomerPay");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayType",
                schema: "service",
                table: "ServiceLines");
        }
    }
}

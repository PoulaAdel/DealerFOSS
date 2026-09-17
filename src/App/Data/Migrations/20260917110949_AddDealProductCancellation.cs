using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealProductCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "deals",
                table: "DealProducts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "deals",
                table: "DealProducts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                schema: "deals",
                table: "DealProducts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                schema: "deals",
                table: "DealProducts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "deals",
                table: "DealProducts");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "deals",
                table: "DealProducts");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                schema: "deals",
                table: "DealProducts");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                schema: "deals",
                table: "DealProducts");
        }
    }
}

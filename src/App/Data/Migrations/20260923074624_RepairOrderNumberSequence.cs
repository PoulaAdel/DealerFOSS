using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairOrderNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberSequence",
                schema: "service",
                table: "RepairOrders",
                type: "int",
                nullable: false,
                computedColumnSql: "CASE WHEN [Number] LIKE 'RO-%' THEN ISNULL(TRY_CAST(SUBSTRING([Number], 4, 26) AS int), 0) ELSE 0 END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrders_RooftopId_NumberSequence",
                schema: "service",
                table: "RepairOrders",
                columns: new[] { "RooftopId", "NumberSequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RepairOrders_RooftopId_NumberSequence",
                schema: "service",
                table: "RepairOrders");

            migrationBuilder.DropColumn(
                name: "NumberSequence",
                schema: "service",
                table: "RepairOrders");
        }
    }
}

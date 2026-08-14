using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Data.Migrations
{
    /// <inheritdoc />
    public partial class HistoryOrderingKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RepairOrderHistory_RepairOrderId_OccurredAt",
                schema: "service",
                table: "RepairOrderHistory");

            migrationBuilder.DropIndex(
                name: "IX_LeadHistory_LeadId_OccurredAt",
                schema: "leads",
                table: "LeadHistory");

            migrationBuilder.DropIndex(
                name: "IX_InventoryStatusHistory_InventoryUnitId_OccurredAt",
                schema: "vehicles",
                table: "InventoryStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_DealHistory_DealId_OccurredAt",
                schema: "deals",
                table: "DealHistory");

            migrationBuilder.DropIndex(
                name: "IX_AccountingPeriodHistory_AccountingPeriodId_OccurredAt",
                schema: "accounting",
                table: "AccountingPeriodHistory");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                schema: "service",
                table: "RepairOrderHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                schema: "leads",
                table: "LeadHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                schema: "vehicles",
                table: "InventoryStatusHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                schema: "deals",
                table: "DealHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                schema: "accounting",
                table: "AccountingPeriodHistory",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrderHistory_RepairOrderId_OccurredAt_Sequence",
                schema: "service",
                table: "RepairOrderHistory",
                columns: new[] { "RepairOrderId", "OccurredAt", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadHistory_LeadId_OccurredAt_Sequence",
                schema: "leads",
                table: "LeadHistory",
                columns: new[] { "LeadId", "OccurredAt", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStatusHistory_InventoryUnitId_OccurredAt_Sequence",
                schema: "vehicles",
                table: "InventoryStatusHistory",
                columns: new[] { "InventoryUnitId", "OccurredAt", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_DealHistory_DealId_OccurredAt_Sequence",
                schema: "deals",
                table: "DealHistory",
                columns: new[] { "DealId", "OccurredAt", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriodHistory_AccountingPeriodId_OccurredAt_Sequence",
                schema: "accounting",
                table: "AccountingPeriodHistory",
                columns: new[] { "AccountingPeriodId", "OccurredAt", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RepairOrderHistory_RepairOrderId_OccurredAt_Sequence",
                schema: "service",
                table: "RepairOrderHistory");

            migrationBuilder.DropIndex(
                name: "IX_LeadHistory_LeadId_OccurredAt_Sequence",
                schema: "leads",
                table: "LeadHistory");

            migrationBuilder.DropIndex(
                name: "IX_InventoryStatusHistory_InventoryUnitId_OccurredAt_Sequence",
                schema: "vehicles",
                table: "InventoryStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_DealHistory_DealId_OccurredAt_Sequence",
                schema: "deals",
                table: "DealHistory");

            migrationBuilder.DropIndex(
                name: "IX_AccountingPeriodHistory_AccountingPeriodId_OccurredAt_Sequence",
                schema: "accounting",
                table: "AccountingPeriodHistory");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "service",
                table: "RepairOrderHistory");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "leads",
                table: "LeadHistory");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "vehicles",
                table: "InventoryStatusHistory");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "deals",
                table: "DealHistory");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "accounting",
                table: "AccountingPeriodHistory");

            migrationBuilder.CreateIndex(
                name: "IX_RepairOrderHistory_RepairOrderId_OccurredAt",
                schema: "service",
                table: "RepairOrderHistory",
                columns: new[] { "RepairOrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadHistory_LeadId_OccurredAt",
                schema: "leads",
                table: "LeadHistory",
                columns: new[] { "LeadId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStatusHistory_InventoryUnitId_OccurredAt",
                schema: "vehicles",
                table: "InventoryStatusHistory",
                columns: new[] { "InventoryUnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DealHistory_DealId_OccurredAt",
                schema: "deals",
                table: "DealHistory",
                columns: new[] { "DealId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriodHistory_AccountingPeriodId_OccurredAt",
                schema: "accounting",
                table: "AccountingPeriodHistory",
                columns: new[] { "AccountingPeriodId", "OccurredAt" });
        }
    }
}

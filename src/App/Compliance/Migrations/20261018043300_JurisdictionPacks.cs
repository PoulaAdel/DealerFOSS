using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Compliance.Migrations
{
    /// <inheritdoc />
    public partial class JurisdictionPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.CreateTable(
                name: "JurisdictionPacks",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Scheme = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LoadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JurisdictionPacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JurisdictionRates",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JurisdictionCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    AdministrativeArea = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    County = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalKey = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JurisdictionRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JurisdictionRates_JurisdictionPacks_PackRowId",
                        column: x => x.PackRowId,
                        principalSchema: "compliance",
                        principalTable: "JurisdictionPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JurisdictionRules",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JurisdictionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JurisdictionRules_JurisdictionPacks_PackRowId",
                        column: x => x.PackRowId,
                        principalSchema: "compliance",
                        principalTable: "JurisdictionPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JurisdictionPacks_PackId_Version",
                schema: "compliance",
                table: "JurisdictionPacks",
                columns: new[] { "PackId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JurisdictionRates_PackRowId",
                schema: "compliance",
                table: "JurisdictionRates",
                column: "PackRowId");

            migrationBuilder.CreateIndex(
                name: "IX_JurisdictionRules_PackRowId_RuleKey",
                schema: "compliance",
                table: "JurisdictionRules",
                columns: new[] { "PackRowId", "RuleKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JurisdictionRates",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "JurisdictionRules",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "JurisdictionPacks",
                schema: "compliance");
        }
    }
}

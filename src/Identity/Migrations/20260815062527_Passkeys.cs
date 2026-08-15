using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerFOSS.Identity.Migrations
{
    /// <inheritdoc />
    public partial class Passkeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasskeyChallenges",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Challenge = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: false),
                    ForRegistration = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasskeyChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Passkeys",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "varbinary(1023)", maxLength: 1023, nullable: false),
                    PublicKeySpki = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignCount = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passkeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasskeyChallenges_ExpiresAt",
                schema: "identity",
                table: "PasskeyChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Passkeys_CredentialId",
                schema: "identity",
                table: "Passkeys",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Passkeys_UserId",
                schema: "identity",
                table: "Passkeys",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasskeyChallenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Passkeys",
                schema: "identity");
        }
    }
}

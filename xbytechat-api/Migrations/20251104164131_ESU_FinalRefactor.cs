using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class ESU_FinalRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacebookAccessToken",
                table: "IntegrationFlags");

            migrationBuilder.DropColumn(
                name: "FacebookTokenExpiresAtUtc",
                table: "IntegrationFlags");

            migrationBuilder.CreateTable(
                name: "EsuTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Scope = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EsuTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EsuTokens_BusinessId_Provider",
                table: "EsuTokens",
                columns: new[] { "BusinessId", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EsuTokens");

            migrationBuilder.AddColumn<string>(
                name: "FacebookAccessToken",
                table: "IntegrationFlags",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FacebookTokenExpiresAtUtc",
                table: "IntegrationFlags",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}

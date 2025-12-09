using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class ContactJourneyStateLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactJourneyStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JourneyText = table.Column<string>(type: "text", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false),
                    LastButtonText = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactJourneyStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactJourneyStates_BusinessId_FlowId_ContactPhone",
                table: "ContactJourneyStates",
                columns: new[] { "BusinessId", "FlowId", "ContactPhone" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactJourneyStates");
        }
    }
}

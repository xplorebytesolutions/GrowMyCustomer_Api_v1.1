using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class HotPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboundMessageJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    TemplateName = table.Column<string>(type: "text", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "text", nullable: true),
                    ResolvedParamsJson = table.Column<string>(type: "text", nullable: false),
                    ResolvedButtonUrlsJson = table.Column<string>(type: "text", nullable: false),
                    HeaderMediaUrl = table.Column<string>(type: "text", nullable: true),
                    MessageBody = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundMessageJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_ProviderMessageId",
                table: "MessageLogs",
                column: "ProviderMessageId",
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundMessageJobs_CampaignId",
                table: "OutboundMessageJobs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_StatusDueCreated",
                table: "OutboundMessageJobs",
                columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Outbox_Biz_IdemKey",
                table: "OutboundMessageJobs",
                columns: new[] { "BusinessId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboundMessageJobs");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_ProviderMessageId",
                table: "MessageLogs");
        }
    }
}

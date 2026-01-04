using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class InboxCursorPagingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_BusinessId_ContactId_CreatedAt",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "ContactId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_BusinessId_ContactId_IsIncoming_CreatedAt",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "ContactId", "IsIncoming", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_msglogs_biz_contact_msgtime_id",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "ContactId", "MessageTime", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_contacts_biz_id",
                table: "Contacts",
                columns: new[] { "BusinessId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessionStates_BusinessId_ContactId",
                table: "ChatSessionStates",
                columns: new[] { "BusinessId", "ContactId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_BusinessId_ContactId_CreatedAt",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_BusinessId_ContactId_IsIncoming_CreatedAt",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "ix_msglogs_biz_contact_msgtime_id",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "ix_contacts_biz_id",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessionStates_BusinessId_ContactId",
                table: "ChatSessionStates");
        }
    }
}

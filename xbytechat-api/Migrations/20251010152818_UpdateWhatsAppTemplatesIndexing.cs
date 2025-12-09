using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWhatsAppTemplatesIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_IsActive",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_LastSyncedAt",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "LastSyncedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_IsActive_Sort",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "IsActive", "UpdatedAt", "LastSyncedAt" },
                descending: new[] { false, false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_Name_Lang",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "Name", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_Status_Lang_Active",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "Status", "LanguageCode" },
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_TemplateId",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "TemplateId" },
                filter: "\"TemplateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WAT_LastSyncedAt",
                table: "WhatsAppTemplates",
                column: "LastSyncedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WAT_UpdatedAt",
                table: "WhatsAppTemplates",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_IsActive",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_LastSyncedAt",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_IsActive_Sort",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_Name_Lang",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_Status_Lang_Active",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_TemplateId",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_LastSyncedAt",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_UpdatedAt",
                table: "WhatsAppTemplates");
        }
    }
}

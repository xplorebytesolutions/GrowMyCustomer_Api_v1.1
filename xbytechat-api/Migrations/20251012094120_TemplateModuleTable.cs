using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class TemplateModuleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_Name_Lang",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_TemplateId",
                table: "WhatsAppTemplates");

            migrationBuilder.CreateTable(
                name: "TemplateDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DefaultLanguage = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateDraftVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    HeaderType = table.Column<string>(type: "text", nullable: false),
                    HeaderText = table.Column<string>(type: "text", nullable: true),
                    HeaderMediaLocalUrl = table.Column<string>(type: "text", nullable: true),
                    FooterText = table.Column<string>(type: "text", nullable: true),
                    ButtonsJson = table.Column<string>(type: "text", nullable: false),
                    ExampleParamsJson = table.Column<string>(type: "text", nullable: false),
                    IsReadyForSubmission = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationErrorsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateDraftVariants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateLibraryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Industry = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    TagsJson = table.Column<string>(type: "text", nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateLibraryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateLibraryVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    HeaderType = table.Column<string>(type: "text", nullable: false),
                    HeaderText = table.Column<string>(type: "text", nullable: true),
                    HeaderDemoUrl = table.Column<string>(type: "text", nullable: true),
                    FooterText = table.Column<string>(type: "text", nullable: true),
                    ButtonsJson = table.Column<string>(type: "text", nullable: false),
                    ExampleParamsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateLibraryVariants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_Name_Lang",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "Name", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_TemplateId",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "TemplateId" },
                unique: true,
                filter: "\"TemplateId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateDrafts");

            migrationBuilder.DropTable(
                name: "TemplateDraftVariants");

            migrationBuilder.DropTable(
                name: "TemplateLibraryItems");

            migrationBuilder.DropTable(
                name: "TemplateLibraryVariants");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_Name_Lang",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WAT_Business_Provider_TemplateId",
                table: "WhatsAppTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_Name_Lang",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "Name", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_WAT_Business_Provider_TemplateId",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider", "TemplateId" },
                filter: "\"TemplateId\" IS NOT NULL");
        }
    }
}

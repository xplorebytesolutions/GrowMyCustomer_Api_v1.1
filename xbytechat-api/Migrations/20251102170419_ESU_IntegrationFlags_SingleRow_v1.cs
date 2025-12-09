using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class ESU_IntegrationFlags_SingleRow_v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationFlags",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacebookEsuCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FacebookAccessToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FacebookTokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationFlags", x => x.BusinessId);
                });

            //migrationBuilder.InsertData(
            //    table: "Permissions",
            //    columns: new[] { "Id", "Code", "CreatedAt", "Description", "Group", "IsActive", "Name" },
            //    values: new object[,]
            //    {
            //        { new Guid("03eabd97-b196-4603-bbdd-1b2cdd595ead"), "template.builder.create.draft", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "TemplateBuilder", true, "Draft Creation" },
            //        { new Guid("14ef7f9d-0975-4ab4-b6f1-7d1af8b594ca"), "template.builder.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "TemplateBuilder", true, "View Template Builder" },
            //        { new Guid("3602f49d-dc10-4faa-9a44-4185a669ea0a"), "template.builder.library.browse", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "TemplateBuilder", true, "View Template Library" },
            //        { new Guid("d272be50-ff26-45cf-bd7a-e9db74813699"), "template.builder.approved.templates.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "TemplateBuilder", true, "View Approved Template" }
            //    });

            migrationBuilder.CreateIndex(
                name: "IX_TLibraryVariants_Language",
                table: "TemplateLibraryVariants",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "UX_TLibraryVariants_Item_Lang",
                table: "TemplateLibraryVariants",
                columns: new[] { "LibraryItemId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TLibrary_Category",
                table: "TemplateLibraryItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TLibrary_Industry_Featured",
                table: "TemplateLibraryItems",
                columns: new[] { "Industry", "IsFeatured" });

            migrationBuilder.CreateIndex(
                name: "UX_TLibrary_Industry_Key",
                table: "TemplateLibraryItems",
                columns: new[] { "Industry", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TDraftVariants_Draft_Ready",
                table: "TemplateDraftVariants",
                columns: new[] { "TemplateDraftId", "IsReadyForSubmission" });

            migrationBuilder.CreateIndex(
                name: "IX_TDraftVariants_Language",
                table: "TemplateDraftVariants",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "UX_TDraftVariants_Draft_Language",
                table: "TemplateDraftVariants",
                columns: new[] { "TemplateDraftId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateDrafts_Biz_UpdatedAt",
                table: "TemplateDrafts",
                columns: new[] { "BusinessId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateDrafts_CreatedAt",
                table: "TemplateDrafts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "UX_TemplateDrafts_Biz_Key",
                table: "TemplateDrafts",
                columns: new[] { "BusinessId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationFlags");

            migrationBuilder.DropIndex(
                name: "IX_TLibraryVariants_Language",
                table: "TemplateLibraryVariants");

            migrationBuilder.DropIndex(
                name: "UX_TLibraryVariants_Item_Lang",
                table: "TemplateLibraryVariants");

            migrationBuilder.DropIndex(
                name: "IX_TLibrary_Category",
                table: "TemplateLibraryItems");

            migrationBuilder.DropIndex(
                name: "IX_TLibrary_Industry_Featured",
                table: "TemplateLibraryItems");

            migrationBuilder.DropIndex(
                name: "UX_TLibrary_Industry_Key",
                table: "TemplateLibraryItems");

            migrationBuilder.DropIndex(
                name: "IX_TDraftVariants_Draft_Ready",
                table: "TemplateDraftVariants");

            migrationBuilder.DropIndex(
                name: "IX_TDraftVariants_Language",
                table: "TemplateDraftVariants");

            migrationBuilder.DropIndex(
                name: "UX_TDraftVariants_Draft_Language",
                table: "TemplateDraftVariants");

            migrationBuilder.DropIndex(
                name: "IX_TemplateDrafts_Biz_UpdatedAt",
                table: "TemplateDrafts");

            migrationBuilder.DropIndex(
                name: "IX_TemplateDrafts_CreatedAt",
                table: "TemplateDrafts");

            migrationBuilder.DropIndex(
                name: "UX_TemplateDrafts_Biz_Key",
                table: "TemplateDrafts");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("03eabd97-b196-4603-bbdd-1b2cdd595ead"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("14ef7f9d-0975-4ab4-b6f1-7d1af8b594ca"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3602f49d-dc10-4faa-9a44-4185a669ea0a"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("d272be50-ff26-45cf-bd7a-e9db74813699"));
        }
    }
}

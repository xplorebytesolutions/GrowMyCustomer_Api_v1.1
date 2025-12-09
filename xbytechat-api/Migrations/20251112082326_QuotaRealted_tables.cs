using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class QuotaRealted_tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Permissions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "BusinessQuotaOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: true),
                    IsUnlimited = table.Column<bool>(type: "boolean", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessQuotaOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessUsageCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Consumed = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessUsageCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanQuotas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    DenialMessage = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanQuotas", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("03eabd97-b196-4603-bbdd-1b2cdd595ead"),
                column: "Code",
                value: "TEMPLATE.BUILDER.CREATE.DRAFT");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("0485154c-dde5-4732-a7aa-a379c77a5b27"),
                column: "Code",
                value: "MESSAGING.SEND.TEMPLATE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("0dedac5b-81c8-44c3-8cfe-76c58e29c6db"),
                column: "Code",
                value: "AUTOMATION_TRIGGER_TEST");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("14ef7f9d-0975-4ab4-b6f1-7d1af8b594ca"),
                column: "Code",
                value: "TEMPLATE.BUILDER.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("205b87c7-b008-4e51-9fea-798c2dc4f9c2"),
                column: "Code",
                value: "ADMIN.WHATSAPPSETTINGS.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("29461562-ef9c-48c0-a606-482ff57b8f95"),
                column: "Code",
                value: "MESSAGING.SEND");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000000"),
                column: "Code",
                value: "DASHBOARD.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "Code",
                value: "CAMPAIGN.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "Code",
                value: "CAMPAIGN.CREATE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "Code",
                value: "CAMPAIGN.DELETE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "Code",
                value: "PRODUCT.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "Code",
                value: "PRODUCT.CREATE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000006"),
                column: "Code",
                value: "PRODUCT.DELETE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000007"),
                column: "Code",
                value: "CONTACTS.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000008"),
                column: "Code",
                value: "TAGS.EDIT");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000009"),
                column: "Code",
                value: "ADMIN.BUSINESS.APPROVE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000010"),
                column: "Code",
                value: "ADMIN.LOGS.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000011"),
                column: "Code",
                value: "ADMIN.PLANS.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000012"),
                column: "Code",
                value: "ADMIN.PLANS.CREATE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000013"),
                column: "Code",
                value: "ADMIN.PLANS.UPDATE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000014"),
                column: "Code",
                value: "ADMIN.PLANS.DELETE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3602f49d-dc10-4faa-9a44-4185a669ea0a"),
                column: "Code",
                value: "TEMPLATE.BUILDER.LIBRARY.BROWSE");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("636b17f2-1c54-4e26-a8cd-dbf561dcb522"),
                column: "Code",
                value: "AUTOMATION.VIEW.TEMPLATE.FLOW_ANALYTICS");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("6e4d3a86-7cf9-4ac2-b8a7-ed10c9f0173d"),
                column: "Code",
                value: "SETTINGS.WHATSAPP.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("74828fc0-e358-4cfc-b924-13719a0d9f50"),
                column: "Code",
                value: "INBOX.MENU");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("74c8034f-d9cb-4a17-8578-a9f765bd845c"),
                column: "Code",
                value: "MESSAGING.REPORT.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("7d7cbceb-4ce7-4835-85cd-59562487298d"),
                column: "Code",
                value: "AUTOMATION.VIEW.TEMPLATEPLUSFREETEXT.FLOW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("821480c6-1464-415e-bba8-066fcb4e7e63"),
                column: "Code",
                value: "AUTOMATION.MENU");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("918a61d0-5ab6-46af-a3d3-41e37b7710f9"),
                column: "Code",
                value: "AUTOMATION.CREATE.TEMPLATE.FLOW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("93c5d5a7-f8dd-460a-8c7b-e3788440ba3a"),
                column: "Code",
                value: "AUTOMATION.CREATE.TEMPLATEPLUSFREETEXT.FLOW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("974af1f9-3caa-4857-a1a7-48462c389332"),
                column: "Code",
                value: "MESSAGING.SEND.TEXT");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("98572fe7-d142-475a-b990-f248641809e2"),
                column: "Code",
                value: "SETTINGS.PROFILE.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("9ae90cfe-3fea-4307-b024-3083c2728148"),
                column: "Code",
                value: "AUTOMATION.VIEW.TEMPLATE.FLOW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("ad36cdb7-5221-448b-a6a6-c35c9f88d021"),
                column: "Code",
                value: "INBOX.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("adfa8490-9705-4a36-a86e-d5bff7ddc220"),
                column: "Code",
                value: "AUTOMATION.VIEW.TEMPLATEPLUSFREETEXT.FLOW_ANALYTICS");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("bbc5202a-eac9-40bb-aa78-176c677dbf5b"),
                column: "Code",
                value: "MESSAGING.WHATSAPPSETTINGS.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("c819f1bd-422d-4609-916c-cc185fe44ab0"),
                column: "Code",
                value: "MESSAGING.STATUS.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("d272be50-ff26-45cf-bd7a-e9db74813699"),
                column: "Code",
                value: "TEMPLATE.BUILDER.APPROVED.TEMPLATES.VIEW");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("eecd0fac-223c-4dba-9fa1-2a6e973d61d1"),
                column: "Code",
                value: "MESSAGING.INBOX.VIEW");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessQuotaOverrides_BusinessId_QuotaKey",
                table: "BusinessQuotaOverrides",
                columns: new[] { "BusinessId", "QuotaKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessUsageCounters_BusinessId_QuotaKey_Period_WindowStar~",
                table: "BusinessUsageCounters",
                columns: new[] { "BusinessId", "QuotaKey", "Period", "WindowStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanQuotas_PlanId_QuotaKey",
                table: "PlanQuotas",
                columns: new[] { "PlanId", "QuotaKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessQuotaOverrides");

            migrationBuilder.DropTable(
                name: "BusinessUsageCounters");

            migrationBuilder.DropTable(
                name: "PlanQuotas");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Code",
                table: "Permissions");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Permissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("03eabd97-b196-4603-bbdd-1b2cdd595ead"),
                column: "Code",
                value: "template.builder.create.draft");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("0485154c-dde5-4732-a7aa-a379c77a5b27"),
                column: "Code",
                value: "messaging.send.template");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("0dedac5b-81c8-44c3-8cfe-76c58e29c6db"),
                column: "Code",
                value: "automation_trigger_test");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("14ef7f9d-0975-4ab4-b6f1-7d1af8b594ca"),
                column: "Code",
                value: "template.builder.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("205b87c7-b008-4e51-9fea-798c2dc4f9c2"),
                column: "Code",
                value: "admin.whatsappsettings.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("29461562-ef9c-48c0-a606-482ff57b8f95"),
                column: "Code",
                value: "messaging.send");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000000"),
                column: "Code",
                value: "dashboard.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "Code",
                value: "campaign.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "Code",
                value: "campaign.create");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "Code",
                value: "campaign.delete");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "Code",
                value: "product.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "Code",
                value: "product.create");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000006"),
                column: "Code",
                value: "product.delete");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000007"),
                column: "Code",
                value: "contacts.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000008"),
                column: "Code",
                value: "tags.edit");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000009"),
                column: "Code",
                value: "admin.business.approve");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000010"),
                column: "Code",
                value: "admin.logs.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000011"),
                column: "Code",
                value: "admin.plans.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000012"),
                column: "Code",
                value: "admin.plans.create");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000013"),
                column: "Code",
                value: "admin.plans.update");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000014"),
                column: "Code",
                value: "admin.plans.delete");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3602f49d-dc10-4faa-9a44-4185a669ea0a"),
                column: "Code",
                value: "template.builder.library.browse");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("636b17f2-1c54-4e26-a8cd-dbf561dcb522"),
                column: "Code",
                value: "automation.View.Template.Flow_analytics");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("6e4d3a86-7cf9-4ac2-b8a7-ed10c9f0173d"),
                column: "Code",
                value: "settings.whatsapp.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("74828fc0-e358-4cfc-b924-13719a0d9f50"),
                column: "Code",
                value: "inbox.menu");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("74c8034f-d9cb-4a17-8578-a9f765bd845c"),
                column: "Code",
                value: "messaging.report.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("7d7cbceb-4ce7-4835-85cd-59562487298d"),
                column: "Code",
                value: "automation.View.TemplatePlusFreetext.Flow");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("821480c6-1464-415e-bba8-066fcb4e7e63"),
                column: "Code",
                value: "automation.menu");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("918a61d0-5ab6-46af-a3d3-41e37b7710f9"),
                column: "Code",
                value: "automation.Create.Template.Flow");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("93c5d5a7-f8dd-460a-8c7b-e3788440ba3a"),
                column: "Code",
                value: "automation.Create.TemplatePlusFreetext.Flow");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("974af1f9-3caa-4857-a1a7-48462c389332"),
                column: "Code",
                value: "messaging.send.text");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("98572fe7-d142-475a-b990-f248641809e2"),
                column: "Code",
                value: "settings.profile.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("9ae90cfe-3fea-4307-b024-3083c2728148"),
                column: "Code",
                value: "automation.View.Template.Flow");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("ad36cdb7-5221-448b-a6a6-c35c9f88d021"),
                column: "Code",
                value: "inbox.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("adfa8490-9705-4a36-a86e-d5bff7ddc220"),
                column: "Code",
                value: "automation.View.TemplatePlusFreeText.Flow_analytics");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("bbc5202a-eac9-40bb-aa78-176c677dbf5b"),
                column: "Code",
                value: "messaging.whatsappsettings.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("c819f1bd-422d-4609-916c-cc185fe44ab0"),
                column: "Code",
                value: "messaging.status.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("d272be50-ff26-45cf-bd7a-e9db74813699"),
                column: "Code",
                value: "template.builder.approved.templates.view");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("eecd0fac-223c-4dba-9fa1-2a6e973d61d1"),
                column: "Code",
                value: "messaging.inbox.view");
        }
    }
}

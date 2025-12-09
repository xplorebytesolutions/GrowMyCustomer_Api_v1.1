using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddBodyHeaderbuttonToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Name",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Name_Language_Provider",
                table: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Provider",
                table: "WhatsAppTemplates");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "RawJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ButtonsJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "BodyParamIndicesJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyText",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ButtonParamTemplatesJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeaderParamIndicesJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParameterFormat",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceholderOccurrencesJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyParamIndicesJson",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "BodyText",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "ButtonParamTemplatesJson",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "HeaderParamIndicesJson",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "ParameterFormat",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "PlaceholderOccurrencesJson",
                table: "WhatsAppTemplates");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WhatsAppTemplates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RawJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "WhatsAppTemplates",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WhatsAppTemplates",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "WhatsAppTemplates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                table: "WhatsAppTemplates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "WhatsAppTemplates",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ButtonsJson",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Name",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Name_Language_Provider",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Name", "Language", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Provider",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider" });
        }
    }
}

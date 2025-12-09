using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWhatsAppTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyParamIndicesJson",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "BodyText",
                table: "WhatsAppTemplates");

            migrationBuilder.RenameColumn(
                name: "PlaceholderOccurrencesJson",
                table: "WhatsAppTemplates",
                newName: "UrlButtons");

            migrationBuilder.RenameColumn(
                name: "PlaceholderCount",
                table: "WhatsAppTemplates",
                newName: "TotalTextParamCount");

            migrationBuilder.RenameColumn(
                name: "Language",
                table: "WhatsAppTemplates",
                newName: "TemplateId");

            migrationBuilder.RenameColumn(
                name: "HeaderParamIndicesJson",
                table: "WhatsAppTemplates",
                newName: "SubCategory");

            migrationBuilder.RenameColumn(
                name: "ExternalId",
                table: "WhatsAppTemplates",
                newName: "PlaceholderMap");

            migrationBuilder.RenameColumn(
                name: "ButtonsJson",
                table: "WhatsAppTemplates",
                newName: "NamedParamKeys");

            migrationBuilder.RenameColumn(
                name: "ButtonParamTemplatesJson",
                table: "WhatsAppTemplates",
                newName: "BodyPreview");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WhatsAppTemplates",
                type: "character varying(24)",
                maxLength: 24,
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
                name: "ParameterFormat",
                table: "WhatsAppTemplates",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WhatsAppTemplates",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "HeaderKind",
                table: "WhatsAppTemplates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyVarCount",
                table: "WhatsAppTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasPhoneButton",
                table: "WhatsAppTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HeaderTextVarCount",
                table: "WhatsAppTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "WhatsAppTemplates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QuickReplyCount",
                table: "WhatsAppTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresMediaHeader",
                table: "WhatsAppTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WhatsAppTemplates",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyVarCount",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "HasPhoneButton",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "HeaderTextVarCount",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "QuickReplyCount",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "RequiresMediaHeader",
                table: "WhatsAppTemplates");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WhatsAppTemplates");

            migrationBuilder.RenameColumn(
                name: "UrlButtons",
                table: "WhatsAppTemplates",
                newName: "PlaceholderOccurrencesJson");

            migrationBuilder.RenameColumn(
                name: "TotalTextParamCount",
                table: "WhatsAppTemplates",
                newName: "PlaceholderCount");

            migrationBuilder.RenameColumn(
                name: "TemplateId",
                table: "WhatsAppTemplates",
                newName: "Language");

            migrationBuilder.RenameColumn(
                name: "SubCategory",
                table: "WhatsAppTemplates",
                newName: "HeaderParamIndicesJson");

            migrationBuilder.RenameColumn(
                name: "PlaceholderMap",
                table: "WhatsAppTemplates",
                newName: "ExternalId");

            migrationBuilder.RenameColumn(
                name: "NamedParamKeys",
                table: "WhatsAppTemplates",
                newName: "ButtonsJson");

            migrationBuilder.RenameColumn(
                name: "BodyPreview",
                table: "WhatsAppTemplates",
                newName: "ButtonParamTemplatesJson");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24);

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
                name: "ParameterFormat",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "HeaderKind",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

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
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddCoulmnHeaderText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "HeaderKind",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeaderText",
                table: "WhatsAppTemplates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeaderText",
                table: "WhatsAppTemplates");

            migrationBuilder.AlterColumn<string>(
                name: "HeaderKind",
                table: "WhatsAppTemplates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}

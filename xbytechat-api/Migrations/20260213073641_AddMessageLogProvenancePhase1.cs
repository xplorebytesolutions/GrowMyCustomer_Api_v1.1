using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageLogProvenancePhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageKind",
                table: "MessageLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateLanguage",
                table: "MessageLogs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "MessageLogs",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateSnapshotJson",
                table: "MessageLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageKind",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "TemplateLanguage",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "TemplateSnapshotJson",
                table: "MessageLogs");
        }
    }
}

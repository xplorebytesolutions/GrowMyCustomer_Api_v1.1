using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    public partial class AddMessageLogMediaFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "MessageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaId",
                table: "MessageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "MessageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "MessageLogs",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "MessageLogs");
        }
    }
}


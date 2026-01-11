using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddCoulmnsForVideoAudioLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationAddress",
                table: "MessageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLatitude",
                table: "MessageLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLongitude",
                table: "MessageLogs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "MessageLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationAddress",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "MessageLogs");
        }
    }
}

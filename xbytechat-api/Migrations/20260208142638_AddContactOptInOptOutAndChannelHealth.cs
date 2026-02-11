using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddContactOptInOptOutAndChannelHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChannelStatus",
                table: "Contacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChannelStatusUpdatedAt",
                table: "Contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptOutReason",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OptStatus",
                table: "Contacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OptStatusUpdatedAt",
                table: "Contacts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelStatus",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "ChannelStatusUpdatedAt",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "OptOutReason",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "OptStatus",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "OptStatusUpdatedAt",
                table: "Contacts");
        }
    }
}

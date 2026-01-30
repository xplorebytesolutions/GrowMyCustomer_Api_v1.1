using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppConnectionFieldsFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerifiedName",
                table: "WhatsAppPhoneNumbers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityRating",
                table: "WhatsAppPhoneNumbers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "WhatsAppPhoneNumbers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessagingLimitTier",
                table: "WhatsAppPhoneNumbers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConnectionDataUpdatedAt",
                table: "WhatsAppPhoneNumbers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerifiedName",
                table: "WhatsAppPhoneNumbers");

            migrationBuilder.DropColumn(
                name: "QualityRating",
                table: "WhatsAppPhoneNumbers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WhatsAppPhoneNumbers");

            migrationBuilder.DropColumn(
                name: "MessagingLimitTier",
                table: "WhatsAppPhoneNumbers");

            migrationBuilder.DropColumn(
                name: "ConnectionDataUpdatedAt",
                table: "WhatsAppPhoneNumbers");
        }
    }
}

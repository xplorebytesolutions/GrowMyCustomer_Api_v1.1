using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class freshDeplyafterWebhookConfigs077 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.CreateTable(
            //    name: "CustomerWebhookConfigs",
            //    columns: table => new
            //    {
            //        Id = table.Column<Guid>(type: "uuid", nullable: false),
            //        BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
            //        Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
            //        BearerToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
            //        IsActive = table.Column<bool>(type: "boolean", nullable: false),
            //        CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            //        UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_CustomerWebhookConfigs", x => x.Id);
            //    });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerWebhookConfigs");
        }
    }
}

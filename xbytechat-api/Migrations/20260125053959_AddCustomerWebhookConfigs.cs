using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerWebhookConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("5f9f5de1-a0b2-48ba-b03d-77b27345613f"),
                columns: new[] { "Code", "Name" },
                values: new object[] { "10001", "Basic" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("5f9f5de1-a0b2-48ba-b03d-77b27345613f"),
                columns: new[] { "Code", "Name" },
                values: new object[] { "SYSTEM_DEFAULT", "System Default" });
        }
    }
}

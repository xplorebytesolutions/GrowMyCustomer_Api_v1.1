using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanIsInternalCoulmn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("5f9f5de1-a0b2-48ba-b03d-77b27345613f"),
                columns: new[] { "Code", "IsInternal", "Name" },
                values: new object[] { "SYSTEM_DEFAULT", true, "System Default" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "Plans");

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("5f9f5de1-a0b2-48ba-b03d-77b27345613f"),
                columns: new[] { "Code", "Name" },
                values: new object[] { "basic", "Basic" });
        }
    }
}

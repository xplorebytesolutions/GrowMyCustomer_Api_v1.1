using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class Campaign_Unique_BizId_Name : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Campaigns_BusinessId",
                table: "Campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_BusinessId_Name",
                table: "Campaigns",
                columns: new[] { "BusinessId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Campaigns_BusinessId_Name",
                table: "Campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_BusinessId",
                table: "Campaigns",
                column: "BusinessId");
        }
    }
}

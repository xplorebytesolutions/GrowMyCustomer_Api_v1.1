using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAtHostingerSever : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_CampaignSendLogs_BizMessage",
                table: "CampaignSendLogs",
                newName: "IX_CampaignSendLogs_Business_MessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_CampaignSendLogs_Business_MessageId",
                table: "CampaignSendLogs",
                newName: "IX_CampaignSendLogs_BizMessage");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateCampaignIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_MessageLogs_Campaign"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_CampaignSendLogs_Campaign"";", suppressTransaction: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_MessageLogs_Campaign"" ON ""MessageLogs"" (""CampaignId"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CampaignSendLogs_Campaign"" ON ""CampaignSendLogs"" (""CampaignId"");");
        }
    }
}

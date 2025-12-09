using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class Entitlements_KeyNormalization_new : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""PlanQuotas"" SET ""QuotaKey"" = UPPER(""QuotaKey"") WHERE ""QuotaKey"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""BusinessQuotaOverrides"" SET ""QuotaKey"" = UPPER(""QuotaKey"") WHERE ""QuotaKey"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""BusinessUsageCounters"" SET ""QuotaKey"" = UPPER(""QuotaKey"") WHERE ""QuotaKey"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE ""Permissions"" SET ""Code"" = UPPER(""Code"") WHERE ""Code"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260125194500_CreateCustomerWebhookConfigsTable")]
    public partial class CreateCustomerWebhookConfigsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some environments ended up without the CustomerWebhookConfigs table (see migration 20251001121508... where CreateTable was commented out).
            // This table is used by CTAJourney/Customer API webhook features and must exist for click processing to work reliably.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""CustomerWebhookConfigs"" (
                    ""Id"" uuid NOT NULL,
                    ""BusinessId"" uuid NOT NULL,
                    ""Url"" character varying(1024) NOT NULL,
                    ""BearerToken"" character varying(2048) NULL,
                    ""IsActive"" boolean NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone NULL,
                    CONSTRAINT ""PK_CustomerWebhookConfigs"" PRIMARY KEY (""Id"")
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""CustomerWebhookConfigs"";");
        }
    }
}

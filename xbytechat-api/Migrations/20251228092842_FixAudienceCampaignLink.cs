using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class FixAudienceCampaignLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Audiences_Campaigns_CampaignId",
                table: "Audiences");

            //migrationBuilder.DropIndex(
            //    name: "IX_PlanPermissions_PlanId",
            //    table: "PlanPermissions");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_PlanPermissions_PlanId"";");

            //migrationBuilder.DropIndex(
            //    name: "IX_Campaigns_BusinessId",
            //    table: "Campaigns");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Campaigns_BusinessId"";");

            migrationBuilder.DropIndex(
                name: "IX_Audiences_BusinessId_CampaignId",
                table: "Audiences");

            migrationBuilder.DropIndex(
                name: "IX_Audiences_CampaignId",
                table: "Audiences");

            //migrationBuilder.DropIndex(
            //    name: "ux_audmember_audience_phone",
            //    table: "AudienceMembers");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""ux_audmember_audience_phone"";");

            migrationBuilder.DropColumn(
                name: "AudienceId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Audiences");

            migrationBuilder.CreateTable(
                name: "CampaignAudienceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CsvBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeactivatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAudienceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAudienceAttachments_Audiences_AudienceId",
                        column: x => x.AudienceId,
                        principalTable: "Audiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignAudienceAttachments_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAudienceAttachments_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAudienceAttachments_CsvBatches_CsvBatchId",
                        column: x => x.CsvBatchId,
                        principalTable: "CsvBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Campaigns_Biz_Name",
                table: "Campaigns",
                columns: new[] { "BusinessId", "Name" },
                unique: true);

            //migrationBuilder.CreateIndex(
            //    name: "UX_Audiences_Biz_Name_Active",
            //    table: "Audiences",
            //    columns: new[] { "BusinessId", "Name" },
            //    unique: true,
            //    filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE c.relkind = 'i'
      AND c.relname = 'UX_AudienceMembers_Audience_PhoneE164'
      AND n.nspname = 'public'
  ) THEN
    CREATE UNIQUE INDEX ""UX_AudienceMembers_Audience_PhoneE164""
      ON ""AudienceMembers"" (""AudienceId"", ""PhoneE164"")
      WHERE ""PhoneE164"" IS NOT NULL AND ""PhoneE164"" <> '';
  END IF;
END $$;
");


            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudienceAttachments_AudienceId",
                table: "CampaignAudienceAttachments",
                column: "AudienceId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudienceAttachments_BusinessId_AudienceId",
                table: "CampaignAudienceAttachments",
                columns: new[] { "BusinessId", "AudienceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudienceAttachments_BusinessId_CampaignId",
                table: "CampaignAudienceAttachments",
                columns: new[] { "BusinessId", "CampaignId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudienceAttachments_BusinessId_CsvBatchId",
                table: "CampaignAudienceAttachments",
                columns: new[] { "BusinessId", "CsvBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudienceAttachments_CampaignId",
                table: "CampaignAudienceAttachments",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudienceAttachments_CsvBatchId",
                table: "CampaignAudienceAttachments",
                column: "CsvBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignAudienceAttachments");

            migrationBuilder.DropIndex(
                name: "UX_Campaigns_Biz_Name",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "UX_Audiences_Biz_Name_Active",
                table: "Audiences");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_AudienceMembers_Audience_PhoneE164"";");


            migrationBuilder.AddColumn<Guid>(
                name: "AudienceId",
                table: "Campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "Audiences",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanPermissions_PlanId",
                table: "PlanPermissions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_BusinessId",
                table: "Campaigns",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_BusinessId_CampaignId",
                table: "Audiences",
                columns: new[] { "BusinessId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_CampaignId",
                table: "Audiences",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "ux_audmember_audience_phone",
                table: "AudienceMembers",
                columns: new[] { "AudienceId", "PhoneE164" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Audiences_Campaigns_CampaignId",
                table: "Audiences",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

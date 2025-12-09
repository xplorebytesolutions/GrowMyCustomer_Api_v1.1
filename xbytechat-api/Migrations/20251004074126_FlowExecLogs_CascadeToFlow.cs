using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    public partial class FlowExecLogs_CascadeToFlow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure helpful index exists
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_FlowExecutionLogs_FlowId""
ON ""FlowExecutionLogs"" (""FlowId"");
");

            // Drop any pre-existing FK (name may or may not exist) then re-add with CASCADE
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.table_constraints
    WHERE constraint_type = 'FOREIGN KEY'
      AND table_name = 'FlowExecutionLogs'
      AND constraint_name = 'FK_FlowExecutionLogs_CTAFlowConfigs_FlowId'
  ) THEN
    ALTER TABLE ""FlowExecutionLogs""
      DROP CONSTRAINT ""FK_FlowExecutionLogs_CTAFlowConfigs_FlowId"";
  END IF;
END$$;
");

            migrationBuilder.Sql(@"
ALTER TABLE ""FlowExecutionLogs""
ADD CONSTRAINT ""FK_FlowExecutionLogs_CTAFlowConfigs_FlowId""
FOREIGN KEY (""FlowId"") REFERENCES ""CTAFlowConfigs""(""Id"")
ON DELETE CASCADE;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the FK if present
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM information_schema.table_constraints
    WHERE constraint_type = 'FOREIGN KEY'
      AND table_name = 'FlowExecutionLogs'
      AND constraint_name = 'FK_FlowExecutionLogs_CTAFlowConfigs_FlowId'
  ) THEN
    ALTER TABLE ""FlowExecutionLogs""
      DROP CONSTRAINT ""FK_FlowExecutionLogs_CTAFlowConfigs_FlowId"";
  END IF;
END$$;
");

            // Optional: drop the index if you want full rollback
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_FlowExecutionLogs_FlowId"";");
        }
    }
}


//using System;
//using Microsoft.EntityFrameworkCore.Migrations;

//#nullable disable

//namespace xbytechat.api.Migrations
//{
//    /// <inheritdoc />
//    public partial class FlowExecLogs_CascadeToFlow : Migration
//    {
//        /// <inheritdoc />
//        protected override void Up(MigrationBuilder migrationBuilder)
//        {
//            migrationBuilder.DropForeignKey(
//                name: "FK_Audiences_Campaigns_CampaignId",
//                table: "Audiences");

//            migrationBuilder.DropForeignKey(
//                name: "FK_MessageLogs_Campaigns_CampaignId",
//                table: "MessageLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_MessageStatusLogs_Campaigns_CampaignId",
//                table: "MessageStatusLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId",
//                table: "TrackingLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_Campaigns_CampaignId",
//                table: "TrackingLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_MessageLogs_MessageLogId",
//                table: "TrackingLogs");

//            migrationBuilder.DropIndex(
//                name: "ix_ctaflowconfigs_biz_active_name",
//                table: "CTAFlowConfigs");

//            migrationBuilder.DropIndex(
//                name: "IX_Campaigns_BusinessId_Name",
//                table: "Campaigns");

//            migrationBuilder.RenameIndex(
//                name: "IX_WhatsAppSettings_BizProviderActive",
//                table: "WhatsAppSettings",
//                newName: "IX_WhatsAppSettings_Business_Provider_IsActive");

//            migrationBuilder.RenameIndex(
//                name: "IX_WhatsAppPhoneNumbers_BizProviderPhone",
//                table: "WhatsAppPhoneNumbers",
//                newName: "UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId");

//            migrationBuilder.RenameIndex(
//                name: "IX_TrackingLogs_MessageLogId",
//                table: "TrackingLogs",
//                newName: "IX_TrackingLogs_MessageLog");

//            migrationBuilder.RenameIndex(
//                name: "IX_TrackingLogs_CampaignSendLogId",
//                table: "TrackingLogs",
//                newName: "IX_TrackingLogs_SendLog");

//            migrationBuilder.RenameIndex(
//                name: "IX_TrackingLogs_CampaignId",
//                table: "TrackingLogs",
//                newName: "IX_TrackingLogs_Campaign");

//            migrationBuilder.RenameIndex(
//                name: "IX_MessageLogs_CampaignId",
//                table: "MessageLogs",
//                newName: "IX_MessageLogs_Campaign");

//            migrationBuilder.RenameIndex(
//                name: "IX_CampaignSendLogs_CampaignId",
//                table: "CampaignSendLogs",
//                newName: "IX_CampaignSendLogs_Campaign");

//            migrationBuilder.AddColumn<Guid>(
//                name: "CampaignId1",
//                table: "TrackingLogs",
//                type: "uuid",
//                nullable: true);

//            migrationBuilder.AddColumn<Guid>(
//                name: "CampaignSendLogId1",
//                table: "TrackingLogs",
//                type: "uuid",
//                nullable: true);

//            migrationBuilder.AddColumn<Guid>(
//                name: "MessageLogId1",
//                table: "TrackingLogs",
//                type: "uuid",
//                nullable: true);

//            migrationBuilder.AddColumn<Guid>(
//                name: "CampaignId1",
//                table: "MessageStatusLogs",
//                type: "uuid",
//                nullable: true);

//            migrationBuilder.CreateIndex(
//                name: "IX_TrackingLogs_CampaignId1",
//                table: "TrackingLogs",
//                column: "CampaignId1");

//            migrationBuilder.CreateIndex(
//                name: "IX_TrackingLogs_CampaignSendLogId1",
//                table: "TrackingLogs",
//                column: "CampaignSendLogId1");

//            migrationBuilder.CreateIndex(
//                name: "IX_TrackingLogs_MessageLogId1",
//                table: "TrackingLogs",
//                column: "MessageLogId1");

//            migrationBuilder.CreateIndex(
//                name: "IX_MessageStatusLogs_CampaignId1",
//                table: "MessageStatusLogs",
//                column: "CampaignId1");

//            migrationBuilder.CreateIndex(
//                name: "IX_Campaigns_BusinessId",
//                table: "Campaigns",
//                column: "BusinessId");

//            migrationBuilder.AddForeignKey(
//                name: "FK_Audiences_Campaigns_CampaignId",
//                table: "Audiences",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.Cascade);

//            migrationBuilder.AddForeignKey(
//                name: "FK_MessageLogs_Campaigns_CampaignId",
//                table: "MessageLogs",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.Cascade);

//            migrationBuilder.AddForeignKey(
//                name: "FK_MessageStatusLogs_Campaigns_CampaignId",
//                table: "MessageStatusLogs",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.Cascade);

//            migrationBuilder.AddForeignKey(
//                name: "FK_MessageStatusLogs_Campaigns_CampaignId1",
//                table: "MessageStatusLogs",
//                column: "CampaignId1",
//                principalTable: "Campaigns",
//                principalColumn: "Id");

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId",
//                table: "TrackingLogs",
//                column: "CampaignSendLogId",
//                principalTable: "CampaignSendLogs",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.Restrict);

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId1",
//                table: "TrackingLogs",
//                column: "CampaignSendLogId1",
//                principalTable: "CampaignSendLogs",
//                principalColumn: "Id");

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_Campaigns_CampaignId",
//                table: "TrackingLogs",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.Restrict);

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_Campaigns_CampaignId1",
//                table: "TrackingLogs",
//                column: "CampaignId1",
//                principalTable: "Campaigns",
//                principalColumn: "Id");

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_MessageLogs_MessageLogId",
//                table: "TrackingLogs",
//                column: "MessageLogId",
//                principalTable: "MessageLogs",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.Restrict);

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_MessageLogs_MessageLogId1",
//                table: "TrackingLogs",
//                column: "MessageLogId1",
//                principalTable: "MessageLogs",
//                principalColumn: "Id");
//        }

//        /// <inheritdoc />
//        protected override void Down(MigrationBuilder migrationBuilder)
//        {
//            migrationBuilder.DropForeignKey(
//                name: "FK_Audiences_Campaigns_CampaignId",
//                table: "Audiences");

//            migrationBuilder.DropForeignKey(
//                name: "FK_MessageLogs_Campaigns_CampaignId",
//                table: "MessageLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_MessageStatusLogs_Campaigns_CampaignId",
//                table: "MessageStatusLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_MessageStatusLogs_Campaigns_CampaignId1",
//                table: "MessageStatusLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId",
//                table: "TrackingLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_Campaigns_CampaignId",
//                table: "TrackingLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_Campaigns_CampaignId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_MessageLogs_MessageLogId",
//                table: "TrackingLogs");

//            migrationBuilder.DropForeignKey(
//                name: "FK_TrackingLogs_MessageLogs_MessageLogId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropIndex(
//                name: "IX_TrackingLogs_CampaignId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropIndex(
//                name: "IX_TrackingLogs_CampaignSendLogId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropIndex(
//                name: "IX_TrackingLogs_MessageLogId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropIndex(
//                name: "IX_MessageStatusLogs_CampaignId1",
//                table: "MessageStatusLogs");

//            migrationBuilder.DropIndex(
//                name: "IX_Campaigns_BusinessId",
//                table: "Campaigns");

//            migrationBuilder.DropColumn(
//                name: "CampaignId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropColumn(
//                name: "CampaignSendLogId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropColumn(
//                name: "MessageLogId1",
//                table: "TrackingLogs");

//            migrationBuilder.DropColumn(
//                name: "CampaignId1",
//                table: "MessageStatusLogs");

//            migrationBuilder.RenameIndex(
//                name: "IX_WhatsAppSettings_Business_Provider_IsActive",
//                table: "WhatsAppSettings",
//                newName: "IX_WhatsAppSettings_BizProviderActive");

//            migrationBuilder.RenameIndex(
//                name: "UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId",
//                table: "WhatsAppPhoneNumbers",
//                newName: "IX_WhatsAppPhoneNumbers_BizProviderPhone");

//            migrationBuilder.RenameIndex(
//                name: "IX_TrackingLogs_SendLog",
//                table: "TrackingLogs",
//                newName: "IX_TrackingLogs_CampaignSendLogId");

//            migrationBuilder.RenameIndex(
//                name: "IX_TrackingLogs_MessageLog",
//                table: "TrackingLogs",
//                newName: "IX_TrackingLogs_MessageLogId");

//            migrationBuilder.RenameIndex(
//                name: "IX_TrackingLogs_Campaign",
//                table: "TrackingLogs",
//                newName: "IX_TrackingLogs_CampaignId");

//            migrationBuilder.RenameIndex(
//                name: "IX_MessageLogs_Campaign",
//                table: "MessageLogs",
//                newName: "IX_MessageLogs_CampaignId");

//            migrationBuilder.RenameIndex(
//                name: "IX_CampaignSendLogs_Campaign",
//                table: "CampaignSendLogs",
//                newName: "IX_CampaignSendLogs_CampaignId");

//            migrationBuilder.CreateIndex(
//                name: "ix_ctaflowconfigs_biz_active_name",
//                table: "CTAFlowConfigs",
//                columns: new[] { "BusinessId", "IsActive", "FlowName" });

//            migrationBuilder.CreateIndex(
//                name: "IX_Campaigns_BusinessId_Name",
//                table: "Campaigns",
//                columns: new[] { "BusinessId", "Name" },
//                unique: true);

//            migrationBuilder.AddForeignKey(
//                name: "FK_Audiences_Campaigns_CampaignId",
//                table: "Audiences",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.SetNull);

//            migrationBuilder.AddForeignKey(
//                name: "FK_MessageLogs_Campaigns_CampaignId",
//                table: "MessageLogs",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id",
//                onDelete: ReferentialAction.Restrict);

//            migrationBuilder.AddForeignKey(
//                name: "FK_MessageStatusLogs_Campaigns_CampaignId",
//                table: "MessageStatusLogs",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id");

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId",
//                table: "TrackingLogs",
//                column: "CampaignSendLogId",
//                principalTable: "CampaignSendLogs",
//                principalColumn: "Id");

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_Campaigns_CampaignId",
//                table: "TrackingLogs",
//                column: "CampaignId",
//                principalTable: "Campaigns",
//                principalColumn: "Id");

//            migrationBuilder.AddForeignKey(
//                name: "FK_TrackingLogs_MessageLogs_MessageLogId",
//                table: "TrackingLogs",
//                column: "MessageLogId",
//                principalTable: "MessageLogs",
//                principalColumn: "Id");
//        }
//    }
//}

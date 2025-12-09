using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20251004153000_CampaignCascadeAndIndexes")]
    public partial class CampaignCascadeAndIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Make ContactId nullable on CampaignSendLogs (matches Fluent + service logic)
            migrationBuilder.AlterColumn<Guid>(
                name: "ContactId",
                table: "CampaignSendLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // 2) Drop existing FKs we’re changing (idempotent guards)
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_type='FOREIGN KEY'
          AND table_name='Audiences'
          AND constraint_name='FK_Audiences_Campaigns_CampaignId'
    ) THEN
        ALTER TABLE ""Audiences"" DROP CONSTRAINT ""FK_Audiences_Campaigns_CampaignId"";
    END IF;
END$$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_type='FOREIGN KEY'
          AND table_name='AudienceMembers'
          AND constraint_name='FK_AudienceMembers_Audiences_AudienceId'
    ) THEN
        ALTER TABLE ""AudienceMembers"" DROP CONSTRAINT ""FK_AudienceMembers_Audiences_AudienceId"";
    END IF;
END$$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_type='FOREIGN KEY'
          AND table_name='MessageLogs'
          AND constraint_name='FK_MessageLogs_Campaigns_CampaignId'
    ) THEN
        ALTER TABLE ""MessageLogs"" DROP CONSTRAINT ""FK_MessageLogs_Campaigns_CampaignId"";
    END IF;
END$$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_type='FOREIGN KEY'
          AND table_name='MessageStatusLogs'
          AND constraint_name='FK_MessageStatusLogs_Campaigns_CampaignId'
    ) THEN
        ALTER TABLE ""MessageStatusLogs"" DROP CONSTRAINT ""FK_MessageStatusLogs_Campaigns_CampaignId"";
    END IF;
END$$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_type='FOREIGN KEY'
          AND table_name='TrackingLogs'
          AND constraint_name='FK_TrackingLogs_Campaigns_CampaignId'
    ) THEN
        ALTER TABLE ""TrackingLogs"" DROP CONSTRAINT ""FK_TrackingLogs_Campaigns_CampaignId"";
    END IF;
END$$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_type='FOREIGN KEY'
          AND table_name='TrackingLogs'
          AND constraint_name='FK_TrackingLogs_MessageLogs_MessageLogId'
    ) THEN
        ALTER TABLE ""TrackingLogs"" DROP CONSTRAINT ""FK_TrackingLogs_MessageLogs_MessageLogId"";
    END IF;
END$$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_type='FOREIGN KEY'
          AND table_name='TrackingLogs'
          AND constraint_name='FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId'
    ) THEN
        ALTER TABLE ""TrackingLogs"" DROP CONSTRAINT ""FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId"";
    END IF;
END$$;");

            // 3) Re-add FKs with desired behaviors
            migrationBuilder.AddForeignKey(
                name: "FK_Audiences_Campaigns_CampaignId",
                table: "Audiences",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AudienceMembers_Audiences_AudienceId",
                table: "AudienceMembers",
                column: "AudienceId",
                principalTable: "Audiences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageLogs_Campaigns_CampaignId",
                table: "MessageLogs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageStatusLogs_Campaigns_CampaignId",
                table: "MessageStatusLogs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackingLogs_Campaigns_CampaignId",
                table: "TrackingLogs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackingLogs_MessageLogs_MessageLogId",
                table: "TrackingLogs",
                column: "MessageLogId",
                principalTable: "MessageLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId",
                table: "TrackingLogs",
                column: "CampaignSendLogId",
                principalTable: "CampaignSendLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4) Index hygiene
            migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_CampaignSendLogs_Campaign"" ON ""CampaignSendLogs"" (""CampaignId"");
            CREATE INDEX IF NOT EXISTS ""IX_MessageLogs_Campaign""      ON ""MessageLogs""      (""CampaignId"");
            CREATE INDEX IF NOT EXISTS ""IX_TrackingLogs_Campaign""     ON ""TrackingLogs""     (""CampaignId"");
            CREATE INDEX IF NOT EXISTS ""IX_TrackingLogs_MessageLog""   ON ""TrackingLogs""     (""MessageLogId"");
            CREATE INDEX IF NOT EXISTS ""IX_TrackingLogs_SendLog""      ON ""TrackingLogs""     (""CampaignSendLogId"");
            ");

            // 5) CTAFlowConfig: drop near-duplicate non-unique index if present
            migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_ctaflowconfigs_biz_active_name') THEN
                    DROP INDEX ""ix_ctaflowconfigs_biz_active_name"";
                END IF;
            END$$;");

            // 6) WhatsAppPhoneNumbers: ensure unique triple index
            migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_WhatsAppPhoneNumbers_BizProviderPhone') THEN
                    DROP INDEX ""IX_WhatsAppPhoneNumbers_BizProviderPhone"";
                END IF;
            END$$;");

                        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId""
            ON ""WhatsAppPhoneNumbers"" (""BusinessId"", ""Provider"", ""PhoneNumberId"");
            ");
                    }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId') THEN
        DROP INDEX ""UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId"";
    END IF;
END$$;");
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_WhatsAppPhoneNumbers_BizProviderPhone""
ON ""WhatsAppPhoneNumbers"" (""BusinessId"", ""Provider"", ""PhoneNumberId"");
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""ix_ctaflowconfigs_biz_active_name""
ON ""CTAFlowConfigs"" (""BusinessId"", ""IsActive"", ""FlowName"");
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE constraint_type='FOREIGN KEY' AND table_name='AudienceMembers'
                 AND constraint_name='FK_AudienceMembers_Audiences_AudienceId') THEN
        ALTER TABLE ""AudienceMembers"" DROP CONSTRAINT ""FK_AudienceMembers_Audiences_AudienceId"";
    END IF;
END$$;");
            migrationBuilder.AddForeignKey(
                name: "FK_AudienceMembers_Audiences_AudienceId",
                table: "AudienceMembers",
                column: "AudienceId",
                principalTable: "Audiences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE constraint_type='FOREIGN KEY' AND table_name='Audiences'
                 AND constraint_name='FK_Audiences_Campaigns_CampaignId') THEN
        ALTER TABLE ""Audiences"" DROP CONSTRAINT ""FK_Audiences_Campaigns_CampaignId"";
    END IF;
END$$;");
            migrationBuilder.AddForeignKey(
                name: "FK_Audiences_Campaigns_CampaignId",
                table: "Audiences",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE constraint_type='FOREIGN KEY' AND table_name='MessageLogs'
                 AND constraint_name='FK_MessageLogs_Campaigns_CampaignId') THEN
        ALTER TABLE ""MessageLogs"" DROP CONSTRAINT ""FK_MessageLogs_Campaigns_CampaignId"";
    END IF;
END$$;");
            migrationBuilder.AddForeignKey(
                name: "FK_MessageLogs_Campaigns_CampaignId",
                table: "MessageLogs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE constraint_type='FOREIGN KEY' AND table_name='MessageStatusLogs'
                 AND constraint_name='FK_MessageStatusLogs_Campaigns_CampaignId') THEN
        ALTER TABLE ""MessageStatusLogs"" DROP CONSTRAINT ""FK_MessageStatusLogs_Campaigns_CampaignId"";
    END IF;
END$$;");
            migrationBuilder.AddForeignKey(
                name: "FK_MessageStatusLogs_Campaigns_CampaignId",
                table: "MessageStatusLogs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE constraint_type='FOREIGN KEY' AND table_name='TrackingLogs'
                 AND constraint_name='FK_TrackingLogs_Campaigns_CampaignId') THEN
        ALTER TABLE ""TrackingLogs"" DROP CONSTRAINT ""FK_TrackingLogs_Campaigns_CampaignId"";
    END IF;
END$$;");
            migrationBuilder.AddForeignKey(
                name: "FK_TrackingLogs_Campaigns_CampaignId",
                table: "TrackingLogs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.table_constraints
                           WHERE constraint_type='FOREIGN KEY' AND table_name='TrackingLogs'
                             AND constraint_name='FK_TrackingLogs_MessageLogs_MessageLogId') THEN
                    ALTER TABLE ""TrackingLogs"" DROP CONSTRAINT ""FK_TrackingLogs_MessageLogs_MessageLogId"";
                END IF;
            END$$;");
            migrationBuilder.AddForeignKey(
                name: "FK_TrackingLogs_MessageLogs_MessageLogId",
                table: "TrackingLogs",
                column: "MessageLogId",
                principalTable: "MessageLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.table_constraints
                           WHERE constraint_type='FOREIGN KEY' AND table_name='TrackingLogs'
                             AND constraint_name='FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId') THEN
                    ALTER TABLE ""TrackingLogs"" DROP CONSTRAINT ""FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId"";
                END IF;
            END$$;");
            migrationBuilder.AddForeignKey(
                name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId",
                table: "TrackingLogs",
                column: "CampaignSendLogId",
                principalTable: "CampaignSendLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Revert ContactId NOT NULL (default Guid.Empty to satisfy NOT NULL)
            migrationBuilder.AlterColumn<Guid>(
                name: "ContactId",
                table: "CampaignSendLogs",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
